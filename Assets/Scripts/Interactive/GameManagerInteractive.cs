using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class GameManagerInteractive : MonoBehaviour
{
    [Header("Prefabs and Roots")]
    public GameObject playerCardPrefab;
    public GameObject aiCardPrefab;
    public Transform playerBoardRoot;
    public Transform aiBoardRoot;

    [Header("UI")]
    public Button btnFlipRandom;
    public Button btnForceFlip;
    public Button btnAttack;
    public Button btnEndTurn;
    public Text logText; // if you use TMP, change to TMP_Text

    [Header("Match parameters")]
    public int turns = 10;
    public int playerBaseAP = 3;
    public int aiBaseAP = 3; // AI gets +1 AP inside its turn
    public int initialBoardCount = 3;
    public int seed = 12345;

    [Header("Decks")]
    public bool useDemoDecks = false;
    public List<CardDefinition> playerDeck;
    public List<CardDefinition> aiDeck;

    System.Random rng;
    PlayerState player, ai;
    int currentTurn = 1;
    bool playerPhase = true;
    bool matchEnded = false;

    Dictionary<CardInstance, CardView> viewByInstance = new Dictionary<CardInstance, CardView>();
    Dictionary<CardView, CardInstance> instanceByView = new Dictionary<CardView, CardInstance>();

    void Start()
    {
        rng = new System.Random(seed);
        player = new PlayerState("Player", playerBaseAP);
        ai = new PlayerState("AI", aiBaseAP);

        // Validate inputs
        if (!useDemoDecks)
        {
            if (playerCardPrefab == null || aiCardPrefab == null)
            {
                Debug.LogError("Assign playerCardPrefab and aiCardPrefab in Inspector.");
                enabled = false; return;
            }
            if (playerDeck == null || playerDeck.Count == 0 || aiDeck == null || aiDeck.Count == 0)
            {
                Debug.LogError("Assign playerDeck and aiDeck with CardDefinition assets, or enable useDemoDecks.");
                enabled = false; return;
            }
        }
        else
        {
            CreateDemoDecksIfEmpty();
            if (playerCardPrefab == null) playerCardPrefab = CreateFallbackPrefab("Player Card");
            if (aiCardPrefab == null) aiCardPrefab = playerCardPrefab;
        }

        // Spawn initial boards from assigned decks
        SpawnInitial(player, playerDeck, playerCardPrefab, playerBoardRoot);
        SpawnInitial(ai, aiDeck, aiCardPrefab, aiBoardRoot);

        // Bind UI
        btnFlipRandom.onClick.AddListener(OnFlipRandom);
        btnForceFlip.onClick.AddListener(OnForceFlip);
        btnAttack.onClick.AddListener(OnAttack);
        btnEndTurn.onClick.AddListener(OnEndTurn);

        AppendLog("=== MATCH START ===");
        AppendLog("Player deck: " + playerDeck.Count + " | AI deck: " + aiDeck.Count);
        UpdateAllViews();
        UpdateHUD();
    }

    void SpawnInitial(PlayerState owner, List<CardDefinition> deck, GameObject prefab, Transform root)
    {
        int n = Mathf.Min(initialBoardCount, deck.Count);
        for (int i = 0; i < n; i++)
            AddCardToBoard(owner, deck[i], prefab, root);
    }

    void AddCardToBoard(PlayerState owner, CardDefinition def, GameObject prefab, Transform root)
    {
        var ci = new CardInstance(def, rng);
        owner.board.Add(ci);

        var go = Instantiate(prefab, root);
        var view = go.GetComponent<CardView>();
        if (view == null)
        {
            Debug.LogError("CardPrefab must have CardView component.");
            return;
        }
        view.Init(this, owner, ci);
        viewByInstance[ci] = view;
        instanceByView[view] = ci;
    }

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
                var view = viewByInstance[ci];
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

    // Optional: create demo decks at runtime if requested
    void CreateDemoDecksIfEmpty()
    {
        if (playerDeck == null) playerDeck = new List<CardDefinition>();
        if (aiDeck == null) aiDeck = new List<CardDefinition>();

        if (playerDeck.Count > 0 && aiDeck.Count > 0) return;

        CardDefinition Make(string name, Faction f, int hp, FrontType ft, int fDmg, int fBlk, int bDmg, int bBlk, int bPA)
        {
            var cd = ScriptableObject.CreateInstance<CardDefinition>();
            cd.cardName = name; cd.faction = f; cd.maxHealth = hp;
            cd.frontType = ft; cd.frontDamage = fDmg; cd.frontBlockValue = fBlk;
            cd.backDamageBonusSameFaction = bDmg; cd.backBlockBonusSameFaction = bBlk; cd.backBonusPAIfTwoRetroSameFaction = bPA;
            return cd;
        }

        if (playerDeck.Count == 0)
        {
            playerDeck.Add(Make("Lama del Culto", Faction.Sangue, 3, FrontType.Attacco, 3, 0, 1, 0, 0));
            playerDeck.Add(Make("Cantico Profano", Faction.Sangue, 3, FrontType.Attacco, 2, 0, 1, 0, 0));
            playerDeck.Add(Make("Scudo dell'Abisso", Faction.Ombra, 4, FrontType.Blocco, 0, 2, 0, 1, 0));
            playerDeck.Add(Make("Simbolo dell'Eclissi", Faction.Ombra, 4, FrontType.Blocco, 0, 1, 0, 1, 0));
            playerDeck.Add(Make("Spirale della Cenere", Faction.Fiamma, 3, FrontType.Attacco, 2, 0, 1, 0, 1));
            playerDeck.Add(Make("Portale Vermiglio", Faction.Fiamma, 3, FrontType.Attacco, 2, 0, 1, 0, 1));
        }

        if (aiDeck.Count == 0)
        {
            aiDeck.Add(Make("Predatore Cremisi", Faction.Sangue, 3, FrontType.Attacco, 3, 0, 1, 0, 0));
            aiDeck.Add(Make("Mano del Silenzio", Faction.Ombra, 3, FrontType.Blocco, 0, 2, 0, 1, 0));
            aiDeck.Add(Make("Custode dell'Abisso", Faction.Ombra, 4, FrontType.Blocco, 0, 1, 0, 1, 0));
            aiDeck.Add(Make("Eremita della Cenere", Faction.Fiamma, 2, FrontType.Attacco, 2, 0, 1, 0, 1));
            aiDeck.Add(Make("Veggente del Nulla", Faction.Fiamma, 3, FrontType.Attacco, 2, 0, 1, 0, 1));
        }
    }

    // Fallback visual if no prefab is assigned and useDemoDecks is true
    GameObject CreateFallbackPrefab(string title)
    {
        var go = new GameObject(title);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(160, 220);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        var btn = go.AddComponent<Button>();
        var cv = go.AddComponent<CardView>();
        return go;
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
