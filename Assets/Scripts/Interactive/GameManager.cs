using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [Serializable] public class PrefabCardBinding { public GameObject prefab; [Min(1)] public int count = 1; }
    [Serializable] public class PrefabSlotBinding { public GameObject prefab; [Min(1)] public int count = 1; }

    [Header("Roots")] public Transform playerBoardRoot; public Transform aiBoardRoot;
    [Header("UI")] public Button btnForceFlip; public Button btnAttack; public Button btnEndTurn; public Button btnSwap;
    [Header("LOG")] public Text logText; static readonly StringBuilder _logBuf = new StringBuilder(4096);

    [Header("HUD")]
    public Text hpText;
    public Text apText;
    public Text EnemyHptxt;


    [Header("Match parameters")] public int turns = 10; public int playerBaseAP = 3; public int seed = 12345;
    [Header("Start constraints")]
    [Min(1)] public int CardsPerSide = 3;
    [Min(1)] public int StartingHandSize = 3;

    [Header("Refs")]
    [SerializeField] private HandManager handManager;

    [Header("Empty Spot")] public GameObject EmptySpot;
    [Header("Empty Slot")] public GameObject EmptySlot;

    [Header("Prefab bindings")] public List<PrefabCardBinding> playerCards = new List<PrefabCardBinding>();
    [Header("Enemy Slots (bindings only)")] public List<PrefabSlotBinding> enemySlots = new List<PrefabSlotBinding>();

    bool awaitingEndTurn = false;
    static GameManager _instance; public static GameManager Instance => _instance;

    System.Random rng; public PlayerState player; public PlayerState ai;
    int currentTurn = 1; bool playerPhase = true; bool matchEnded = false;

    readonly Dictionary<CardInstance, CardView> viewByInstance = new Dictionary<CardInstance, CardView>();
    readonly List<CardView> playerViews = new List<CardView>();
    readonly Dictionary<CardInstance, List<AbilityBase>> abilitiesByInstance = new Dictionary<CardInstance, List<AbilityBase>>();

    readonly Dictionary<SlotInstance, SlotView> slotViewByInstance = new Dictionary<SlotInstance, SlotView>();
    readonly List<SlotView> enemySlotViews = new List<SlotView>();


    void Awake()
    {
        Logger.SetSink(AppendLog);
        EventBus.Publish(GameEventType.Info, new EventContext { phase = "GameManager ready" });
        _instance = this;

        // Listener: se non assegnati in Inspector -> NRE (voluto)
        btnForceFlip.onClick.AddListener(OnForceFlip);
        btnAttack.onClick.AddListener(OnAttack);
        btnEndTurn.onClick.AddListener(OnEndTurn);
        btnSwap.onClick.AddListener(OnSwap);
    }

    void Start()
    {
        rng = new System.Random(seed);
        player = new PlayerState("Player", playerBaseAP);
        ai = new PlayerState("AI", 0);

        ClearChildrenUnder(playerBoardRoot);

        // 1) Riempio il board del player con degli EmptySpot invece delle carte
        SpawnInitialEmptySpots();

        // 2) Creo gli slot nemici davanti a ogni lane del player (come prima)
        SpawnEnemySlots();

        // 3) Creo la mano iniziale
        SpawnStartingHand();

        EventBus.Publish(GameEventType.Info, new EventContext { phase = "=== MATCH START ===" });

        UpdateAllViews();
        UpdateHUD();
        StartTurn(player, ai, true);
    }

    // === BUILD ===
    void SpawnStartingHand()
    {
        if (handManager == null) return;
        if (StartingHandSize <= 0) return;

        // Costruisco un "mazzo" lineare dai bindings
        var deck = new List<GameObject>();
        foreach (var b in playerCards)
            for (int i = 0; i < b.count; i++)
                deck.Add(b.prefab);

        if (deck.Count == 0) return;

        // Shuffle semplice
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }

        // Pesco StartingHandSize carte e le metto in mano
        player.actionPoints = StartingHandSize;
        for (int i = 0; i < StartingHandSize; i++)
        {
            handManager.DrawCard();
        }
        player.actionPoints = playerBaseAP;
    }


    void SpawnInitialEmptySpots()
    {
        if (EmptySpot == null) return;

        for (int i = 0; i < CardsPerSide; i++)
        {
            var spotGO = Instantiate(EmptySpot, playerBoardRoot);
            spotGO.name = EmptySpot.name;
            spotGO.SetActive(true);
            spotGO.transform.SetSiblingIndex(i);

            // Stesso comportamento degli EmptySpot creati quando muore una carta

            // Outline
            var outline = spotGO.GetComponent<Outline>();
            if (outline == null)
            {
                outline = spotGO.AddComponent<Outline>();
                outline.enabled = false;
                outline.effectDistance = new Vector2(5f, 5f);
                outline.useGraphicAlpha = false;
                outline.effectColor = Color.white;
            }

            // Button -> OnEmptySpotClicked
            var btn = spotGO.GetComponent<Button>();
            if (btn != null)
            {
                var t = spotGO.transform; // catturo la transform
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnEmptySpotClicked(t));
            }
        }
    }

    void AddSlotFromTemplate(PlayerState owner, SlotDefinition.Spec def, GameObject prefab, Transform root, List<SlotView> outViews)
    {
        var si = new SlotInstance(def);
        var go = Instantiate(prefab, root); go.name = prefab.name; go.SetActive(true);

        var view = go.GetComponent<SlotView>();
        view.Init(this, owner, si);
        slotViewByInstance[si] = view; outViews.Add(view);

        foreach (var ab in go.GetComponents<AbilityBase>()) ab.Bind(null, ai, player);
    }

    void SpawnEnemySlots()
    {
        enemySlotViews.Clear();
        var toKill = new List<GameObject>();
        foreach (Transform t in aiBoardRoot) toKill.Add(t.gameObject);
        for (int i = 0; i < toKill.Count; i++) Destroy(toKill[i]);
        slotViewByInstance.Clear();

        var flat = new List<GameObject>();
        for (int i = 0; i < enemySlots.Count; i++)
            for (int k = 0; k < enemySlots[i].count; k++) flat.Add(enemySlots[i].prefab);

        for (int i = flat.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (flat[i], flat[j]) = (flat[j], flat[i]); }

        int lanes = playerBoardRoot.childCount;
        for (int i = 0; i < lanes; i++)
        {
            var prefab = flat[i % flat.Count];
            var sd = prefab.GetComponent<SlotDefinition>();
            var spec = sd.BuildSpec();
            AddSlotFromTemplate(ai, spec, prefab, aiBoardRoot, enemySlotViews);
        }

        for (int i = 0; i < aiBoardRoot.childCount; i++) aiBoardRoot.GetChild(i).SetSiblingIndex(i);
    }
    // === REFRESH / HUD ===
    public void UpdateAllViews()
    {
        // 1) Prima passo: rimuovo tutte le carte morte (di qualunque owner)
        //    Uso uno snapshot perché RemoveCard modifica viewByInstance.
        var viewsSnapshot = viewByInstance.Values.ToList();
        foreach (var v in viewsSnapshot)
        {
            if (v.instance != null && !v.instance.alive)
            {
                // v.owner è settato in CardView.Init, quindi è il PlayerState giusto
                RemoveCard(v.owner, v.instance);
            }
        }

        // 2) Secondo passo: refresh di tutte le carte ancora vive
        foreach (var v in viewByInstance.Values)
            v.Refresh();

        // 3) Slot nemici: come prima (già corretta la rimozione)
        for (int i = enemySlotViews.Count - 1; i >= 0; i--)
        {
            var sv = enemySlotViews[i];
            if (!sv.instance.alive)
            {
                RemoveSlotView(sv);
                continue;
            }
            sv.Refresh();
        }
    }

    public void UpdateHUD()
    {
        if (matchEnded) return;
        if (playerPhase)
        {
            btnAttack.interactable = !awaitingEndTurn;
            btnForceFlip.interactable = !awaitingEndTurn && player.actionPoints > 0;
        }
        else
        {
            btnAttack.interactable = false;
            btnForceFlip.interactable = false;
        }

        // Aggiorno HUD giocatore (vita + punti abilità)
        if (hpText != null)
            hpText.text = $"{player.hp}";

        if (apText != null)
            apText.text = $"{player.actionPoints}/{playerBaseAP}";

        if (EnemyHptxt != null)
            EnemyHptxt.text = $"{ai.hp}";
    }
    
    // === TURN FLOW / UI ACTIONS ===
    void StartTurn(PlayerState owner, PlayerState opponent, bool isPlayerPhase)
    {
        playerPhase = isPlayerPhase; awaitingEndTurn = false;
        owner.actionPoints = (owner == player) ? playerBaseAP : 0;

        EventBus.Publish(GameEventType.TurnStart, new EventContext { owner = owner, opponent = opponent, phase = $"TURN {currentTurn}" });
        if (playerPhase) RandomizePlayerLayoutAndSides(); else ExecuteAiTurnStartActions();
        UpdateAllViews(); UpdateHUD();
    }

    void EndTurnInternal(PlayerState owner, PlayerState opponent)
    {
        if (matchEnded) return;

        EventBus.Publish(GameEventType.TurnEnd, new EventContext { owner = owner, opponent = opponent, phase = "TurnEnd" });

        if (owner == ai)
        {
            ShuffleEnemySlotsPreservingInstances();
            EventBus.Publish(GameEventType.Info, new EventContext { owner = ai, opponent = player, phase = "EnemyTurnEndShuffle" });
            UpdateAllViews();
        }

        if (IsGameOver() || currentTurn >= turns) EndMatch();
    }

    // === SLOTS SHUFFLE (preserva istanze/HP) ===
    void ShuffleEnemySlotsPreservingInstances()
    {
        var children = new List<Transform>();
        for (int i = 0; i < aiBoardRoot.childCount; i++) children.Add(aiBoardRoot.GetChild(i));
        if (children.Count <= 1) return;

        for (int i = children.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            if (i == j) continue;

            int iIdx = children[i].GetSiblingIndex();
            int jIdx = children[j].GetSiblingIndex();
            children[i].SetSiblingIndex(jIdx);
            children[j].SetSiblingIndex(iIdx);
        }

        for (int i = 0; i < aiBoardRoot.childCount; i++) aiBoardRoot.GetChild(i).SetSiblingIndex(i);
    }

    void OnForceFlip()
    {
        if (awaitingEndTurn || matchEnded || !playerPhase) { UpdateHUD(); return; }
        if (player.actionPoints <= 0) { EventBus.Publish(GameEventType.Info, new EventContext { phase = "Not enough Player PA" }); UpdateHUD(); return; }

        if (SelectionManager.Instance.SelectedOwned == null ||SelectionManager.Instance.SelectedOwned.instance == null)
        {
            // Nessuna carta selezionata -> non fare nulla
            return;
        }

        var sel = SelectionManager.Instance.SelectedOwned.instance;

        sel.Flip();
        player.actionPoints -= 1;

        EventBus.Publish(GameEventType.Flip, new EventContext
        {
            owner = player,
            opponent = ai,
            source = sel
        });

        UpdateAllViews();
        UpdateHUD();
    }

    void OnAttack()
    {
        if (awaitingEndTurn || matchEnded || !playerPhase) { UpdateHUD(); return; }

        // Il numero di lane è guidato dal numero di carte del player
        int lanes = playerBoardRoot.childCount;
        for (int lane = 0; lane < lanes; lane++)
        {
            var pView = playerBoardRoot.GetChild(lane).GetComponentInChildren<CardView>(false);
            if (pView == null || pView.instance == null) continue;

            var ci = pView.instance;
            if (!ci.alive || ci.side != Side.Fronte) continue;

            // Recupero lo slot nemico SOLO se esiste quel child
            SlotView sView = null;
            if (lane < aiBoardRoot.childCount)
                sView = aiBoardRoot.GetChild(lane).GetComponentInChildren<SlotView>(false);

            // Se non c'è uno slot attivo davanti (slot distrutto o proprio nessuno) -> danno diretto agli HP nemico
            if (sView == null || !sView.instance.alive)
            {
                int dmg = ci.def.frontDamage;
                ci.DealDamageToPlayer(player, ai, dmg, "DirectToEnemy");
            }
            else
            {
                // Slot ancora vivo -> attacco normale allo slot
                var si = sView.instance;
                if (!si.alive) continue;

                ci.Attack(player, ai, si);
            }
        }

        CleanupDestroyedSlots();
        UpdateAllViews();
        awaitingEndTurn = true; 
        UpdateHUD();
    }
    void OnSwap()
    {
        if (awaitingEndTurn || matchEnded || !playerPhase) { UpdateHUD(); return; }

        // dice al SelectionManager: "arma lo swap con la carta attualmente selezionata"
        SelectionManager.Instance.BeginSwap();
    }


    public void SwapCardPositions(CardView a, CardView b)
    {
        if (a == null || b == null || a == b) return;

        var tA = a.transform;
        var tB = b.transform;

        // tutte le carte sono del player -> devono essere figli di playerBoardRoot
        if (tA.parent != playerBoardRoot || tB.parent != playerBoardRoot)
            return;

        int idxA = tA.GetSiblingIndex();
        int idxB = tB.GetSiblingIndex();
        player.actionPoints -= 1;
        tA.SetSiblingIndex(idxB);
        tB.SetSiblingIndex(idxA);

        EventBus.Publish(GameEventType.Info, new EventContext
        {
            phase = $"[Swap] L{idxA + 1} <-> L{idxB + 1}"
        });

        UpdateAllViews();
        UpdateHUD();
    }


    void OnEndTurn()
    {
        if (matchEnded) return;
        if (playerPhase)
        {
            EndTurnInternal(player, ai);
            if (!matchEnded) StartTurn(ai, player, false);
        }
        else
        {
            EndTurnInternal(ai, player);
            if (!matchEnded) { currentTurn++; StartTurn(player, ai, true); }
        }
    }

    // === AI ===
    void ExecuteAiTurnStartActions() { ApplyEnemySlotsEffectsLeftToRight(); }

    void ApplyEnemySlotsEffectsLeftToRight()
    {
        int lanes = Mathf.Min(aiBoardRoot.childCount, playerBoardRoot.childCount);
        var aiChildren = new Transform[lanes];
        for (int i = 0; i < lanes; i++) aiChildren[i] = aiBoardRoot.GetChild(i);

        for (int lane = 0; lane < lanes; lane++)
        {
            var sView = aiChildren[lane].GetComponentInChildren<SlotView>(false);
            if (sView == null || !sView.instance.alive)    // <--- AGGIUNTO null check
                continue;

            EventBus.Publish(GameEventType.Info, new EventContext { phase = $"[SlotEffect] Lane {lane + 1}" });
            EventBus.Publish(GameEventType.Custom, new EventContext
            {
                owner = ai,
                opponent = player,
                source = sView.instance,
                phase = "SlotEffect"
            });
        }
    }


    // === CLEANUP ===

    void RemoveCard(PlayerState owner, CardInstance ci)
    {
        // Unbind abilità
        if (abilitiesByInstance.TryGetValue(ci, out var list))
        {
            for (int i = 0; i < list.Count; i++)
                list[i].Unbind();
            abilitiesByInstance.Remove(ci);
        }

        // Trovo il view associato
        if (!viewByInstance.TryGetValue(ci, out var view))
            return;

        // Se era selezionata, deseleziona
        var sel = SelectionManager.Instance;
        if (sel != null && sel.SelectedOwned == view)
            sel.SelectOwned(null);

        // Salvo parent e indice di lane PRIMA di distruggere la carta
        Transform parent = view.transform.parent;
        int laneIndex = view.transform.GetSiblingIndex();

        // Rimuovo il modello dal dizionario e dalla board del player/ai
        viewByInstance.Remove(ci);
        owner.board.Remove(ci);
        ci.Dispose();

        // Distruggo il GameObject della carta
        Destroy(view.gameObject);

        // SOLO per il player: rimpiazzo lo slot con il prefab EmptySpot
        if (owner == player && EmptySpot != null && parent != null)
        {
            var spotGO = Instantiate(EmptySpot, parent);
            spotGO.name = EmptySpot.name;
            spotGO.SetActive(true);
            spotGO.transform.SetSiblingIndex(laneIndex);

            // AGGIUNTA: assicuro che lo EmptySpot abbia un Outline come le carte
            var outline = spotGO.GetComponent<Outline>();
            if (outline == null)
            {
                outline = spotGO.AddComponent<Outline>();
                outline.enabled = false;                    // parte spento
                outline.effectDistance = new Vector2(5f, 5f); // usa gli stessi valori che usi sulle carte
                outline.useGraphicAlpha = false;
                outline.effectColor = Color.white;
            }

            // Assicuro che il bottone chiami GameManager.OnEmptySpotClicked
            var btn = spotGO.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnEmptySpotClicked(spotGO.transform));
            }
        }

        EventBus.Publish(GameEventType.Info, new EventContext
        {
            owner = owner,
            opponent = (owner == player) ? ai : player,
            source = ci,
            phase = "[GM] Removed destroyed card (replaced by EmptySpot if player)"
        });
    }


    void PlayCardFromHand(CardView handCard, Transform emptySpot)
    {
        if (handCard == null || emptySpot == null) return;

        var cd = handCard.GetComponent<CardDefinition>();
        if (cd == null)
        {
            Debug.LogError("[GM] Card in hand senza CardDefinition!");
            return;
        }

        // Parent e lane dello spot vuoto
        Transform parent = emptySpot.parent != null ? emptySpot.parent : playerBoardRoot;
        int laneIndex = emptySpot.GetSiblingIndex();

        // Lo spot vuoto non serve più
        Destroy(emptySpot.gameObject);

        // --- MODELLO / LOGICA ---
        var spec = cd.BuildSpec();
        var ci = new CardInstance(spec, rng);
        ci.AssignGM(this);
        player.board.Add(ci);

        // --- VIEW: clono la carta dalla mano ma la "resetto" da board ---
        GameObject go = Instantiate(handCard.gameObject, parent);
        go.name = handCard.gameObject.name;
        go.SetActive(true);
        go.transform.SetSiblingIndex(laneIndex);

        // Reset di scala/rotazione/posizione per farla sembrare come le altre sul tabellone
        var rt = go.transform as RectTransform;
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
        }
        else
        {
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
        }

        var view = go.GetComponent<CardView>();
        if (view == null)
        {
            Debug.LogError("[GM] Il clone della carta non ha CardView!");
            return;
        }

        // Inizializzo come una normale carta sul board
        view.Init(this, player, ci);
        view.SetHighlight(false);
        viewByInstance[ci] = view;

        // Bind delle abilità
        var opponent = ai;
        var abilities = go.GetComponents<AbilityBase>().ToList();
        foreach (var ab in abilities) ab.Bind(ci, player, opponent);
        abilitiesByInstance[ci] = abilities;

        // Rimuovo la carta dalla mano (questa distrugge SOLO la carta in mano, non il clone sul board)
        if (handManager != null)
            handManager.RemoveFromHand(handCard.gameObject);

        EventBus.Publish(GameEventType.CardPlayed, new EventContext
        {
            owner = player,
            opponent = ai,
            source = ci,
            phase = "FromHandToEmptySpot"
        });

        UpdateAllViews();
        UpdateHUD();
    }


    void CleanupDestroyedSlots()
    {
        for (int i = enemySlotViews.Count - 1; i >= 0; i--)
            if (!enemySlotViews[i].instance.alive) RemoveSlotView(enemySlotViews[i]);
    }

    // === SLOTS REBUILD ===
    

    void RemoveSlotView(SlotView v)
    {
        // Salvo parent e lane PRIMA di distruggere
        Transform parent = v.transform.parent;
        int laneIndex = v.transform.GetSiblingIndex();

        // Lo tolgo dalle strutture dati logiche
        enemySlotViews.Remove(v);
        slotViewByInstance.Remove(v.instance);

        // Distruggo lo slot attuale
        Destroy(v.gameObject);

        // Rimpiazzo il posto con il prefab EmptySlot, senza far "scalare" gli altri
        if (EmptySlot != null && parent != null)
        {
            var empty = Instantiate(EmptySlot, parent);
            empty.name = EmptySlot.name;
            empty.SetActive(true);
            empty.transform.SetSiblingIndex(laneIndex);
        }
    }


    // === Utility ===
    public object GetOpponentObjInstance(object obj)
    {
        if (obj is CardInstance card)
        {
            var atkView = viewByInstance[card];
            Transform oppRoot = (atkView.owner == player) ? aiBoardRoot : playerBoardRoot;
            int lane = atkView.transform.GetSiblingIndex();

            if (lane < 0 || lane >= oppRoot.childCount)
                return null;

            if (atkView.owner == player)
            {
                var sView = oppRoot.GetChild(lane).GetComponentInChildren<SlotView>(false);
                if (sView == null)                    // <--- AGGIUNTO null check
                    return null;

                var si = sView.instance;
                return si.alive ? si : null;
            }
            else
            {
                var cView = oppRoot.GetChild(lane).GetComponentInChildren<CardView>(false);
                var ci = cView.instance;
                return ci.alive ? ci : null;
            }
        }

        if (obj is SlotInstance slot)
        {
            var sView = slotViewByInstance[slot];
            int lane = sView.transform.GetSiblingIndex();

            if (lane < 0 || lane >= playerBoardRoot.childCount)
                return null;

            var cView = playerBoardRoot.GetChild(lane).GetComponentInChildren<CardView>(false);
            if (cView == null)          // <-- nessuna carta: lane con EmptySpot
                return null;            // le abilità possono interpretare questo come "danno diretto al player"

            var ci = cView.instance;
            return ci.alive ? ci : null;
        }


        return null;
    }

    public int GetLaneIndexFor(object obj)
    {
        if (obj is CardInstance ci) return viewByInstance[ci].transform.GetSiblingIndex();
        if (obj is SlotInstance si) return slotViewByInstance[si].transform.GetSiblingIndex();
        return -1;
    }

    void RandomizePlayerLayoutAndSides()
    {
        /*
        */
        // Shuffle visuale dei figli (lasciato commentato: da attivare in futuro)
        var children = new List<Transform>();
        foreach (Transform t in playerBoardRoot) children.Add(t);
        for (int i = children.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            if (i != j)
            {
                int iIdx = children[i].GetSiblingIndex();
                int jIdx = children[j].GetSiblingIndex();
                children[i].SetSiblingIndex(jIdx); children[j].SetSiblingIndex(iIdx);
            }
        }
        
        // Random side su carte vive
        var cards = player.board.ToArray();
        for (int i = 0; i < cards.Length; i++)
        {
            var ci = cards[i];
            var newSide = (rng.NextDouble() < 0.5) ? Side.Fronte : Side.Retro;
            if (ci.side != newSide)
            {
                ci.Flip();
                EventBus.Publish(GameEventType.Flip, new EventContext { owner = player, opponent = ai, source = ci, phase = "TurnStartRandomize" });
            }
        }
    }

    void ClearChildrenUnder(Transform root)
    {
        var toKill = new List<GameObject>();
        foreach (Transform t in root) toKill.Add(t.gameObject);
        for (int i = 0; i < toKill.Count; i++) Destroy(toKill[i]);
    }

    public void AppendLog(string msg) { _logBuf.AppendLine(msg); logText.text = _logBuf.ToString(); }

    public void ClearLog() { _logBuf.Clear(); logText.text = ""; }

    bool IsGameOver() => player.hp <= 0 || ai.hp <= 0;

    void EndMatch()
    {
        if (matchEnded) return; matchEnded = true;
        int diff = ai.hp - player.hp;
        string result = diff > 0 ? "AI AHEAD" : diff < 0 ? "PLAYER AHEAD" : "TIE";
        EventBus.Publish(GameEventType.Info, new EventContext { phase = "=== MATCH END ===" });
        EventBus.Publish(GameEventType.Info, new EventContext { phase = $"Score: PlayerHP {player.hp} vs AIHP {ai.hp} | Diff (AI-Player) = {diff} -> {result}" });
    }

    // CLICK
    public void OnCardClicked(CardView view)
    {
        if (matchEnded) return;

        // 1) Carta in mano
        bool isHandCard = (view.owner == null && view.instance == null);
        if (isHandCard)
        {
            Transform emptySpot = null;
            if (SelectionManager.Instance != null)
                emptySpot = SelectionManager.Instance.SelectedEmptySpot;

            if (emptySpot != null)
            {
                PlayCardFromHand(view, emptySpot);

                // dopo aver riempito lo spot, deseleziono lo spot (e quindi spengo l'outline)
                if (SelectionManager.Instance != null)
                    SelectionManager.Instance.SelectEmptySpot(null);
            }

            return;
        }

        // 2) Carta del player sul board
        bool isPlayers = view.owner == player && view.instance != null;
        if (isPlayers)
        {
            // la selezione della carta gestisce già lo spegnimento di eventuali empty spot
            SelectionManager.Instance.SelectOwned(view);
        }
    }



    public void OnEmptySpotClicked(Transform emptySpot)
    {
        if (matchEnded || emptySpot == null) return;

        if (SelectionManager.Instance != null)
            SelectionManager.Instance.SelectEmptySpot(emptySpot);
    }



}
