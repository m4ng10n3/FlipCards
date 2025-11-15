using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.VisualScripting;
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
    [Header("Start constraints")][Min(1)] public int CardsPerSide = 3;

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
        ClearSlotsRoot();

        SpawnCardsFromBindings(player, playerCards, playerBoardRoot, playerViews);
        RebuildEnemySlotsToMatchPlayer();

        if (playerViews.Count < CardsPerSide) { matchEnded = true; return; }

        EventBus.Publish(GameEventType.Info, new EventContext { phase = "=== MATCH START ===" });

        UpdateAllViews(); UpdateHUD();
        StartTurn(player, ai, true);
    }

    // === BUILD ===
    void SpawnCardsFromBindings(PlayerState owner, List<PrefabCardBinding> bindings, Transform root, List<CardView> outViews)
    {
        foreach (var b in bindings)
        {
            for (int i = 0; i < b.count; i++)
            {
                // Per il PLAYER fissiamo il numero di carte iniziali sul tabellone
                // Non ne istanziamo più di CardsPerSide
                if (owner == player && outViews.Count >= CardsPerSide)
                    return;

                var cd = b.prefab.GetComponent<CardDefinition>();
                var def = cd.BuildSpec();
                AddCardFromTemplate(owner, def, b.prefab, root, outViews);
            }
        }
    }


    void AddCardFromTemplate(PlayerState owner, CardDefinition.Spec def, GameObject prefab, Transform root, List<CardView> outViews)
    {
        var ci = new CardInstance(def, rng); owner.board.Add(ci); ci.AssignGM(GameManager.Instance);
        var go = Instantiate(prefab, root); go.name = prefab.name; go.SetActive(true);

        var view = go.GetComponent<CardView>();
        view.Init(this, owner, ci);
        viewByInstance[ci] = view; outViews.Add(view);

        var opponent = (owner == player) ? ai : player;
        var abilities = go.GetComponents<AbilityBase>().ToList();
        foreach (var ab in abilities) ab.Bind(ci, owner, opponent);
        abilitiesByInstance[ci] = abilities;

        EventBus.Publish(GameEventType.CardPlayed, new EventContext { owner = owner, opponent = opponent, source = ci, phase = "Main" });
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

    // === REFRESH / HUD ===
    public void UpdateAllViews()
    {
        foreach (var v in viewByInstance.Values) v.Refresh();

        // slot: se morto, rimuovi subito
        for (int i = enemySlotViews.Count - 1; i >= 0; i--)
        {
            var v = enemySlotViews[i];
            if (!v.instance.alive) { RemoveSlotView(v); continue; }
            v.Refresh();
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

        var sel = SelectionManager.Instance.SelectedOwned.instance;
        sel.Flip(); player.actionPoints -= 1;
        EventBus.Publish(GameEventType.Flip, new EventContext { owner = player, opponent = ai, source = sel });
        UpdateAllViews(); UpdateHUD();
    }

    void OnAttack()
    {
        if (awaitingEndTurn || matchEnded || !playerPhase) { UpdateHUD(); return; }

        // Il numero di lane è guidato dal numero di carte del player
        int lanes = playerBoardRoot.childCount;
        for (int lane = 0; lane < lanes; lane++)
        {
            var pView = playerBoardRoot.GetChild(lane).GetComponentInChildren<CardView>(false);
            if (pView == null) continue;

            var ci = pView.instance;
            if (!ci.alive || ci.side != Side.Fronte) continue;

            // Recupero lo slot nemico SOLO se esiste quel child
            SlotView sView = null;
            if (lane < aiBoardRoot.childCount)
                sView = aiBoardRoot.GetChild(lane).GetComponentInChildren<SlotView>(false);

            // Se non c'è uno slot attivo davanti (slot distrutto o proprio nessuno) -> danno diretto agli HP nemico
            if (sView == null || !sView.instance.alive)
            {
                int dmg = ci.ComputeFrontDamage(player);
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

        CleanupDestroyed(player); 
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
            if (!sView.instance.alive) continue;

            EventBus.Publish(GameEventType.Info, new EventContext { phase = $"[SlotEffect] Lane {lane + 1}" });
            EventBus.Publish(GameEventType.Custom, new EventContext { owner = ai, opponent = player, source = sView.instance, phase = "SlotEffect" });
        }
    }

    // === CLEANUP ===
    void CleanupDestroyed(PlayerState p)
    {
        var dead = p.board.Where(ci => !ci.alive).ToArray();
        for (int i = 0; i < dead.Length; i++) RemoveCard(p, dead[i]);
    }

    void RemoveCard(PlayerState owner, CardInstance ci)
    {
        var list = abilitiesByInstance[ci];
        for (int i = 0; i < list.Count; i++) list[i].Unbind();
        abilitiesByInstance.Remove(ci);

        var view = viewByInstance[ci];
        var sel = SelectionManager.Instance;
        if (sel.SelectedOwned == view) sel.SelectOwned(null);

        viewByInstance.Remove(ci); Destroy(view.gameObject);
        owner.board.Remove(ci);

        EventBus.Publish(GameEventType.Info, new EventContext { owner = owner, opponent = (owner == player) ? ai : player, source = ci, phase = "[GM] Removed destroyed card" });
    }

    void CleanupDestroyedSlots()
    {
        for (int i = enemySlotViews.Count - 1; i >= 0; i--)
            if (!enemySlotViews[i].instance.alive) RemoveSlotView(enemySlotViews[i]);
    }

    // === SLOTS REBUILD ===
    void RebuildEnemySlotsToMatchPlayer()
    {
        ClearSlotsRoot();

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

    void ClearSlotsRoot()
    {
        enemySlotViews.Clear();
        var toKill = new List<GameObject>();
        foreach (Transform t in aiBoardRoot) toKill.Add(t.gameObject);
        for (int i = 0; i < toKill.Count; i++) Destroy(toKill[i]);
        slotViewByInstance.Clear();
    }

    void RemoveSlotView(SlotView v)
    {
        enemySlotViews.Remove(v);
        Destroy(v.gameObject);
        slotViewByInstance.Remove(v.instance);
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
        bool isPlayers = view.owner == player;
        if (isPlayers) SelectionManager.Instance.SelectOwned(view);
    }
}
