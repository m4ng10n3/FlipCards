using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class GameManagerInteractive : MonoBehaviour
{
    // ====== PREFAB BINDINGS (fallback ONLY if no scene cards are found) ======
    [System.Serializable]
    public class PrefabCardBinding
    {
        [Tooltip("Card prefab (must have CardView + CardDefinitionInline)")]
        public GameObject prefab;

        [Min(1), Tooltip("How many copies of this prefab to spawn")]
        public int count = 1;
    }

    // ====== SCENE TEMPLATE ======
    class SceneCardTemplate
    {
        public GameObject template;   // disabled clone used as source for Instantiate
        public CardDefinitionInline.Spec def;    // built at runtime from CardDefinitionInline
    }

    [Header("Roots")]
    public Transform playerBoardRoot;
    public Transform aiBoardRoot;

    [Header("UI")]
    public Button btnFlipRandom;
    public Button btnForceFlip;
    public Button btnAttack;
    public Button btnEndTurn;
    public Text logText; // use TMP_Text se preferisci TMP
    private static StringBuilder _logBuf = new StringBuilder(4096);

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

    static GameManagerInteractive _instance;
    public static GameManagerInteractive Instance => _instance;

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

    Dictionary<CardInstance, CardView> viewByInstance = new Dictionary<CardInstance, CardView>();
    Dictionary<CardView, CardInstance> instanceByView = new Dictionary<CardView, CardInstance>();

    List<CardView> playerViews = new List<CardView>();
    List<CardView> aiViews = new List<CardView>();

    // Nuovo: tieni traccia delle abilit attaccate per unbind sicuro
    Dictionary<CardInstance, List<AbilityBase>> abilitiesByInstance = new Dictionary<CardInstance, List<AbilityBase>>();

    void Start()
    {
        // Ensure SelectionManager singleton
        if (SelectionManager.Instance == null)
            new GameObject("SelectionManager").AddComponent<SelectionManager>();

        // Basic validation
        if (playerBoardRoot == null || aiBoardRoot == null)
        {
            Logger.Error("Assign playerBoardRoot and aiBoardRoot in the Inspector.");
            enabled = false; return;
        }

        rng = new System.Random(seed);
        player = new PlayerState("Player", playerBaseAP);
        ai = new PlayerState("AI", aiBaseAP);

        // 1) Try to build from SCENE templates (CardDefinitionInline on each card)
        BuildSideFromSceneOrBindings(player, playerBoardRoot, playerViews, playerCards, "PLAYER");
        BuildSideFromSceneOrBindings(ai, aiBoardRoot, aiViews, aiCards, "AI");

        // 2) Enforce minimum per side
        if (playerViews.Count < minCardsPerSide || aiViews.Count < minCardsPerSide)
        {
            Logger.Info("Not enough cards to start. Player:" + playerViews.Count + " / AI:" + aiViews.Count + " (min " + minCardsPerSide + ")");
            matchEnded = true;
            return;
        }

        // Bind UI
        Logger.Info("=== MATCH START (Scene templates -> runtime instances, inline defs) ===");
        UpdateAllViews();
        UpdateHUD();
        StartTurn(player, ai, true);

        // Pubblica inizio turno iniziale (player)
        //EventBus.Publish(GameEventType.TurnStart, new EventContext { owner = player, opponent = ai, phase = "TurnStart" });

        // Minimal default rules (all inline), disable by unchecking enableDefaultRules
        if (enableDefaultRules)
        {
            // Esempio: se una carta in FRONTE infligge danno a un'altra carta, applica +1 danno extra
            AddRule(GameEventType.DamageDealt,
                ctx => ctx.target != null && ctx.source != null && ctx.source.side == Side.Fronte,
                ctx => {
                    // PRIMA: GameRules.DealDamageToCard(...)
                    ctx.source.DealDamageToCard(ctx.owner, ctx.opponent, ctx.target, 1, "Rule:+1 Front");
                });

            // Esempio: all'inizio turno Player, se hai >=2 retro Ombra, ping di 1 al nemico
            AddRule(GameEventType.TurnStart,
                ctx => ctx.owner == player && player.CountRetro(Faction.Ombra) >= 2,
                ctx => {
                    // scegli una carta retro come "sorgente" del ping (evitiamo metodi statici)
                    var src = player.board.FirstOrDefault(c => c.alive && c.side == Side.Retro && c.def.faction == Faction.Ombra);
                    if (src != null) src.DealDamageToPlayer(ctx.owner, ctx.opponent, 1, "Rule:Upkeep Ping");
                });
        }

    }

    void Awake()
    {
        Logger.SetSink(AppendLog);     // <— questa è l’unica cosa indispensabile
        Logger.Info("== GameManager ready ==");

        _instance = this;
        // wiring bottoni
        if (btnFlipRandom) btnFlipRandom.onClick.AddListener(OnFlipRandom);
        if (btnForceFlip) btnForceFlip.onClick.AddListener(OnForceFlip);
        if (btnAttack) btnAttack.onClick.AddListener(OnAttack);
        if (btnEndTurn) btnEndTurn.onClick.AddListener(OnEndTurn);
    }


    public static void Logf(string fmt, params object[] args)
    => Logger.Info(string.Format(fmt, args));



    // ====== BUILD SIDE ======

    void BuildSideFromSceneOrBindings(PlayerState owner,
                                      Transform root,
                                      List<CardView> outViews,
                                      List<PrefabCardBinding> fallbackBindings,
                                      string label)
    {
        // A) From scene
        var templates = CaptureTemplatesAndClear(root, label);
        if (templates.Count > 0)
        {
            SpawnFromTemplates(owner, templates, root, outViews);
            // Cleanup temp clones
            foreach (var t in templates)
                if (t.template != null) Destroy(t.template);
            return;
        }

        // B) Fallback from prefab bindings (reads inline definition from prefab)
        SpawnFromBindings(owner, fallbackBindings, root, outViews);
    }

    // Helper: try get spec from a GO that has CardDefinitionInline
    bool TryGetSpec(GameObject go, out CardDefinitionInline.Spec spec)
    {
        spec = default;
        if (go == null) return false;
        var inline = go.GetComponent<CardDefinitionInline>();
        if (inline == null) return false;
        spec = inline.BuildSpec();
        return true;
    }

    // Scan children under root, pick those with CardView + CardDefinitionInline,
    // clone them as disabled templates, capture their built definition, then remove originals.
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

            if (!TryGetSpec(t.gameObject, out var def))
            {
                // Not a valid card (missing CardDefinitionInline)
                continue;
            }

            // Create disabled clone as template
            var template = Instantiate(t.gameObject);
            template.name = t.gameObject.name + " (TEMPLATE)";
            template.SetActive(false);

            list.Add(new SceneCardTemplate { template = template, def = def });

            // Remove original from scene (disappear at start)
            Destroy(t.gameObject);
        }

        if (list.Count > 0)
            Logger.Info("[" + label + "] Found " + list.Count + " scene card templates.");

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

    void AddCardFromTemplate(PlayerState owner, CardDefinitionInline.Spec def, GameObject template, Transform root, List<CardView> outViews)
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

        // Bind automatico abilit presenti sul prefab/istanza
        var opponent = (owner == player) ? ai : player;
        var abilities = go.GetComponents<AbilityBase>()?.ToList() ?? new List<AbilityBase>();
        foreach (var ab in abilities) ab.Bind(ci, owner, opponent);
        abilitiesByInstance[ci] = abilities;

        // Evento carta giocata sul board
        EventBus.Publish(GameEventType.CardPlayed, new EventContext { owner = owner, opponent = opponent, source = ci, phase = "Main" });
    }

    // ====== PREFAB BINDINGS FALLBACK (inline-only) ======

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
                Logger.Error("Prefab '" + b.prefab.name + "' must have CardDefinitionInline.");
                continue;
            }

            for (int i = 0; i < b.count; i++)
            {
                AddCardFromTemplate(owner, def, b.prefab, root, outViews);
            }
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
        string header = "[TURN " + currentTurn + "] " + (playerPhase ? "PLAYER PHASE" : "AI PHASE");
        string status = "Player HP:" + player.hp + " AP:" + player.actionPoints + "  ||  AI HP:" + ai.hp + " AP:" + ai.actionPoints;
        Logger.Info(header + "  " + status);
    }

    // ====== UI ACTIONS ======

    void StartTurn(PlayerState owner, PlayerState opponent, bool isPlayerPhase)
    {
        playerPhase = isPlayerPhase;
        // reset AP (semplice: base fissa; se vuoi bonus passivi, calcolali qui)
        owner.actionPoints = (owner == player) ? playerBaseAP : aiBaseAP;

        EventBus.Publish(GameEventType.TurnStart, new EventContext { owner = owner, opponent = opponent, phase = "TurnStart" });
        UpdateHUD();

        // se è turno IA, eseguila immediatamente e poi chiudi il turno
        if (!playerPhase && !matchEnded)
        {
            AIController.ExecuteTurn(rng, ai, player);    // vedi fix sotto per AIController
            CleanupDestroyed(player);
            CleanupDestroyed(ai);
            UpdateAllViews();
            UpdateHUD();

            // chiude turno IA e torna al player
            EndTurnInternal(ai, player);
            if (!matchEnded)
                StartTurn(player, ai, true);
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
        if (player.actionPoints <= 0) { Logger.Info("Niente PA per flippare."); UpdateHUD(); return; }
        var sel = SelectionManager.Instance.SelectedOwned?.instance;
        if (sel == null) return;
        sel.Flip();
        player.actionPoints -= 1;
        EventBus.Publish(GameEventType.Flip, new EventContext { owner = player, opponent = ai, source = sel });
        UpdateAllViews(); UpdateHUD();
    }

    void OnAttack()
    {
        if (matchEnded || !playerPhase) return;
        if (player.actionPoints <= 0) { Logger.Info("Niente PA per attaccare."); UpdateHUD(); return; }

        var atk = SelectionManager.Instance.SelectedOwned?.instance;
        var tgt = SelectionManager.Instance.SelectedEnemy?.instance;
        if (atk == null || tgt == null) return;

        atk.Attack(player, ai, tgt);  // già corretto
        player.actionPoints -= 1;

        CleanupDestroyed(player);
        CleanupDestroyed(ai);
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
            {
                currentTurn++;
                StartTurn(ai, player, false);
            }
        }
        else
        {
            // In pratica non ci arrivi, perché l’IA si auto-gestisce, ma per completezza:
            EndTurnInternal(ai, player);
            if (!matchEnded)
                StartTurn(player, ai, true);
        }
    }
    void CleanupDestroyed(PlayerState p)
    {
        foreach (var ci in p.board)
        {
            if (!ci.alive)
            {
                // Unbind abilit una sola volta quando la carta non  pi viva
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
        _logBuf.AppendLine($"[{Time.frameCount}] {msg}");
        if (logText != null) logText.text = _logBuf.ToString();
    }
    public void ClearLog() { _logBuf.Clear(); if (logText) logText.text = ""; }


    bool IsGameOver() => player.hp <= 0 || ai.hp <= 0;

    void EndMatch()
    {
        if (matchEnded) return;
        matchEnded = true;
        int diff = (ai.hp - player.hp);
        string result = diff > 0 ? "AI AHEAD" : diff < 0 ? "PLAYER AHEAD" : "TIE";
        Logger.Info("=== MATCH END ===");
        Logger.Info("Score: PlayerHP " + player.hp + " vs AIHP " + ai.hp + " | Diff (AI-Player) = " + diff + " -> " + result);
    }

    // Called by CardView on click
    public void OnCardClicked(CardView view)
    {
        if (matchEnded) return;
        bool isPlayers = view.owner == player;
        if (isPlayers) SelectionManager.Instance.SelectOwned(view);
        else SelectionManager.Instance.SelectEnemy(view);
    }

}
