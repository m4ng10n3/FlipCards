using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    // ====== PREFAB BINDINGS ======
    [Serializable] public class PrefabCardBinding { public GameObject prefab; [Min(1)] public int count = 1; }
    [Serializable] public class PrefabSlotBinding { public GameObject prefab; [Min(1)] public int count = 1; }

    [Header("Roots")] public Transform playerBoardRoot; public Transform aiBoardRoot;
    [Header("UI")] public Button btnForceFlip; public Button btnAttack; public Button btnEndTurn;
    [Header("LOG")] public Text logText; static readonly StringBuilder _logBuf = new StringBuilder(4096);

    [Header("Match parameters")] public int turns = 10; public int playerBaseAP = 3; public int seed = 12345;
    [Header("Start constraints")][Min(1)] public int minCardsPerSide = 3;

    [Header("Prefab bindings")] public List<PrefabCardBinding> playerCards = new List<PrefabCardBinding>();
    [Header("Enemy Slots (bindings only)")] public List<PrefabSlotBinding> enemySlots = new List<PrefabSlotBinding>();

    bool awaitingEndTurn = false;
    static GameManager _instance; public static GameManager Instance => _instance;

    public bool TryGetView(CardInstance ci, out CardView v) => viewByInstance.TryGetValue(ci, out v);

    // ====== SIMPLE RULE ENGINE ======
    struct Rule { public GameEventType trigger; public Func<EventContext, bool> cond; public Action<EventContext> act; public bool enabled; }
    readonly List<Rule> _rules = new List<Rule>(); public bool enableDefaultRules = true;
    void AddRule(GameEventType t, Func<EventContext, bool> cond, Action<EventContext> act, bool enabled = true)
    {
        EventBus.Handler h = (evt, ctx) => { if (!enabled || evt != t) return; if (cond == null || cond(ctx)) act?.Invoke(ctx); };
        _rules.Add(new Rule { trigger = t, cond = cond, act = act, enabled = enabled }); EventBus.Subscribe(t, h);
    }

    // ====== RUNTIME ======
    System.Random rng; public PlayerState player; public PlayerState ai;
    int currentTurn = 1; bool playerPhase = true; bool matchEnded = false;

    readonly Dictionary<CardInstance, CardView> viewByInstance = new Dictionary<CardInstance, CardView>();
    readonly Dictionary<CardView, CardInstance> instanceByView = new Dictionary<CardView, CardInstance>();
    readonly List<CardView> playerViews = new List<CardView>();
    readonly Dictionary<CardInstance, List<AbilityBase>> abilitiesByInstance = new Dictionary<CardInstance, List<AbilityBase>>();

    // SLOTS (nemico)
    readonly Dictionary<SlotInstance, SlotView> slotViewByInstance = new Dictionary<SlotInstance, SlotView>();
    readonly Dictionary<SlotView, SlotInstance> slotInstanceByView = new Dictionary<SlotView, SlotInstance>();
    readonly List<SlotView> enemySlotViews = new List<SlotView>();

    void Awake()
    {
        Logger.SetSink(AppendLog);
        EventBus.Publish(GameEventType.Info, new EventContext { phase = "GameManager ready" });
        _instance = this;
        if (btnForceFlip) btnForceFlip.onClick.AddListener(OnForceFlip);
        if (btnAttack) btnAttack.onClick.AddListener(OnAttack);
        if (btnEndTurn) btnEndTurn.onClick.AddListener(OnEndTurn);
    }

    void Start()
    {
        if (SelectionManager.Instance == null) new GameObject("SelectionManager").AddComponent<SelectionManager>();
        if (!playerBoardRoot || !aiBoardRoot) { Logger.Error("Assign playerBoardRoot and aiBoardRoot in the Inspector."); enabled = false; return; }

        rng = new System.Random(seed);
        player = new PlayerState("Player", playerBaseAP);
        ai = new PlayerState("AI", 0);

        ClearChildrenUnder(playerBoardRoot);
        ClearSlotsRoot(); // pulisce anche tracking slot

        SpawnCardsFromBindings(player, playerCards, playerBoardRoot, playerViews);
        RebuildEnemySlotsToMatchPlayer(); // crea slot in base alle lane del player

        if (playerViews.Count < minCardsPerSide) { Logger.Info($"Not enough Player cards: {playerViews.Count} (min {minCardsPerSide})"); matchEnded = true; return; }

        EventBus.Publish(GameEventType.Info, new EventContext { phase = "=== MATCH START ===" });

        if (enableDefaultRules)
        {
            AddRule(GameEventType.TurnStart,
                ctx => ctx.owner == player && player.CountRetro(Faction.Ombra) >= 2,
                ctx => { var src = player.board.FirstOrDefault(c => c.alive && c.side == Side.Retro && c.def.faction == Faction.Ombra); if (src != null) src.DealDamageToPlayer(ctx.owner, ctx.opponent, 1, "Rule:Upkeep Ping"); });
        }

        UpdateAllViews(); UpdateHUD();
        StartTurn(player, ai, true);
    }

    // ====== BUILD (bindings ONLY) ======
    bool TryGetCardSpec(GameObject go, out CardDefinition.Spec spec) { spec = default; var cd = go ? go.GetComponent<CardDefinition>() : null; if (!cd) return false; spec = cd.BuildSpec(); return true; }
    bool TryGetSlotSpec(GameObject go, out SlotDefinition.Spec spec) { spec = default; var sd = go ? go.GetComponent<SlotDefinition>() : null; if (!sd) return false; spec = sd.BuildSpec(); return true; }

    void SpawnCardsFromBindings(PlayerState owner, List<PrefabCardBinding> bindings, Transform root, List<CardView> outViews)
    {
        if (bindings == null) return;
        foreach (var b in bindings)
        {
            if (b == null || b.count <= 0 || !b.prefab) { Logger.Warn("Invalid card binding."); continue; }
            if (!TryGetCardSpec(b.prefab, out var def)) { Logger.Error($"Card prefab '{b.prefab.name}' needs CardDefinition."); continue; }
            for (int i = 0; i < b.count; i++) AddCardFromTemplate(owner, def, b.prefab, root, outViews);
        }
    }

    void AddCardFromTemplate(PlayerState owner, CardDefinition.Spec def, GameObject prefab, Transform root, List<CardView> outViews)
    {
        var ci = new CardInstance(def, rng); owner.board.Add(ci);
        var go = Instantiate(prefab, root); go.name = prefab.name; go.SetActive(true);
        var view = go.GetComponent<CardView>(); if (!view) { Logger.Error("Card prefab has no CardView."); Destroy(go); return; }
        view.Init(this, owner, ci);
        viewByInstance[ci] = view; instanceByView[view] = ci; outViews.Add(view);

        var opponent = (owner == player) ? ai : player;
        var abilities = go.GetComponents<AbilityBase>()?.ToList() ?? new List<AbilityBase>();
        foreach (var ab in abilities) ab.Bind(ci, owner, opponent);
        abilitiesByInstance[ci] = abilities;

        EventBus.Publish(GameEventType.CardPlayed, new EventContext { owner = owner, opponent = opponent, source = ci, phase = "Main" });
    }

    void AddSlotFromTemplate(PlayerState owner, SlotDefinition.Spec def, GameObject prefab, Transform root, List<SlotView> outViews)
    {
        var si = new SlotInstance(def);
        var go = Instantiate(prefab, root); go.name = prefab.name; go.SetActive(true);
        var view = go.GetComponent<SlotView>(); if (!view) { Logger.Error("Slot prefab has no SlotView."); Destroy(go); return; }
        view.Init(this, owner, si);
        slotViewByInstance[si] = view; slotInstanceByView[view] = si; outViews.Add(view);

        var abilities = go.GetComponents<AbilityBase>();
        if (abilities != null) foreach (var ab in abilities) ab.Bind(null, ai, player);
    }

    // ====== REFRESH / HUD ======
    public void UpdateAllViews()
    {
        // snapshot per evitare 'Collection was modified'
        var cardViews = viewByInstance.Values.Where(v => v != null).ToArray();
        for (int i = 0; i < cardViews.Length; i++) cardViews[i].Refresh();

        // slot nemici: prune + refresh su snapshot
        var slots = enemySlotViews.ToArray();
        for (int i = 0; i < slots.Length; i++)
        {
            var v = slots[i];
            if (v == null || v.instance == null || !v.instance.alive) { SafeRemoveSlotView(v); continue; }
            v.Refresh();
        }
    }

    public void UpdateHUD()
    {
        if (matchEnded) return;
        if (playerPhase)
        {
            if (btnAttack) btnAttack.interactable = !awaitingEndTurn;
            if (btnForceFlip) btnForceFlip.interactable = !awaitingEndTurn && player.actionPoints > 0;
        }
        else
        {
            if (btnAttack) btnAttack.interactable = false;
            if (btnForceFlip) btnForceFlip.interactable = false;
        }
    }

    // ====== TURN FLOW / UI ACTIONS ======
    void StartTurn(PlayerState owner, PlayerState opponent, bool isPlayerPhase)
    {
        playerPhase = isPlayerPhase; awaitingEndTurn = false;
        owner.actionPoints = (owner == player) ? playerBaseAP : 0;

        EventBus.Publish(GameEventType.TurnStart, new EventContext { owner = owner, opponent = opponent, phase = "TurnStart" });
        if (playerPhase)
        {
            RandomizePlayerLayoutAndSides();
            RebuildEnemySlotsToMatchPlayer(); // mantiene 1:1 con le lane
        }
        else
        {
            ExecuteAiTurnStartActions();
        }
        UpdateAllViews(); UpdateHUD();
    }

    void EndTurnInternal(PlayerState owner, PlayerState opponent)
    {
        // Idempotente: se la partita è finita o già in transizione, esci
        if (matchEnded) return;

        EventBus.Publish(GameEventType.TurnEnd, new EventContext { owner = owner, opponent = opponent, phase = "TurnEnd" });

        // A fine turno IA: flip probabilistico del player e ricostruzione slot
        if (owner == ai)
        {
            var pCards = player.board.ToArray();
            for (int i = 0; i < pCards.Length; i++)
            {
                var ci = pCards[i]; if (ci == null || !ci.alive) continue;
                float p = Mathf.Clamp01(ci.def.endTurnFlipChance);
                if (rng.NextDouble() < p) { ci.Flip(); EventBus.Publish(GameEventType.Flip, new EventContext { owner = player, opponent = ai, source = ci, phase = "EndTurnFlip" }); }
            }

            // Ricostruisci SEMPRE gli slot in modo sicuro (stop a mutate-conflittuali)
            RebuildEnemySlotsToMatchPlayer();
            UpdateAllViews();
        }

        if (IsGameOver() || currentTurn >= turns) EndMatch();
    }

    void OnForceFlip()
    {
        if (awaitingEndTurn || matchEnded || !playerPhase) { UpdateHUD(); return; }
        if (player.actionPoints <= 0) { EventBus.Publish(GameEventType.Info, new EventContext { phase = "Not enough Player PA" }); UpdateHUD(); return; }

        var sel = SelectionManager.Instance.SelectedOwned?.instance; if (sel == null) return;
        sel.Flip(); player.actionPoints -= 1;
        EventBus.Publish(GameEventType.Flip, new EventContext { owner = player, opponent = ai, source = sel });
        UpdateAllViews(); UpdateHUD();
    }

    void OnAttack()
    {
        if (awaitingEndTurn || matchEnded || !playerPhase) { UpdateHUD(); return; }

        int lanes = Mathf.Min(playerBoardRoot.childCount, aiBoardRoot.childCount);
        for (int lane = 0; lane < lanes; lane++)
        {
            var pChild = playerBoardRoot.GetChild(lane);
            var pView = pChild ? pChild.GetComponentInChildren<CardView>(false) : null;
            var ci = pView ? pView.instance : null;
            if (ci == null || !ci.alive || ci.side != Side.Fronte) continue;

            var aChild = aiBoardRoot.GetChild(lane);
            var sView = aChild ? aChild.GetComponentInChildren<SlotView>(false) : null;
            var si = sView ? sView.instance : null;
            if (si == null || !si.alive) continue;

            ci.Attack(player, ai, si);
        }

        CleanupDestroyed(player); CleanupDestroyedSlots();
        UpdateAllViews();
        awaitingEndTurn = true; UpdateHUD();

        var sm = SelectionManager.Instance; if (sm != null) { sm.SelectOwned(null); sm.SelectEnemy(null); }
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

    // ====== AI ======
    void ExecuteAiTurnStartActions() { ApplyEnemySlotsEffectsLeftToRight(); }

    void ApplyEnemySlotsEffectsLeftToRight()
    {
        int lanes = Mathf.Min(aiBoardRoot.childCount, playerBoardRoot.childCount);
        // snapshot trasform per evitare problemi se eventi modificano la gerarchia
        var aiChildren = new Transform[lanes];
        for (int i = 0; i < lanes; i++) aiChildren[i] = aiBoardRoot.GetChild(i);

        for (int lane = 0; lane < lanes; lane++)
        {
            var ch = aiChildren[lane];
            var sView = ch ? ch.GetComponentInChildren<SlotView>(false) : null;
            if (sView == null || sView.instance == null || !sView.instance.alive) continue;

            EventBus.Publish(GameEventType.Info, new EventContext { phase = $"[SlotEffect] Lane {lane + 1}" });
            EventBus.Publish(GameEventType.Custom, new EventContext { owner = ai, opponent = player, source = sView.instance, phase = "SlotEffect" });
        }
    }

    // ====== CLEANUP ======
    void CleanupDestroyed(PlayerState p)
    {
        // snapshot per evitare enumerate su lista che cambia
        var dead = p.board.Where(ci => ci != null && !ci.alive).ToArray();
        for (int i = 0; i < dead.Length; i++) RemoveCard(p, dead[i]);
    }

    void RemoveCard(PlayerState owner, CardInstance ci)
    {
        if (ci == null) return;

        if (abilitiesByInstance.TryGetValue(ci, out var list) && list != null)
        {
            for (int i = 0; i < list.Count; i++) { var ab = list[i]; if (ab != null) ab.Unbind(); }
            abilitiesByInstance.Remove(ci);
        }

        if (viewByInstance.TryGetValue(ci, out var view) && view != null)
        {
            var sel = SelectionManager.Instance;
            if (sel != null)
            {
                if (sel.SelectedOwned == view) sel.SelectOwned(null);
                if (sel.SelectedEnemy == view) sel.SelectEnemy(null);
            }
            instanceByView.Remove(view); viewByInstance.Remove(ci); Destroy(view.gameObject);
        }

        owner.board.Remove(ci);
        EventBus.Publish(GameEventType.Info, new EventContext { owner = owner, opponent = (owner == player) ? ai : player, source = ci, phase = "[GM] Removed destroyed card" });
    }

    void CleanupDestroyedSlots()
    {
        var toRemove = enemySlotViews.Where(v => v == null || v.instance == null || !v.instance.alive).ToArray();
        for (int i = 0; i < toRemove.Length; i++) SafeRemoveSlotView(toRemove[i]);
    }

    // ====== SLOTS REBUILD (centrale e sicuro) ======
    void RebuildEnemySlotsToMatchPlayer()
    {
        // 1) pulisci tracking + GameObject esistenti sotto aiBoardRoot
        ClearSlotsRoot();

        // 2) crea lista piatta di prefabs; se vuota, esci
        var flat = new List<GameObject>();
        for (int i = 0; i < enemySlots.Count; i++)
        {
            var b = enemySlots[i]; if (b == null || b.count <= 0 || !b.prefab) continue;
            for (int k = 0; k < b.count; k++) flat.Add(b.prefab);
        }
        if (flat.Count == 0) return;

        // 3) mescola (Fisher–Yates)
        for (int i = flat.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (flat[i], flat[j]) = (flat[j], flat[i]); }

        // 4) riempi tante lane quanti sono i figli del player
        int lanes = playerBoardRoot ? playerBoardRoot.childCount : 0;
        for (int i = 0; i < lanes; i++)
        {
            var prefab = flat[i % flat.Count];
            if (!TryGetSlotSpec(prefab, out var spec)) continue;
            AddSlotFromTemplate(ai, spec, prefab, aiBoardRoot, enemySlotViews);
        }

        // 5) allinea indici
        for (int i = 0; i < aiBoardRoot.childCount; i++) aiBoardRoot.GetChild(i).SetSiblingIndex(i);
    }

    void ClearSlotsRoot()
    {
        // snapshot figli
        var toKill = new List<GameObject>();
        foreach (Transform t in aiBoardRoot) toKill.Add(t.gameObject);

        // pulisci tracking
        for (int i = enemySlotViews.Count - 1; i >= 0; i--)
        {
            var sv = enemySlotViews[i];
            if (sv != null && slotInstanceByView.TryGetValue(sv, out var si)) { slotInstanceByView.Remove(sv); if (si != null) slotViewByInstance.Remove(si); }
        }
        enemySlotViews.Clear();

        // distruggi go (Destroy è deferred ma non incrociamo enumerazioni)
        for (int i = 0; i < toKill.Count; i++) Destroy(toKill[i]);
    }

    void SafeRemoveSlotView(SlotView v)
    {
        if (v == null) return;
        if (slotInstanceByView.TryGetValue(v, out var si)) { slotInstanceByView.Remove(v); if (si != null) slotViewByInstance.Remove(si); }
        enemySlotViews.Remove(v);
        Destroy(v.gameObject);
    }

    // ====== Utility ======
    public CardInstance GetOpposingCardInstance(CardInstance attacker)
    {
        if (attacker == null) return null;
        if (!TryGetView(attacker, out var atkView) || atkView == null) return null;

        Transform myRoot = (atkView.owner == player) ? playerBoardRoot : aiBoardRoot;
        Transform oppRoot = (atkView.owner == player) ? aiBoardRoot : playerBoardRoot;
        if (!myRoot || !oppRoot) return null;

        int lane = atkView.transform.GetSiblingIndex();
        if (lane < 0 || lane >= oppRoot.childCount) return null;

        var oppChild = oppRoot.GetChild(lane);
        var oppView = oppChild ? oppChild.GetComponentInChildren<CardView>(false) : null;
        if (!oppView) return null;

        if (!instanceByView.TryGetValue(oppView, out var target)) return null;
        return (target != null && target.alive) ? target : null;
    }

    void RandomizePlayerLayoutAndSides()
    {
        // Shuffle visuale dei figli
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
            var ci = cards[i]; if (ci == null || !ci.alive) continue;
            var newSide = (rng.NextDouble() < 0.5) ? Side.Fronte : Side.Retro;
            if (ci.side != newSide) { ci.Flip(); EventBus.Publish(GameEventType.Flip, new EventContext { owner = player, opponent = ai, source = ci, phase = "TurnStartRandomize" }); }
        }
    }

    void ClearChildrenUnder(Transform root)
    {
        if (!root) return;
        var toKill = new List<GameObject>();
        foreach (Transform t in root) toKill.Add(t.gameObject);
        for (int i = 0; i < toKill.Count; i++) Destroy(toKill[i]);
    }

    public void AppendLog(string msg) { _logBuf.AppendLine(msg); if (logText) logText.text = _logBuf.ToString(); }
    public void ClearLog() { _logBuf.Clear(); if (logText) logText.text = ""; }

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
        if (matchEnded || view == null) return;
        bool isPlayers = view.owner == player;
        if (isPlayers) SelectionManager.Instance.SelectOwned(view);
        else SelectionManager.Instance.SelectEnemy(view);
    }
}
