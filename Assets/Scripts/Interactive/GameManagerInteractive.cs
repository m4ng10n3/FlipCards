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
    public Text logText; // use TMP_Text if preferisci TMP

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

    // ====== RUNTIME ======
    System.Random rng;
    PlayerState player, ai;
    int currentTurn = 1;
    bool playerPhase = true;
    bool matchEnded = false;

    Dictionary<CardInstance, CardView> viewByInstance = new Dictionary<CardInstance, CardView>();
    Dictionary<CardView, CardInstance> instanceByView = new Dictionary<CardView, CardInstance>();

    List<CardView> playerViews = new List<CardView>();
    List<CardView> aiViews = new List<CardView>();

    void Start()
    {
        // Ensure SelectionManager singleton
        if (SelectionManager.Instance == null)
            new GameObject("SelectionManager").AddComponent<SelectionManager>();

        // Basic validation
        if (playerBoardRoot == null || aiBoardRoot == null)
        {
            Debug.LogError("Assign playerBoardRoot and aiBoardRoot in the Inspector.");
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
            Debug.LogError("Not enough cards to start. Player:" + playerViews.Count + " / AI:" + aiViews.Count + " (min " + minCardsPerSide + ")");
            matchEnded = true;
            return;
        }

        // Bind UI
        if (btnFlipRandom) btnFlipRandom.onClick.AddListener(OnFlipRandom);
        if (btnForceFlip) btnForceFlip.onClick.AddListener(OnForceFlip);
        if (btnAttack) btnAttack.onClick.AddListener(OnAttack);
        if (btnEndTurn) btnEndTurn.onClick.AddListener(OnEndTurn);

        AppendLog("=== MATCH START (Scene templates -> runtime instances, inline defs) ===");
        UpdateAllViews();
        UpdateHUD();
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
            Debug.Log("[" + label + "] Found " + list.Count + " scene card templates.");

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
        if (view == null) { Debug.LogError("Card template has no CardView."); Destroy(go); return; }

        view.Init(this, owner, ci);
        viewByInstance[ci] = view;
        instanceByView[view] = ci;
        outViews.Add(view);
    }

    // ====== PREFAB BINDINGS FALLBACK (inline-only) ======

    void SpawnFromBindings(PlayerState owner, List<PrefabCardBinding> bindings, Transform root, List<CardView> outViews)
    {
        if (bindings == null) return;

        foreach (var b in bindings)
        {
            if (b == null || b.count <= 0 || b.prefab == null)
            {
                Debug.LogWarning("Invalid binding: assign Prefab and Count >= 1.");
                continue;
            }

            if (!TryGetSpec(b.prefab, out var def))
            {
                Debug.LogError("Prefab '" + b.prefab.name + "' must have CardDefinitionInline.");
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
        if (matchEnded || !playerPhase) return;
        int flips = 0;
        foreach (var c in player.board)
            if (c.alive && rng.NextDouble() < 0.5) { c.Flip(); flips++; }
        AppendLog("Flip random: " + flips + " cards flipped.");
        UpdateAllViews();
    }

    void OnForceFlip()
    {
        if (matchEnded || !playerPhase) return;
        var selected = SelectionManager.Instance.SelectedOwned;
        if (selected == null) { AppendLog("Select one of YOUR cards to force flip."); return; }
        var ci = instanceByView[selected];
        if (!ci.alive) { AppendLog("Card is destroyed."); return; }
        if (player.actionPoints <= 0) { AppendLog("No AP left to force flip."); return; }
        ci.Flip();
        player.actionPoints -= 1;
        AppendLog("Forced flip: " + ci.def.cardName + " -> " + ci.side);
        selected.Blink();
        UpdateAllViews();
        UpdateHUD();
    }

    void OnAttack()
    {
        if (matchEnded || !playerPhase) return;
        if (player.actionPoints <= 0) { AppendLog("No AP left to attack."); return; }

        var attackerView = SelectionManager.Instance.SelectedOwned;
        var targetView = SelectionManager.Instance.SelectedEnemy;

        if (attackerView == null) { AppendLog("Select YOUR attacker card (Front/Attack)."); return; }
        if (targetView == null) { AppendLog("Select ENEMY target card."); return; }

        var attacker = instanceByView[attackerView];
        var target = instanceByView[targetView];

        if (!attacker.alive) { AppendLog("Attacker is destroyed."); return; }
        if (attacker.side != Side.Fronte || attacker.def.frontType != FrontType.Attacco)
        { AppendLog("Selected attacker is not in Front/Attack."); return; }

        GameRules.Attack(player, ai, attacker, target);
        player.actionPoints -= 1;

        CleanupDestroyed(ai);
        CleanupDestroyed(player);

        attackerView.Blink();
        targetView.Blink();

        UpdateAllViews();
        UpdateHUD();
        CheckEndImmediate();
    }

    void OnEndTurn()
    {
        if (matchEnded || !playerPhase) return;

        playerPhase = false;
        RunAITurn();

        currentTurn += 1;
        if (currentTurn > turns || IsGameOver())
        {
            EndMatch(); return;
        }

        StartPlayerPhase();
    }

    void StartPlayerPhase()
    {
        playerPhase = true;
        player.ResetAP(playerBaseAP + GameRules.PassiveBonusPA(player));
        foreach (var c in player.board) if (c.alive && rng.NextDouble() < 0.5) c.Flip();
        UpdateAllViews();
        UpdateHUD();
        AppendLog("Player phase started.");
    }

    void RunAITurn()
    {
        ai.ResetAP(aiBaseAP + GameRules.PassiveBonusPA(ai));
        AIController.ExecuteTurn(rng, ai, player);
        CleanupDestroyed(ai);
        CleanupDestroyed(player);
        UpdateAllViews();
        UpdateHUD();
        AppendLog("AI phase ended.");
        CheckEndImmediate();
    }

    void CleanupDestroyed(PlayerState p)
    {
        foreach (var ci in p.board)
        {
            if (!ci.alive)
            {
                if (viewByInstance.TryGetValue(ci, out var view))
                    view.Refresh();
            }
        }
    }

    void AppendLog(string msg)
    {
        if (logText != null) logText.text += "\n" + msg;
        Debug.Log(msg);
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
