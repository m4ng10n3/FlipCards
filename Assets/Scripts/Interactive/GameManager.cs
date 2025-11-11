using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    // ====== PREFAB BINDINGS ======
    [System.Serializable]
    public class PrefabCardBinding
    {
        [Tooltip("Card prefab (must have CardView + CardDefinition)")]
        public GameObject prefab;

        [Min(1), Tooltip("How many copies of this prefab to spawn")]
        public int count = 1;
    }

    [System.Serializable]
    public class PrefabSlotBinding
    {
        [Tooltip("Slot prefab (must have SlotView + SlotDefinition)")]
        public GameObject prefab;

        [Min(1), Tooltip("How many copies of this prefab to spawn")]
        public int count = 1;
    }

    [Header("Roots")]
    public Transform playerBoardRoot;
    public Transform aiBoardRoot;

    [Header("UI")]
    public Button btnForceFlip;
    public Button btnAttack;
    public Button btnEndTurn;

    [Header("LOG")]
    public Text logText;
    private static readonly StringBuilder _logBuf = new StringBuilder(4096);

    [Header("Match parameters")]
    public int turns = 10;
    public int playerBaseAP = 3;
    public int seed = 12345;

    [Header("Start constraints")]
    [Min(1)] public int minCardsPerSide = 3;

    [Header("Prefab bindings")]
    public List<PrefabCardBinding> playerCards = new List<PrefabCardBinding>();

    [Header("Enemy Slots (bindings only)")]
    public List<PrefabSlotBinding> enemySlots = new List<PrefabSlotBinding>();

    bool awaitingEndTurn = false; // se true, l’utente deve premere End Turn; nessuna altra azione permessa

    static GameManager _instance;
    public static GameManager Instance => _instance;

    public bool TryGetView(CardInstance ci, out CardView v) => viewByInstance.TryGetValue(ci, out v);

    // ====== SIMPLE RULE ENGINE ======
    struct Rule
    {
        public GameEventType trigger;
        public Func<EventContext, bool> cond;
        public Action<EventContext> act;
        public bool enabled;
    }

    readonly List<Rule> _rules = new List<Rule>();
    public bool enableDefaultRules = true;

    void AddRule(GameEventType t, Func<EventContext, bool> cond, Action<EventContext> act, bool enabled = true)
    {
        EventBus.Handler h = (evt, ctx) =>
        {
            if (!enabled || evt != t) return;
            if (cond == null || cond(ctx)) act?.Invoke(ctx);
        };
        _rules.Add(new Rule { trigger = t, cond = cond, act = act, enabled = enabled });
        EventBus.Subscribe(t, h);
    }

    void ClearRules() => _rules.Clear();

    // ====== RUNTIME ======
    System.Random rng;
    public PlayerState player;
    public PlayerState ai;
    int currentTurn = 1;
    bool playerPhase = true;
    bool matchEnded = false;

    readonly Dictionary<CardInstance, CardView> viewByInstance = new Dictionary<CardInstance, CardView>();
    readonly Dictionary<CardView, CardInstance> instanceByView = new Dictionary<CardView, CardInstance>();

    readonly List<CardView> playerViews = new List<CardView>();

    // Abilità per unbind sicuro
    readonly Dictionary<CardInstance, List<AbilityBase>> abilitiesByInstance = new Dictionary<CardInstance, List<AbilityBase>>();

    // ====== SLOTS (nemico) ======
    readonly Dictionary<SlotInstance, SlotView> slotViewByInstance = new Dictionary<SlotInstance, SlotView>();
    readonly Dictionary<SlotView, SlotInstance> slotInstanceByView = new Dictionary<SlotView, SlotInstance>();
    readonly List<SlotView> enemySlotViews = new List<SlotView>();

    void Awake()
    {
        Logger.SetSink(AppendLog);
        EventBus.Publish(GameEventType.Info, new EventContext { phase = "GameManager ready" });
        _instance = this;

        // Wiring bottoni UI
        if (btnForceFlip) btnForceFlip.onClick.AddListener(OnForceFlip);
        if (btnAttack) btnAttack.onClick.AddListener(OnAttack);
        if (btnEndTurn) btnEndTurn.onClick.AddListener(OnEndTurn);
    }

    void Start()
    {
        // Ensure SelectionManager singleton (per flip manuale)
        if (SelectionManager.Instance == null)
            new GameObject("SelectionManager").AddComponent<SelectionManager>();

        // Basic validation
        if (playerBoardRoot == null || aiBoardRoot == null)
        {
            Logger.Error("Assign playerBoardRoot and aiBoardRoot in the Inspector.");
            enabled = false;
            return;
        }

        rng = new System.Random(seed);
        player = new PlayerState("Player", playerBaseAP);
        ai = new PlayerState("AI", 0); // IA senza PA

        // Pulisci sempre le root: bindings sono l'unica verità
        ClearChildrenUnder(playerBoardRoot);
        ClearChildrenUnder(aiBoardRoot);

        // 1) Costruisci il lato Player (SOLO dai bindings)
        SpawnCardsFromBindings(player, playerCards, playerBoardRoot, playerViews);

        // 2) Costruisci gli SLOT del nemico (SOLO dai bindings)
        SpawnSlotsFromBindings(ai, enemySlots, aiBoardRoot, enemySlotViews);

        // 3) Allinea il numero di slot alle lane del player
        EnsureSlotsMatchPlayerLanes();

        // 4) Minimo carte Player
        if (playerViews.Count < minCardsPerSide)
        {
            Logger.Info($"Not enough Player cards to start. Player:{playerViews.Count} (min {minCardsPerSide})");
            matchEnded = true;
            return;
        }

        EventBus.Publish(GameEventType.Info, new EventContext { phase = "=== MATCH START ===" });

        UpdateAllViews();
        UpdateHUD();

        // 5) Regole di default (opzionali)
        if (enableDefaultRules)
        {
            // Esempio: upkeep ping se Player ha >=2 Retro Ombra
            AddRule(GameEventType.TurnStart,
                ctx => ctx.owner == player && player.CountRetro(Faction.Ombra) >= 2,
                ctx =>
                {
                    var src = player.board.FirstOrDefault(c => c.alive && c.side == Side.Retro && c.def.faction == Faction.Ombra);
                    if (src != null) src.DealDamageToPlayer(ctx.owner, ctx.opponent, 1, "Rule:Upkeep Ping");
                });
        }

        // 6) Avvia turno Player
        StartTurn(player, ai, true);
    }

    // ====== BUILD (bindings ONLY) ======
    bool TryGetCardSpec(GameObject go, out CardDefinition.Spec spec)
    {
        spec = default;
        if (go == null) return false;
        var inline = go.GetComponent<CardDefinition>();
        if (inline == null) return false;
        spec = inline.BuildSpec();
        return true;
    }

    bool TryGetSlotSpec(GameObject go, out SlotDefinition.Spec spec)
    {
        spec = default;
        if (go == null) return false;
        var inline = go.GetComponent<SlotDefinition>();
        if (inline == null) return false;
        spec = inline.BuildSpec();
        return true;
    }

    void SpawnCardsFromBindings(PlayerState owner, List<PrefabCardBinding> bindings, Transform root, List<CardView> outViews)
    {
        if (bindings == null) return;

        foreach (var b in bindings)
        {
            if (b == null || b.count <= 0 || b.prefab == null)
            {
                Logger.Warn("Invalid binding: assign Prefab and Count >= 1.");
                continue;
            }
            if (!TryGetCardSpec(b.prefab, out var def))
            {
                Logger.Error("Prefab '" + b.prefab.name + "' must have CardDefinition.");
                continue;
            }
            for (int i = 0; i < b.count; i++)
                AddCardFromTemplate(owner, def, b.prefab, root, outViews);
        }
    }

    void AddCardFromTemplate(PlayerState owner, CardDefinition.Spec def, GameObject prefab, Transform root, List<CardView> outViews)
    {
        var ci = new CardInstance(def, rng);
        owner.board.Add(ci);

        var go = Instantiate(prefab, root);
        go.name = prefab.name;
        go.SetActive(true);

        var view = go.GetComponent<CardView>();
        if (view == null) { Logger.Error("Card prefab has no CardView."); Destroy(go); return; }

        view.Init(this, owner, ci);
        viewByInstance[ci] = view;
        instanceByView[view] = ci;
        outViews.Add(view);

        // Bind automatico abilità sul prefab
        var opponent = ai;
        var abilities = go.GetComponents<AbilityBase>()?.ToList() ?? new List<AbilityBase>();
        foreach (var ab in abilities) ab.Bind(ci, owner, opponent);
        abilitiesByInstance[ci] = abilities;

        // Evento carta giocata
        EventBus.Publish(GameEventType.CardPlayed, new EventContext { owner = owner, opponent = opponent, source = ci, phase = "Main" });
    }

    void SpawnSlotsFromBindings(PlayerState owner, List<PrefabSlotBinding> bindings, Transform root, List<SlotView> outViews)
    {
        if (bindings == null) return;

        foreach (var b in bindings)
        {
            if (b == null || b.count <= 0 || b.prefab == null)
            {
                Logger.Warn("Invalid slot binding: assign Prefab and Count >= 1.");
                continue;
            }
            if (!TryGetSlotSpec(b.prefab, out var def))
            {
                Logger.Error("Slot prefab '" + b.prefab.name + "' must have SlotDefinition.");
                continue;
            }
            for (int i = 0; i < b.count; i++)
                AddSlotFromTemplate(owner, def, b.prefab, root, outViews);
        }
    }

    void AddSlotFromTemplate(PlayerState owner, SlotDefinition.Spec def, GameObject prefab, Transform root, List<SlotView> outViews)
    {
        var si = new SlotInstance(def);

        var go = Instantiate(prefab, root);
        go.name = prefab.name;
        go.SetActive(true);

        var view = go.GetComponent<SlotView>();
        if (view == null) { Logger.Error("Slot prefab has no SlotView."); Destroy(go); return; }

        view.Init(this, owner, si);
        slotViewByInstance[si] = view;
        slotInstanceByView[view] = si;
        outViews.Add(view);

        // Abilità su prefab (se presenti; source null per slot)
        var abilities = go.GetComponents<AbilityBase>()?.ToList();
        if (abilities != null)
            foreach (var ab in abilities) ab.Bind(null, ai, player);
    }

    // Assicura che il numero degli slot corrisponda al numero delle lane del player
    void EnsureSlotsMatchPlayerLanes()
    {
        int lanes = playerBoardRoot ? playerBoardRoot.childCount : 0;
        if (lanes <= 0 || aiBoardRoot == null) return;

        // Troppi slot? taglia
        while (aiBoardRoot.childCount > lanes)
        {
            var last = aiBoardRoot.GetChild(aiBoardRoot.childCount - 1);
            var sv = last.GetComponentInChildren<SlotView>(false);
            if (sv != null)
            {
                enemySlotViews.Remove(sv);
                if (slotInstanceByView.TryGetValue(sv, out var si))
                {
                    slotInstanceByView.Remove(sv);
                    slotViewByInstance.Remove(si);
                }
            }
            Destroy(last.gameObject);
        }

        // Pochi slot? duplica ciclicamente dai binding per riempire
        if (aiBoardRoot.childCount < lanes)
        {
            var flat = new List<GameObject>();
            foreach (var b in enemySlots)
                for (int i = 0; i < Mathf.Max(0, b.count); i++)
                    if (b.prefab != null) flat.Add(b.prefab);

            if (flat.Count == 0) return; // nulla da cui pescare

            while (aiBoardRoot.childCount < lanes)
            {
                var prefab = flat[(aiBoardRoot.childCount) % flat.Count];
                if (!TryGetSlotSpec(prefab, out var spec)) { break; }
                AddSlotFromTemplate(ai, spec, prefab, aiBoardRoot, enemySlotViews);
            }
        }

        // Allinea gli indici
        for (int i = 0; i < aiBoardRoot.childCount; i++)
            aiBoardRoot.GetChild(i).SetSiblingIndex(i);
    }

    // ====== REFRESH / HUD ======
    public void UpdateAllViews()
    {
        foreach (var kv in viewByInstance)
            kv.Value.Refresh();

        foreach (var v in enemySlotViews)
            if (v != null) v.Refresh();
    }

    public void UpdateHUD()
    {
        if (matchEnded) return;

        if (playerPhase)
        {
            // Durante il turno del player:
            // - se awaitingEndTurn == true => hai già attaccato -> niente altre azioni; solo EndTurn
            if (btnAttack) btnAttack.interactable = !awaitingEndTurn;
            if (btnForceFlip) btnForceFlip.interactable = !awaitingEndTurn && player.actionPoints > 0;
        }
        else
        {
            // Durante il turno IA: nessuna azione disponibile, ma si può SEMPRE chiudere il turno
            if (btnAttack) btnAttack.interactable = false;
            if (btnForceFlip) btnForceFlip.interactable = false;
        }

    }

    // ====== TURN FLOW / UI ACTIONS ======
    void StartTurn(PlayerState owner, PlayerState opponent, bool isPlayerPhase)
    {
        playerPhase = isPlayerPhase;

        // AZZERA il lock a inizio turno (sarà rimesso a true dopo l’attacco)
        awaitingEndTurn = false;

        // Reset AP
        owner.actionPoints = (owner == player) ? playerBaseAP : 0;

        EventBus.Publish(GameEventType.TurnStart, new EventContext { owner = owner, opponent = opponent, phase = "TurnStart" });
        UpdateHUD();

        if (playerPhase)
        {
            // Randomizza layout/verso carte; gli slot restano quelli da bindings
            RandomizePlayerLayoutAndSides();
            EnsureSlotsMatchPlayerLanes();
            UpdateAllViews();
            return; // attende input (Flip / Attack / EndTurn)
        }
        else
        {
            // Turno IA: esegue SUBITO le sue azioni automatiche (per vedere gli hint)
            ExecuteAiTurnStartActions();
            UpdateAllViews();
            UpdateHUD();
            return;
        }

    }

    void EndTurnInternal(PlayerState owner, PlayerState opponent)
    {
        EventBus.Publish(GameEventType.TurnEnd, new EventContext { owner = owner, opponent = opponent, phase = "TurnEnd" });

        // A fine turno IA: flip probabilistico del player (gli slot NON vengono ricreati)
        if (owner == ai)
        {
            foreach (var ci in player.board.Where(c => c.alive))
            {
                float p = Mathf.Clamp01(ci.def.endTurnFlipChance);
                if (rng.NextDouble() < p)
                {
                    ci.Flip();
                    EventBus.Publish(GameEventType.Flip, new EventContext { owner = player, opponent = ai, source = ci, phase = "EndTurnFlip" });
                }
            }

            // 1) distruggi gli slot attuali e pulisci tracking
            while (aiBoardRoot.childCount > 0)
            {
                var child = aiBoardRoot.GetChild(aiBoardRoot.childCount - 1);
                var sv = child.GetComponentInChildren<SlotView>(false);
                if (sv != null)
                {
                    enemySlotViews.Remove(sv);
                    if (slotInstanceByView.TryGetValue(sv, out var si))
                    {
                        slotInstanceByView.Remove(sv);
                        slotViewByInstance.Remove(si);
                    }
                }
                Destroy(child.gameObject);
            }

            // 2) crea una lista "flat" di prefabs dagli enemySlots e **mescola**
            var flat = new List<GameObject>();
            foreach (var b in enemySlots)
                for (int i = 0; i < Mathf.Max(0, b.count); i++)
                    if (b.prefab != null) flat.Add(b.prefab);

            // se non hai binding niente da fare
            if (flat.Count > 0)
            {
                // Fisher–Yates
                for (int i = flat.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (flat[i], flat[j]) = (flat[j], flat[i]);
                }

                // 3) riempi le lane del player scegliendo dalla lista mescolata (ciclica)
                int lanes = playerBoardRoot.childCount;
                for (int i = 0; i < lanes; i++)
                {
                    var prefab = flat[i % flat.Count];
                    if (!TryGetSlotSpec(prefab, out var spec)) continue;
                    AddSlotFromTemplate(ai, spec, prefab, aiBoardRoot, enemySlotViews);
                }
            }

            UpdateAllViews();
        }

        if (IsGameOver() || currentTurn >= turns) EndMatch();
    }

    void OnForceFlip()
    {
        if (awaitingEndTurn) { UpdateHUD(); return; } // già attaccato: solo End Turn

        if (matchEnded || !playerPhase) return;
        if (player.actionPoints <= 0) { EventBus.Publish(GameEventType.Info, new EventContext { phase = "Not enough Player PA" }); UpdateHUD(); return; }

        var sel = SelectionManager.Instance.SelectedOwned?.instance;
        if (sel == null) return;

        sel.Flip();
        player.actionPoints -= 1;

        EventBus.Publish(GameEventType.Flip, new EventContext { owner = player, opponent = ai, source = sel });
        UpdateAllViews();
        UpdateHUD();
    }

    void OnAttack()
    {
        if (awaitingEndTurn) { UpdateHUD(); return; } // già attaccato in questo turno

        if (matchEnded || !playerPhase) return;

        // Attacco di massa: tutte le carte Player in Fronte colpiscono lo slot opposto
        int lanes = playerBoardRoot.childCount;
        for (int lane = 0; lane < lanes; lane++)
        {
            // CardView nella lane (lato Player)
            Transform pChild = playerBoardRoot.GetChild(lane);
            var pView = pChild ? pChild.GetComponentInChildren<CardView>(includeInactive: false) : null;
            var ci = pView ? pView.instance : null;

            if (ci == null || !ci.alive || ci.side != Side.Fronte) continue;

            // SlotView opposto (lato AI)
            if (lane >= aiBoardRoot.childCount) break;
            Transform aChild = aiBoardRoot.GetChild(lane);
            var sView = aChild ? aChild.GetComponentInChildren<SlotView>(includeInactive: false) : null;
            var si = sView ? sView.instance : null;

            if (si == null || !si.alive) continue;

            ci.Attack(player, ai, si);
        }

        // Pulizia, refresh
        CleanupDestroyed(player);
        CleanupDestroyedSlots();
        UpdateAllViews();
        UpdateHUD();

        awaitingEndTurn = true;
        UpdateHUD();

        // (opzionale) Pulisci eventuali selezioni per evitare confusioni UI
        var sel = SelectionManager.Instance;
        if (sel != null) { sel.SelectOwned(null); sel.SelectEnemy(null); }

    }

    void OnEndTurn()
    {
        if (matchEnded) return;

        if (playerPhase)
        {
            EndTurnInternal(player, ai);
            if (!matchEnded)
                StartTurn(ai, player, false);
        }
        else
        {
            EndTurnInternal(ai, player);
            if (!matchEnded)
            {
                currentTurn++;
                StartTurn(player, ai, true);
            }
        }
    }

    void ApplyEnemySlotsEffectsLeftToRight()
    {
        int lanes = Mathf.Min(aiBoardRoot.childCount, playerBoardRoot.childCount);
        for (int lane = 0; lane < lanes; lane++)
        {
            Transform ch = aiBoardRoot.GetChild(lane);
            var sView = ch ? ch.GetComponentInChildren<SlotView>(false) : null;
            if (sView == null || sView.instance == null || !sView.instance.alive) continue;

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

    void CleanupDestroyed(PlayerState p)
    {
        var dead = p.board.Where(ci => ci != null && !ci.alive).ToList();
        foreach (var ci in dead)
            RemoveCard(p, ci);
    }

    void RemoveCard(PlayerState owner, CardInstance ci)
    {
        if (ci == null) return;

        if (abilitiesByInstance.TryGetValue(ci, out var list) && list != null)
        {
            foreach (var ab in list) { if (ab != null) ab.Unbind(); }
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

            instanceByView.Remove(view);
            viewByInstance.Remove(ci);
            Destroy(view.gameObject);
        }

        owner.board.Remove(ci);

        EventBus.Publish(GameEventType.Info, new EventContext
        {
            owner = owner,
            opponent = (owner == player) ? ai : player,
            source = ci,
            phase = "[GM] Removed destroyed card"
        });
    }

    public void AppendLog(string msg)
    {
        _logBuf.AppendLine(msg);
        if (logText != null) logText.text = _logBuf.ToString();
    }

    public void ClearLog()
    {
        _logBuf.Clear();
        if (logText) logText.text = "";
    }

    bool IsGameOver() => player.hp <= 0 || ai.hp <= 0;

    void EndMatch()
    {
        if (matchEnded) return;
        matchEnded = true;

        int diff = ai.hp - player.hp;
        string result = diff > 0 ? "AI AHEAD" : diff < 0 ? "PLAYER AHEAD" : "TIE";
        EventBus.Publish(GameEventType.Info, new EventContext { phase = "=== MATCH END ===" });
        EventBus.Publish(GameEventType.Info, new EventContext { phase = $"Score: PlayerHP {player.hp} vs AIHP {ai.hp} | Diff (AI-Player) = {diff} -> {result}" });
    }

    // ====== CLICK DELLE CARTE ======
    public void OnCardClicked(CardView view)
    {
        if (matchEnded) return;
        bool isPlayers = view.owner == player;
        if (isPlayers) SelectionManager.Instance.SelectOwned(view);
        else SelectionManager.Instance.SelectEnemy(view);
    }

    // ====== Utility ======
    public CardInstance GetOpposingCardInstance(CardInstance attacker)
    {
        if (attacker == null) return null;
        if (!TryGetView(attacker, out var atkView) || atkView == null) return null;

        // Root del lato attaccante e del lato opposto
        Transform myRoot = (atkView.owner == player) ? playerBoardRoot : aiBoardRoot;
        Transform oppRoot = (atkView.owner == player) ? aiBoardRoot : playerBoardRoot;
        if (myRoot == null || oppRoot == null) return null;

        int lane = atkView.transform.GetSiblingIndex();
        if (lane < 0 || lane >= oppRoot.childCount) return null;

        // Cerca una CardView nella stessa lane del lato opposto
        Transform oppChild = oppRoot.GetChild(lane);
        CardView oppView = oppChild ? oppChild.GetComponentInChildren<CardView>(includeInactive: false) : null;
        if (oppView == null) return null;

        if (!instanceByView.TryGetValue(oppView, out var target)) return null;
        if (target == null || !target.alive) return null;

        return target;
    }

    // Esegui le azioni automatiche dell'IA all'inizio del suo turno (debug friendly)
    // NOTA: non chiude il turno e non passa la mano; serve solo a generare gli hint visibili.
    void ExecuteAiTurnStartActions()
    {

        ApplyEnemySlotsEffectsLeftToRight();

        UpdateAllViews();
        UpdateHUD();
    }


    void RandomizePlayerLayoutAndSides()
    {
        // Random order (Fisher–Yates sugli indici visuali)
        var children = new List<Transform>();
        foreach (Transform t in playerBoardRoot) children.Add(t);
        for (int i = children.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            if (i != j)
            {
                int iIdx = children[i].GetSiblingIndex();
                int jIdx = children[j].GetSiblingIndex();
                children[i].SetSiblingIndex(jIdx);
                children[j].SetSiblingIndex(iIdx);
            }
        }

        // Random side
        foreach (var ci in player.board.Where(c => c.alive))
        {
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
        if (root == null) return;
        var toKill = new List<GameObject>();
        foreach (Transform t in root) toKill.Add(t.gameObject);
        foreach (var go in toKill) Destroy(go);
    }

    void CleanupDestroyedSlots()
    {
        var toRemove = enemySlotViews
            .Where(v => v == null || v.instance == null || !v.instance.alive)
            .ToList();
        foreach (var v in toRemove)
        {
            if (v != null) Destroy(v.gameObject);
            enemySlotViews.Remove(v);
        }
    }
}
