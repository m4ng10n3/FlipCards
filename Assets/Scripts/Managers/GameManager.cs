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
    [Header("UI")] public Button btnAttack; public Button btnEndTurn; 
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
    public HandManager HandManager => handManager;
    [SerializeField] private SlotBatchManager slotBatchManager;

    [Header("Empty Spot")] public GameObject EmptySpot;
    [Header("Empty Slot")] public GameObject EmptySlot;

    [Header("Prefab bindings")] public List<PrefabCardBinding> playerCards = new List<PrefabCardBinding>();
    [Header("Enemy Slots (bindings only)")] public List<PrefabSlotBinding> enemySlots = new List<PrefabSlotBinding>();

    bool awaitingEndTurn = false;
    static GameManager _instance; public static GameManager Instance => _instance;

    System.Random rng; public PlayerState player; public PlayerState ai;
    int currentTurn = 1; bool playerPhase = true; bool matchEnded = false;

    readonly Dictionary<CardInstance, CardView> viewByInstance = new Dictionary<CardInstance, CardView>();
    readonly Dictionary<CardInstance, List<AbilityBase>> abilitiesByInstance = new Dictionary<CardInstance, List<AbilityBase>>();

    readonly Dictionary<SlotInstance, SlotView> slotViewByInstance = new Dictionary<SlotInstance, SlotView>();
    readonly List<SlotView> enemySlotViews = new List<SlotView>();

    Transform playerBoardRootClone;
    public Transform PlayerBoardRootClone => playerBoardRootClone;

    void Awake()
    {
        Logger.SetSink(AppendLog);
        EventBus.Publish(GameEventType.Info, new EventContext { phase = "GameManager ready" });
        _instance = this;
        if (handManager == null) throw new InvalidOperationException("HandManager missing");

        btnAttack.onClick.AddListener(OnAttack);
        btnEndTurn.onClick.AddListener(OnEndTurn);
    }

    void Start()
    {
        rng = new System.Random(seed);
        player = new PlayerState("Player", playerBaseAP);
        ai = new PlayerState("AI", 0);

        ClearChildrenUnder(playerBoardRoot);

        SpawnInitialEmptySpots();
        var cloneGO = Instantiate(playerBoardRoot.gameObject, playerBoardRoot.parent);
        cloneGO.name = $"{playerBoardRoot.name}_Clone";
        cloneGO.transform.SetSiblingIndex(playerBoardRoot.GetSiblingIndex());
        playerBoardRootClone = cloneGO.transform;

        for (int i = 0; i < playerBoardRootClone.childCount; i++)
        {
            var child = playerBoardRootClone.GetChild(i);
            if (child.gameObject.name != EmptySpot.name) continue;
            child.GetComponent<Image>().enabled = false;
        }

        SpawnEnemySlots();

        SpawnStartingHand();

        EventBus.Publish(GameEventType.Info, new EventContext { phase = "=== MATCH START ===" });

        UpdateAllViews();
        UpdateHUD();
        StartTurn(player, ai, true);
    }

    void SpawnStartingHand()
    {
        player.actionPoints = StartingHandSize;
        for (int i = 0; i < StartingHandSize; i++) handManager.DrawCard();
        player.actionPoints = playerBaseAP;
    }


    void SpawnInitialEmptySpots()
    {
        for (int i = 0; i < CardsPerSide; i++)
        {
            var spotGO = Instantiate(EmptySpot, playerBoardRoot);
            spotGO.name = EmptySpot.name;
            spotGO.SetActive(true);
            spotGO.transform.SetSiblingIndex(i);

            var outline = spotGO.GetComponent<Outline>();
            outline.enabled = false;
            outline.effectDistance = new Vector2(5f, 5f);
            outline.useGraphicAlpha = false;
            outline.effectColor = Color.white;

            var btn = spotGO.GetComponent<Button>();
            var t = spotGO.transform;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnEmptySpotClicked(t));
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
    public void UpdateAllViews()
    {
        var viewsSnapshot = viewByInstance.Values.ToList();
        foreach (var v in viewsSnapshot)
        {
            if (v.instance != null && !v.instance.alive)
            {
                RemoveCard(v.owner, v.instance);
            }
        }

        foreach (var v in viewByInstance.Values)
            v.Refresh();

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
        var enable = playerPhase && !awaitingEndTurn;
        btnAttack.interactable = enable;
        handManager.btnDraw.interactable = enable;

        hpText.text = $"{player.hp}";
        apText.text = $"{player.actionPoints}/{playerBaseAP}";
        EnemyHptxt.text = $"{ai.hp}";
    }
    
    void StartTurn(PlayerState owner, PlayerState opponent, bool isPlayerPhase)
    {
        playerPhase = isPlayerPhase;
        awaitingEndTurn = false;
        owner.actionPoints = playerBaseAP;
        EventBus.Publish(GameEventType.TurnStart, new EventContext
        {
            owner    = owner,
            opponent = opponent,
            phase    = $"TURN {currentTurn}"
        });
        UpdateAllViews();
        UpdateHUD();
    }

    // ═══════════════════════════════════════════════════════════
    //  RISOLUZIONE LANE (sostituisce il vecchio OnAttack)
    // ═══════════════════════════════════════════════════════════

    void OnAttack()
    {
        if (awaitingEndTurn || matchEnded || !playerPhase) { UpdateHUD(); return; }

        int lanes = Mathf.Max(playerBoardRoot.childCount, aiBoardRoot.childCount);

        for (int lane = 0; lane < lanes; lane++)
        {
            CardInstance card = null;
            SlotInstance slot = null;

            if (lane < playerBoardRoot.childCount)
            {
                var cv = playerBoardRoot.GetChild(lane).GetComponentInChildren<CardView>(false);
                if (cv?.instance != null && cv.instance.alive) card = cv.instance;
            }

            if (lane < aiBoardRoot.childCount)
            {
                var sv = aiBoardRoot.GetChild(lane).GetComponentInChildren<SlotView>(false);
                if (sv?.instance != null && sv.instance.alive) slot = sv.instance;
            }

            LaneResolver.Resolve(lane, card, slot, player, ai);
        }

        // Sinergie — controlla combo dopo la risoluzione di tutte le lane
        SynergyResolver.Resolve(player.board, player, ai);

        CleanupDestroyedSlots();
        UpdateAllViews();
        awaitingEndTurn = true;
        UpdateHUD();
    }

    // ═══════════════════════════════════════════════════════════
    //  CAOS ROGUELITE — flip/swap casuali a fine turno
    // ═══════════════════════════════════════════════════════════

    void RandomizePlayerBoard()
    {
        var board = player.board;
        if (board.Count == 0) return;

        // 1. Flip casuale: ogni carta usa la sua endTurnFlipChance
        for (int i = 0; i < board.Count; i++)
        {
            var ci = board[i];
            if (!ci.alive) continue;
            if (rng.NextDouble() < ci.def.endTurnFlipChance)
            {
                ci.Flip();
                EventBus.Publish(GameEventType.Flip, new EventContext { owner = player, opponent = ai, source = ci });

                // Notifica visiva sulla CardView corrispondente
                if (viewByInstance.TryGetValue(ci, out var cv))
                    cv.FlipSide(false);
            }
        }

        // 2. Swap casuale tra coppie adiacenti (25% per coppia)
        for (int i = 0; i < playerBoardRoot.childCount - 1; i++)
        {
            if (rng.NextDouble() >= 0.25) continue;

            var childA = playerBoardRoot.GetChild(i);
            var childB = playerBoardRoot.GetChild(i + 1);

            var cvA = childA.GetComponentInChildren<CardView>(false);
            var cvB = childB.GetComponentInChildren<CardView>(false);
            if (cvA == null || cvB == null) continue;
            if (cvA.instance == null || cvB.instance == null) continue;

            // Scambia i sibling index
            childA.SetSiblingIndex(i + 1);
            childB.SetSiblingIndex(i);

            Logger.Info($"[Caos] Swap: L{i + 1} {cvA.instance.def.cardName} ↔ L{i + 2} {cvB.instance.def.cardName}");
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  ACCUMULO CHARGE (chiamato a fine turno per carte in Retro)
    // ═══════════════════════════════════════════════════════════

    void AccumulateFlipCharges()
    {
        foreach (var ci in player.board)
        {
            if (!ci.alive || ci.side != Side.Retro) continue;
            ci.flipCharge = Mathf.Min(ci.flipCharge + 1, CardInstance.MaxFlipCharge);
            string bar = ci.flipCharge == 3 ? "●●● MAX!" :
                         ci.flipCharge == 2 ? "●●○"      : "●○○";
            ci.PushHint($"Charge {bar}");
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  AVANZAMENTO PATTERN SLOT (chiamato a fine turno)
    // ═══════════════════════════════════════════════════════════

    void AdvanceSlotPatterns()
    {
        for (int i = 0; i < enemySlotViews.Count; i++)
        {
            var sv = enemySlotViews[i];
            if (sv?.instance == null || !sv.instance.alive) continue;

            Side oldSide = sv.instance.side;
            sv.instance.AdvanceFlip();
            sv.Refresh();

            if (sv.instance.side != oldSide)
                Logger.Info($"[Slot] {sv.instance.def.SlotName} → {sv.instance.side}");
        }
    }

    public void OnCardDoubleClicked(CardView view)
    {
        TryFlipCard(view);
    }

    private bool TryFlipCard(CardView view)
    {
        if (awaitingEndTurn || matchEnded || !playerPhase) { UpdateHUD(); return false; }
        if (player.actionPoints <= 0) { EventBus.Publish(GameEventType.Info, new EventContext { phase = "Not enough Player PA" }); UpdateHUD(); return false; }

        if (view == null || view.instance == null || view.owner != player)
            return false;

        view.instance.Flip();
        player.actionPoints -= 1;

        EventBus.Publish(GameEventType.Flip, new EventContext
        {
            owner = player,
            opponent = ai,
            source = view.instance
        });

        UpdateAllViews();
        UpdateHUD();
        return true;
    }
    public void SwapCardPositions(CardView a, CardView b)
    {
        if (matchEnded || awaitingEndTurn || !playerPhase) { UpdateHUD(); return; }
        if (a == null || b == null || a == b || player.actionPoints <= 0)
            return;

        var cA = a.PlayerBoardContainer != null
            ? a.PlayerBoardContainer
            : a.EnsurePlayerBoardContainer(playerBoardRoot);

        var cB = b.PlayerBoardContainer != null
            ? b.PlayerBoardContainer
            : b.EnsurePlayerBoardContainer(playerBoardRoot);

        if (cA == null || cB == null || cA == cB)
            return;

        if (cA.parent != playerBoardRoot || cB.parent != playerBoardRoot)
            return;

        int idxA = cA.GetSiblingIndex();
        int idxB = cB.GetSiblingIndex();

        Vector3 posA = cA.localPosition;
        Vector3 posB = cB.localPosition;
        Quaternion rotA = cA.localRotation;
        Quaternion rotB = cB.localRotation;

        player.actionPoints -= 1;

        cA.SetSiblingIndex(idxB);
        cB.SetSiblingIndex(idxA);

        a.UpdateBoardContainerTarget(posB, rotB);
        b.UpdateBoardContainerTarget(posA, rotA);

        EventBus.Publish(GameEventType.Info, new EventContext
        {
            owner = player,
            opponent = ai,
            source = a.instance,
            target = b.instance,
            phase = $"[Swap] L{idxA + 1} <-> L{idxB + 1}"
        });

        UpdateAllViews();
        UpdateHUD();
    }


    void OnEndTurn()
    {
        if (matchEnded || !playerPhase) return;

        // 1. Caos roguelite: flip/swap casuale carte player
        RandomizePlayerBoard();

        // 2. Accumula charge per le carte rimaste in Retro
        AccumulateFlipCharges();

        // 3. Avanza il pattern di flip di tutti gli slot
        AdvanceSlotPatterns();

        // 4. Pubblica TurnEnd (abilità come OnEndTurnDealDamage reagiscono qui)
        EventBus.Publish(GameEventType.TurnEnd, new EventContext
        {
            owner    = player,
            opponent = ai,
            phase    = "TurnEnd"
        });

        // 5. Controlla fine partita
        if (IsGameOver() || currentTurn >= turns) { EndMatch(); return; }

        // 6. Avanza contatore turno
        currentTurn++;

        // 7. Roll slot nemici con animazione, poi avvia il nuovo turno
        if (slotBatchManager != null)
        {
            SetButtonsInteractable(false);
            int laneCount = playerBoardRoot.childCount;
            slotBatchManager.RollNewSlots(laneCount, chosenPrefabs =>
            {
                RespawnEnemySlotsFromList(chosenPrefabs);
                SetButtonsInteractable(true);
                StartTurn(player, ai, true);
            });
        }
        else
        {
            StartTurn(player, ai, true);
        }
    }

    void SetButtonsInteractable(bool on)
    {
        btnAttack.interactable  = on;
        btnEndTurn.interactable = on;
        if (handManager != null) handManager.btnDraw.interactable = on;
    }

    void RespawnEnemySlotsFromList(List<GameObject> prefabs)
    {
        // Rimuovi slot view esistenti
        for (int i = enemySlotViews.Count - 1; i >= 0; i--)
        {
            var sv = enemySlotViews[i];
            slotViewByInstance.Remove(sv.instance);
            Destroy(sv.gameObject);
        }
        enemySlotViews.Clear();

        // Pulisci eventuali placeholder rimasti (EmptySlot, _BatchReelBG, ecc.)
        var toKill = new List<GameObject>();
        foreach (Transform t in aiBoardRoot) toKill.Add(t.gameObject);
        foreach (var go in toKill) Destroy(go);

        int lanes = playerBoardRoot.childCount;
        for (int i = 0; i < lanes; i++)
        {
            if (i >= prefabs.Count || prefabs[i] == null)
            {
                if (EmptySlot != null)
                {
                    var empty = Instantiate(EmptySlot, aiBoardRoot);
                    empty.name  = EmptySlot.name;
                    empty.SetActive(true);
                }
                continue;
            }

            var prefab = prefabs[i];
            var sd     = prefab.GetComponent<SlotDefinition>();
            if (sd == null) continue;

            AddSlotFromTemplate(ai, sd.BuildSpec(), prefab, aiBoardRoot, enemySlotViews);
        }

        // Riordina i sibling index
        for (int i = 0; i < aiBoardRoot.childCount; i++)
            aiBoardRoot.GetChild(i).SetSiblingIndex(i);
    }



    void RemoveCard(PlayerState owner, CardInstance ci)
    {
        if (abilitiesByInstance.TryGetValue(ci, out var list))
        {
            for (int i = 0; i < list.Count; i++) list[i].Unbind();
            abilitiesByInstance.Remove(ci);
        }

        var view = viewByInstance[ci];

        var sel = SelectionManager.Instance;
        if (sel != null && sel.SelectedOwned == view) sel.SelectOwned(null);

        Transform parent = view.transform.parent;
        int laneIndex = view.transform.GetSiblingIndex();

        var boardContainer = view.PlayerBoardContainer;
        if (boardContainer != null && boardContainer.parent == playerBoardRoot)
        {
            parent = boardContainer.parent;
            laneIndex = boardContainer.GetSiblingIndex();
            Destroy(boardContainer.gameObject);
        }

        viewByInstance.Remove(ci);
        owner.board.Remove(ci);
        ci.Dispose();
        Destroy(view.gameObject);

        if (owner == player)
        {
            var spotGO = Instantiate(EmptySpot, parent);
            spotGO.name = EmptySpot.name;
            spotGO.SetActive(true);
            spotGO.transform.SetSiblingIndex(laneIndex);

            var outline = spotGO.GetComponent<Outline>();
            outline.enabled = false;
            outline.effectDistance = new Vector2(5f, 5f);
            outline.useGraphicAlpha = false;
            outline.effectColor = Color.white;

            var btn = spotGO.GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnEmptySpotClicked(spotGO.transform));
        }

        EventBus.Publish(GameEventType.Info, new EventContext
        {
            owner = owner,
            opponent = owner == player ? ai : player,
            source = ci,
            phase = "[GM] Removed destroyed card (replaced by EmptySpot if player)"
        });
    }


    void PlayCardFromHand(CardView handCard, Transform emptySpot)
    {
        var cd = handCard.GetComponentInParent<CardDefinition>();
        var parent = emptySpot.parent;
        int laneIndex = emptySpot.GetSiblingIndex();

        Destroy(emptySpot.gameObject);

        var spec = cd.BuildSpec();
        var ci = new CardInstance(spec, rng);
        ci.AssignGM(this);
        player.board.Add(ci);

        GameObject go = Instantiate(handCard.GetComponentInParent<CardDefinition>().gameObject, parent);
        go.name = handCard.GetComponentInParent<CardDefinition>().gameObject.name;
        go.SetActive(true);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;

        var view = go.GetComponentInChildren<CardView>();
        view.Init(this, player, ci);
        view.EnsurePlayerBoardContainer(playerBoardRoot);
        view.PlayerBoardContainer.SetSiblingIndex(laneIndex);

        viewByInstance[ci] = view;

        var opponent = ai;
        var abilities = go.GetComponents<AbilityBase>().ToList();
        foreach (var ab in abilities) ab.Bind(ci, player, opponent);
        abilitiesByInstance[ci] = abilities;

        handManager.OnHandCardDroppedToBoard(handCard);
        handManager.RemoveFromHand(handCard);

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

    

    void RemoveSlotView(SlotView v)
    {
        Transform parent = v.transform.parent;
        int laneIndex = v.transform.GetSiblingIndex();

        enemySlotViews.Remove(v);
        slotViewByInstance.Remove(v.instance);

        Destroy(v.gameObject);

        if (EmptySlot != null && parent != null)
        {
            var empty = Instantiate(EmptySlot, parent);
            empty.name = EmptySlot.name;
            empty.SetActive(true);
            empty.transform.SetSiblingIndex(laneIndex);
        }
    }


    public object GetOpponentObjInstance(object obj)
    {
        if (obj is CardInstance card)
        {
            var atkView = viewByInstance[card];
            Transform oppRoot = (atkView.owner == player) ? aiBoardRoot : playerBoardRoot;
            int lane = atkView.PlayerBoardContainer.transform.GetSiblingIndex();

            if (lane < 0 || lane >= oppRoot.childCount)
                return null;

            if (atkView.owner == player)
            {
                var sView = oppRoot.GetChild(lane).GetComponentInChildren<SlotView>(false);
                if (sView == null)
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
            if (cView == null)
                return null;

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

    // RIMOSSO: RandomizePlayerLayoutAndSides() distruggeva la strategia del player.
    // Il layout e i lati sono ora completamente sotto controllo del giocatore.

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

    public void OnCardClicked(CardView view)
    {
        if (matchEnded || view == null) return;

        bool isHandCard = view.owner == null && view.instance == null;
        if (isHandCard)
        {
            if (awaitingEndTurn || !playerPhase) { UpdateHUD(); return; }
            var emptySpot = SelectionManager.Instance != null ? SelectionManager.Instance.SelectedEmptySpot : null;
            if (emptySpot == null) return;

            PlayCardFromHand(view, emptySpot);
            SelectionManager.Instance?.SelectEmptySpot(null);
            return;
        }

        if (view.owner != player || view.instance == null) return;

        SelectionManager.Instance.SelectOwned(view);
    }



    public void OnEmptySpotClicked(Transform emptySpot)
    {
        if (matchEnded || emptySpot == null) return;

        if (SelectionManager.Instance != null)
            SelectionManager.Instance.SelectEmptySpot(emptySpot);
    }



}
