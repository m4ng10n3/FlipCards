using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

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
            Logger.Error("Not enough cards to start. Player:" + playerViews.Count + " / AI:" + aiViews.Count + " (min " + minCardsPerSide + ")");
            matchEnded = true;
            return;
        }

        // Bind UI
        AppendLog("=== MATCH START (Scene templates -> runtime instances, inline defs) ===");
        UpdateAllViews();
        UpdateHUD();

        // Pubblica inizio turno iniziale (player)
        EventBus.Publish(GameEventType.TurnStart, new EventContext { owner = player, opponent = ai, phase = "TurnStart" });
    }

    void Awake()
    {
        Logger.Sink = AppendLog;

        _instance = this;
        // wiring bottoni
        if (btnFlipRandom) btnFlipRandom.onClick.AddListener(OnFlipRandom);
        if (btnForceFlip) btnForceFlip.onClick.AddListener(OnForceFlip);
        if (btnAttack) btnAttack.onClick.AddListener(OnAttack);
        if (btnEndTurn) btnEndTurn.onClick.AddListener(OnEndTurn);
    }

    // metodo statico chiamato da EventLogger
    public static void TryAppendLogStatic(string line)
    {
        if (_instance != null) _instance.AppendLog(line);
    }

    // Elabora la coda (richiamalo dopo azioni importanti o su Update con throttling)
    void PumpTurnQueue(int maxSteps = 64)
    {
        var q = EventLogger.Instance?.turnQueue;
        if (q == null) return;
        int steps = 0;
        while (steps++ < maxSteps && q.TryDequeue(out var it))
        {
            // qui potresti innescare VFX leggeri sulle carte coinvolte
            if (it.ctx.source?.view != null) it.ctx.source.view.Blink();
            if (it.ctx.target?.view != null) it.ctx.target.view.Blink();
        }
    }



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
        AppendLog(header + "  " + status);
    }

    // ====== UI ACTIONS ======

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
        var sel = SelectionManager.Instance.SelectedOwned?.instance; // vedi patch E
        if (sel == null) return;
        sel.Flip();
        EventBus.Publish(GameEventType.Flip, new EventContext { owner = player, opponent = ai, source = sel });
        UpdateAllViews();
    }

    void OnAttack()
    {
        var atk = SelectionManager.Instance.SelectedOwned?.instance;
        var tgt = SelectionManager.Instance.SelectedEnemy?.instance;
        if (atk == null || tgt == null) return;

        // Deleghiamo tutta la risoluzione a GameRules, che pubblica anche gli eventi
        GameRules.Attack(player, ai, atk, tgt);

        // Pulizia eventuali carte distrutte e refresh UI
        CleanupDestroyed(player);
        CleanupDestroyed(ai);
        UpdateAllViews();
        UpdateHUD();
    }
    void SwapActive()
    {
        playerPhase = !playerPhase;
    }



    void OnEndTurn()
    {
        EventBus.Publish(GameEventType.TurnEnd, new EventContext { owner = player, opponent = ai });
        SwapActive(); // passa il turno
        EventBus.Publish(GameEventType.TurnStart, new EventContext { owner = player, opponent = ai });
        UpdateHUD();
    }

    void StartPlayerPhase()
    {
        playerPhase = true;
        player.ResetAP(playerBaseAP + GameRules.PassiveBonusPA(player));

        // (Comportamento esistente) flip casuale a inizio fase player
        foreach (var c in player.board)
            if (c.alive && rng.NextDouble() < 0.5)
            {
                c.Flip();
                EventBus.Publish(GameEventType.Flip, new EventContext { owner = player, opponent = ai, source = c, phase = "TurnStart" });
            }

        // Evento inizio turno Player
        EventBus.Publish(GameEventType.TurnStart, new EventContext { owner = player, opponent = ai, phase = "TurnStart" });

        UpdateAllViews();
        UpdateHUD();
        AppendLog("Player phase started.");
    }

    void RunAITurn()
    {
        // Evento inizio turno AI
        EventBus.Publish(GameEventType.TurnStart, new EventContext { owner = ai, opponent = player, phase = "TurnStart" });

        ai.ResetAP(aiBaseAP + GameRules.PassiveBonusPA(ai));
        AIController.ExecuteTurn(rng, ai, player);
        CleanupDestroyed(ai);
        CleanupDestroyed(player);
        UpdateAllViews();
        UpdateHUD();
        AppendLog("AI phase ended.");

        // Evento fine turno AI
        EventBus.Publish(GameEventType.TurnEnd, new EventContext { owner = ai, opponent = player, phase = "TurnEnd" });

        CheckEndImmediate();
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

    public void AppendLog(string line)
    {
        if (logText == null) return;
        if (!string.IsNullOrEmpty(logText.text)) logText.text += "\n";
        logText.text += line;
    }

    bool IsGameOver() => player.hp <= 0 || ai.hp <= 0;

    void CheckEndImmediate() { if (IsGameOver()) EndMatch(); }

    void EndMatch()
    {
        if (matchEnded) return;
        matchEnded = true;
        int diff = (ai.hp - player.hp);
        string result = diff > 0 ? "AI AHEAD" : diff < 0 ? "PLAYER AHEAD" : "TIE";
        AppendLog("=== MATCH END ===");
        AppendLog("Score: PlayerHP " + player.hp + " vs AIHP " + ai.hp + " | Diff (AI-Player) = " + diff + " -> " + result);
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
