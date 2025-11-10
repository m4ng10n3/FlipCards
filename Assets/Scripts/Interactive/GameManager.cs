using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    // ====== PREFAB BINDINGS (fallback ONLY if no scene cards are found) ======
    [System.Serializable]
    public class PrefabCardBinding
    {
        [Tooltip("Card prefab (must have CardView + CardDefinition)")]
        public GameObject prefab;

        [Min(1), Tooltip("How many copies of this prefab to spawn")]
        public int count = 1;
    }

    // ====== SCENE TEMPLATE ======
    class SceneCardTemplate
    {
        public GameObject template;                 // disabled clone used as source for Instantiate
        public CardDefinition.Spec def;       // built at runtime from CardDefinition
    }

    [Header("Roots")]
    public Transform playerBoardRoot;
    public Transform aiBoardRoot;

    [Header("UI")]
    public Button btnFlipRandom;
    public Button btnForceFlip;
    public Button btnAttack;
    public Button btnEndTurn;

    [Header("Control")]
    public bool enemyControlledByButtons = true;   // se true non auto-esegue l'IA: usi i bottoni Enemy
    public Button btnEnemyAttack;
    public Button btnEnemyFlip;

    [Header("LOG")]
    public Text logText; // usa TMP_Text se preferisci TMP
    private static readonly StringBuilder _logBuf = new StringBuilder(4096);

    [Header("Match parameters")]
    public int turns = 10;
    public int playerBaseAP = 3;
    public int aiBaseAP = 3; // AI may get extra AP via GameRules
    public int seed = 12345;

    [Header("Start constraints")]
    [Min(1)] public int minCardsPerSide = 3;

    [Header("Prefab bindings (used only if NO scene cards are found)")]
    public List<PrefabCardBinding> playerCards = new List<PrefabCardBinding>();
    public List<PrefabCardBinding> aiCards = new List<PrefabCardBinding>();

    static GameManager _instance;
    public static GameManager Instance => _instance;

    public bool TryGetView(CardInstance ci, out CardView v) => viewByInstance.TryGetValue(ci, out v);

    // ====== SIMPLE RULE ENGINE (INLINE, NO EXTRA FILES) ======
    struct Rule
    {
        public GameEventType trigger;
        public Func<EventContext, bool> cond;
        public Action<EventContext> act;
        public bool enabled;
    }

    readonly List<Rule> _rules = new List<Rule>();
    public bool enableDefaultRules = true; // puoi spegnerle in Inspector

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
    readonly List<CardView> aiViews = new List<CardView>();

    // Traccia abilità attaccate per unbind sicuro
    readonly Dictionary<CardInstance, List<AbilityBase>> abilitiesByInstance = new Dictionary<CardInstance, List<AbilityBase>>();

    void Awake()
    {
        Logger.SetSink(AppendLog);
        EventBus.Publish(GameEventType.Info, new EventContext { phase = "GameManager ready" });
        _instance = this;

        // Wiring bottoni UI
        if (btnFlipRandom) btnFlipRandom.onClick.AddListener(OnFlipRandom);
        if (btnForceFlip) btnForceFlip.onClick.AddListener(OnForceFlip);
        if (btnAttack) btnAttack.onClick.AddListener(OnAttack);
        if (btnEndTurn) btnEndTurn.onClick.AddListener(OnEndTurn);
        if (btnEnemyAttack) btnEnemyAttack.onClick.AddListener(OnEnemyAttack);
        if (btnEnemyFlip) btnEnemyFlip.onClick.AddListener(OnEnemyFlip);
    }

    void Start()
    {
        // Ensure SelectionManager singleton
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
        ai = new PlayerState("AI", aiBaseAP);

        // 1) Build da SCENA o da Prefab
        BuildSideFromSceneOrBindings(player, playerBoardRoot, playerViews, playerCards, "PLAYER");
        BuildSideFromSceneOrBindings(ai, aiBoardRoot, aiViews, aiCards, "AI");

        // 2) Minimo carte
        if (playerViews.Count < minCardsPerSide || aiViews.Count < minCardsPerSide)
        {
            Logger.Info($"Not enough cards to start. Player:{playerViews.Count} / AI:{aiViews.Count} (min {minCardsPerSide})");
            matchEnded = true;
            return;
        }

        EventBus.Publish(GameEventType.Info, new EventContext { phase = "=== MATCH START ===" });

        UpdateAllViews();
        UpdateHUD();

        // 3) Regole di default (opzionali)
        if (enableDefaultRules)
        {
            // Esempio: +1 danno extra se source è in Fronte
            /*
            AddRule(GameEventType.DamageDealt,
                ctx => ctx.target != null && ctx.source != null && ctx.source.side == Side.Fronte,
                ctx => { ctx.source.DealDamageToCard(ctx.owner, ctx.opponent, ctx.target, 1, "Rule:+1 Front"); });
            */
            // Esempio: upkeep ping se Player ha >=2 Retro Ombra
            AddRule(GameEventType.TurnStart,
                ctx => ctx.owner == player && player.CountRetro(Faction.Ombra) >= 2,
                ctx =>
                {
                    var src = player.board.FirstOrDefault(c => c.alive && c.side == Side.Retro && c.def.faction == Faction.Ombra);
                    if (src != null) src.DealDamageToPlayer(ctx.owner, ctx.opponent, 1, "Rule:Upkeep Ping");
                });
        }

        // 4) Avvia turno Player
        StartTurn(player, ai, true);
    }

    // ====== BUILD SIDE ======
    void BuildSideFromSceneOrBindings(PlayerState owner,
                                      Transform root,
                                      List<CardView> outViews,
                                      List<PrefabCardBinding> fallbackBindings,
                                      string label)
    {
        var templates = CaptureTemplatesAndClear(root, label);
        if (templates.Count > 0)
        {
            SpawnFromTemplates(owner, templates, root, outViews);
            foreach (var t in templates) if (t.template != null) Destroy(t.template);
            return;
        }

        SpawnFromBindings(owner, fallbackBindings, root, outViews);
    }

    bool TryGetSpec(GameObject go, out CardDefinition.Spec spec)
    {
        spec = default;
        if (go == null) return false;
        var inline = go.GetComponent<CardDefinition>();
        if (inline == null) return false;
        spec = inline.BuildSpec();
        return true;
    }

    List<SceneCardTemplate> CaptureTemplatesAndClear(Transform root, string label)
    {
        var list = new List<SceneCardTemplate>();
        if (root == null) return list;

        var toProcess = new List<Transform>();
        foreach (Transform t in root) toProcess.Add(t);

        foreach (var t in toProcess)
        {
            var view = t.GetComponent<CardView>();
            if (view == null) continue;
            if (!TryGetSpec(t.gameObject, out var def)) continue;

            var template = Instantiate(t.gameObject);
            template.name = t.gameObject.name + " (TEMPLATE)";
            template.SetActive(false);

            list.Add(new SceneCardTemplate { template = template, def = def });
            Destroy(t.gameObject); // rimuovi originali
        }

        if (list.Count > 0)
            EventBus.Publish(GameEventType.Info, new EventContext { phase = $"[{label}] Found {list.Count} scene card templates." });

        return list;
    }

    void SpawnFromTemplates(PlayerState owner, List<SceneCardTemplate> templates, Transform root, List<CardView> outViews)
    {
        foreach (var t in templates)
        {
            if (t.template == null) continue;
            AddCardFromTemplate(owner, t.def, t.template, root, outViews);
        }
    }

    void AddCardFromTemplate(PlayerState owner, CardDefinition.Spec def, GameObject template, Transform root, List<CardView> outViews)
    {
        var ci = new CardInstance(def, rng);
        owner.board.Add(ci);

        var go = Instantiate(template, root);
        go.name = template.name.Replace(" (TEMPLATE)", "");
        go.SetActive(true);

        var view = go.GetComponent<CardView>();
        if (view == null) { Logger.Error("Card template has no CardView."); Destroy(go); return; }

        view.Init(this, owner, ci);
        viewByInstance[ci] = view;
        instanceByView[view] = ci;
        outViews.Add(view);

        // Bind automatico abilità sul prefab
        var opponent = (owner == player) ? ai : player;
        var abilities = go.GetComponents<AbilityBase>()?.ToList() ?? new List<AbilityBase>();
        foreach (var ab in abilities) ab.Bind(ci, owner, opponent);
        abilitiesByInstance[ci] = abilities;

        // Evento carta giocata
        EventBus.Publish(GameEventType.CardPlayed, new EventContext { owner = owner, opponent = opponent, source = ci, phase = "Main" });
    }

    void SpawnFromBindings(PlayerState owner, List<PrefabCardBinding> bindings, Transform root, List<CardView> outViews)
    {
        if (bindings == null) return;

        foreach (var b in bindings)
        {
            if (b == null || b.count <= 0 || b.prefab == null)
            {
                Logger.Warn("Invalid binding: assign Prefab and Count >= 1.");
                continue;
            }
            if (!TryGetSpec(b.prefab, out var def))
            {
                Logger.Error("Prefab '" + b.prefab.name + "' must have CardDefinition.");
                continue;
            }
            for (int i = 0; i < b.count; i++)
                AddCardFromTemplate(owner, def, b.prefab, root, outViews);
        }
    }

    // ====== REFRESH / HUD ======
    public void UpdateAllViews()
    {
        foreach (var kv in viewByInstance)
            kv.Value.Refresh();
    }

    public void UpdateHUD()
    {
        if (matchEnded) return;
    }

    // ====== TURN FLOW / UI ACTIONS ======
    void StartTurn(PlayerState owner, PlayerState opponent, bool isPlayerPhase)
    {
        playerPhase = isPlayerPhase;

        // Reset AP (puoi aggiungere bonus passivi qui)
        owner.actionPoints = (owner == player) ? playerBaseAP : aiBaseAP;

        EventBus.Publish(GameEventType.TurnStart, new EventContext { owner = owner, opponent = opponent, phase = "TurnStart" });
        UpdateHUD();

        // Se è turno IA: o manuale (bottoni) o auto-esecuzione
        if (!playerPhase && !matchEnded)
        {
            if (enemyControlledByButtons)
            {
                EventBus.Publish(GameEventType.Info, new EventContext { phase = "[GM] Enemy manual phase: usa i bottoni Enemy per agire." });
                return; // restiamo nella fase Enemy in attesa input
            }

            // IA automatica
            AIController.ExecuteTurn(rng, ai, player);

            CleanupDestroyed(player);
            CleanupDestroyed(ai);
            UpdateAllViews();
            UpdateHUD();

            // Chiudi turno IA e passa al Player
            EndTurnInternal(ai, player);
            if (!matchEnded)
            {
                currentTurn++; // incrementa quando torni al Player
                StartTurn(player, ai, true);
            }
        }
    }

    void EndTurnInternal(PlayerState owner, PlayerState opponent)
    {
        EventBus.Publish(GameEventType.TurnEnd, new EventContext { owner = owner, opponent = opponent, phase = "TurnEnd" });
        if (IsGameOver() || currentTurn >= turns) EndMatch();
    }

    void OnFlipRandom()
    {
        if (matchEnded) return;
        var list = player.board.Where(c => c.alive).ToList();
        if (list.Count == 0) return;

        var c = list[rng.Next(list.Count)];
        c.Flip();
        EventBus.Publish(GameEventType.Flip, new EventContext { owner = player, opponent = ai, source = c });
        UpdateAllViews();
    }

    void OnForceFlip()
    {
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
        if (matchEnded || !playerPhase) return;
        if (player.actionPoints <= 0) { EventBus.Publish(GameEventType.Info, new EventContext { phase = "Not enough Player PA" }); UpdateHUD(); return; }

        var atk = SelectionManager.Instance.SelectedOwned?.instance;
        var tgt = SelectionManager.Instance.SelectedEnemy?.instance;
        if (atk == null || tgt == null) return;

        atk.Attack(player, ai, tgt);
        player.actionPoints -= 1;

        CleanupDestroyed(player);
        CleanupDestroyed(ai);
        UpdateAllViews();
        UpdateHUD();
    }

    void OnEnemyAttack()
    {
        if (matchEnded || playerPhase) return;          // deve essere fase AI
        if (!enemyControlledByButtons) return;          // controllo manuale attivo
        if (ai.actionPoints <= 0) { EventBus.Publish(GameEventType.Info, new EventContext { phase = "Not enough Enemy PA" }); UpdateHUD(); return; }

        var atk = SelectionManager.Instance.SelectedEnemy?.instance;
        var tgt = SelectionManager.Instance.SelectedOwned?.instance;
        if (atk == null || tgt == null) return;

        atk.Attack(ai, player, tgt);
        ai.actionPoints -= 1;

        CleanupDestroyed(player);
        CleanupDestroyed(ai);
        UpdateAllViews();
        UpdateHUD();
    }

    void OnEnemyFlip()
    {
        if (matchEnded || playerPhase) return;
        if (!enemyControlledByButtons) return;
        if (ai.actionPoints <= 0) { Logger.Info("Enemy: no AP."); UpdateHUD(); return; }

        var sel = SelectionManager.Instance.SelectedEnemy?.instance;
        if (sel == null) return;

        sel.Flip();
        ai.actionPoints -= 1;

        EventBus.Publish(GameEventType.Flip, new EventContext { owner = ai, opponent = player, source = sel });
        UpdateAllViews();
        UpdateHUD();
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
                currentTurn++;                 // incrementa quando torni al Player
                StartTurn(player, ai, true);
            }
        }
    }

    void CleanupDestroyed(PlayerState p)
    {
        foreach (var ci in p.board)
        {
            if (!ci.alive)
            {
                // Unbind abilità una sola volta quando la carta non è più viva
                if (abilitiesByInstance.TryGetValue(ci, out var list) && list != null)
                {
                    foreach (var ab in list) { if (ab != null) ab.Unbind(); }
                    abilitiesByInstance.Remove(ci);
                }

                if (viewByInstance.TryGetValue(ci, out var view))
                    view.Refresh();
            }
        }
    }

    public void AppendLog(string msg)
    {
        // timestamp leggero: framecount
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

    // Called by CardView on click
    public void OnCardClicked(CardView view)
    {
        if (matchEnded) return;
        bool isPlayers = view.owner == player;
        if (isPlayers) SelectionManager.Instance.SelectOwned(view);
        else SelectionManager.Instance.SelectEnemy(view);
    }

    // ====== LANE / TARGETING (REALTIME) ======

    /// <summary>
    /// Restituisce la CardInstance avversaria "di fronte" all'attaccante, basandosi
    /// sul SiblingIndex della CardView nella gerarchia (posizione LIVE in scena).
    /// Robusta a placeholder/oggetti extra: se il child opposto non ha CardView,
    /// cerca la prima CardView valida in quella lane.
    /// </summary>
    public CardInstance GetOpposingCardInstance(CardInstance attacker)
    {
        if (attacker == null) return null;
        if (!TryGetView(attacker, out var atkView) || atkView == null) return null;

        Transform myRoot = (atkView.owner == player) ? playerBoardRoot : aiBoardRoot;
        Transform oppRoot = (atkView.owner == player) ? aiBoardRoot : playerBoardRoot;
        if (myRoot == null || oppRoot == null) return null;

        int lane = atkView.transform.GetSiblingIndex();
        if (lane < 0 || lane >= oppRoot.childCount) return null;

        // 1) Child alla stessa lane
        Transform oppChild = oppRoot.GetChild(lane);
        CardView oppView = oppChild ? oppChild.GetComponent<CardView>() : null;

        // 2) Fallback: se il child non ha CardView (placeholder, wrapper, ecc.),
        // prova a cercare nel child una CardView discendente oppure
        // scansiona i figli per la prima CardView con lo stesso siblingIndex logico.
        if (oppView == null)
        {
            // tenta nel discendente diretto
            oppView = oppChild ? oppChild.GetComponentInChildren<CardView>(includeInactive: false) : null;

            // se ancora nulla, scandisci tutti i figli della root avversaria e prendi
            // la prima CardView che abbia proprio quel sibling index (layout custom)
            if (oppView == null)
            {
                for (int i = 0; i < oppRoot.childCount; i++)
                {
                    var ch = oppRoot.GetChild(i);
                    var cv = ch ? ch.GetComponent<CardView>() : null;
                    if (cv != null && ch.GetSiblingIndex() == lane) { oppView = cv; break; }
                }
            }
        }

        if (oppView == null) return null;
        if (!instanceByView.TryGetValue(oppView, out var target)) return null;
        if (target == null || !target.alive) return null;

        return target;
    }

    // Helper opzionali (se utili altrove)
    public int GetLaneIndex(CardInstance ci)
        => (TryGetView(ci, out var v) && v != null) ? v.transform.GetSiblingIndex() : -1;

    public CardView GetOpposingCardView(CardInstance attacker)
    {
        if (!TryGetView(attacker, out var atkView) || atkView == null) return null;
        Transform oppRoot = (atkView.owner == player) ? aiBoardRoot : playerBoardRoot;

        int lane = atkView.transform.GetSiblingIndex();
        if (lane < 0 || lane >= oppRoot.childCount) return null;

        var oppChild = oppRoot.GetChild(lane);
        var oppView = oppChild ? oppChild.GetComponent<CardView>() : null;
        if (oppView != null) return oppView;

        // fallback leggero
        return oppChild ? oppChild.GetComponentInChildren<CardView>(includeInactive: false) : null;
    }

    public int GetDisplayIndex(CardView v) => v ? (v.transform.GetSiblingIndex() + 1) : -1;
    public char GetSideChar(CardView v) => (v != null && v.owner == player) ? 'P' : 'E';

}
