using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [Serializable] public class PrefabCardBinding { public GameObject prefab; [Min(1)] public int count = 1; }
    [Serializable] public class PrefabSlotBinding { public GameObject prefab; [Min(1)] public int count = 1; }

    [Header("Roots")]
    public Transform playerBoardRoot;
    public Transform aiBoardRoot;

    [Header("UI")]
    public Button btnAttack;
    public Button btnEndTurn;
    public TMPro.TMP_Text logText;

    static readonly StringBuilder _logBuf = new StringBuilder(4096);

    [Header("HUD")]
    public TMPro.TMP_Text hpText;
    public TMPro.TMP_Text apText;
    public TMPro.TMP_Text EnemyHptxt;

    [Header("Match Parameters")]
    public int turns = 12;
    public int playerBaseAP = 4;
    public int seed = 12345;

    [Header("Start Constraints")]
    [Min(1)] public int CardsPerSide = 3;
    [Min(1)] public int StartingHandSize = 3;

    [Header("Balance")]
    [Min(1)] public int playerMaxHp = 20;
    [Min(1)] public int enemyMaxHp = 24;
    [Min(0)] public int drawCardCost = 1;
    [Min(0)] public int playCardCost = 1;
    [Min(0)] public int flipCardCost = 1;
    [Min(0)] public int swapCardCost = 1;
    [Min(0)] public int maxBonusAP = 1;
    [Min(0)] public int bossDamageOnSlotBreak = 1;
    [Range(0f, 1f)] public float chaosFlipChance = 0.45f;
    [Range(0f, 1f)] public float chaosSwapChance = 0.3f;
    [Min(0)] public int maxChaosFlipsPerTurn = 1;

    [Header("Ritmo della risoluzione")]
    [Tooltip("Pausa dopo le combo di adiacenza, prima della prima corsia.")]
    [Min(0f)] public float resolveOpeningDelay = 0.22f;
    [Tooltip("Tempo lasciato alle animazioni di una corsia prima di contare i morti.")]
    [Min(0f)] public float resolveLaneDelay = 0.38f;
    [Tooltip("Stacco fra una corsia e la successiva.")]
    [Min(0f)] public float resolveLaneGap = 0.12f;
    [Tooltip("Stacco fra l'ingresso di uno slot nemico e il successivo, dopo il rullo.")]
    [Min(0f)] public float slotEnterDelay = 0.26f;

    [Header("Refs")]
    [SerializeField] private HandManager handManager;
    public HandManager HandManager => handManager;
    [SerializeField] private SlotBatchManager slotBatchManager;

    [Header("Empty Spot")]
    public GameObject EmptySpot;

    [Header("Empty Slot")]
    public GameObject EmptySlot;

    [Header("Prefab Bindings")]
    public List<PrefabCardBinding> playerCards = new List<PrefabCardBinding>();

    [Header("Enemy Slots")]
    public List<PrefabSlotBinding> enemySlots = new List<PrefabSlotBinding>();

    bool awaitingEndTurn;
    // True mentre gira il reel di fine turno: gli slot nemici sono gia' stati
    // scambiati ma sono ancora coperti. Senza questo lock UpdateHUD riaccende
    // Attack/Draw a meta' animazione e il giocatore attaccherebbe slot invisibili.
    bool inputLocked;
    // True mentre la risoluzione scorre corsia per corsia. Distinto da
    // inputLocked perche' le due attese non sono la stessa cosa e la HUD deve
    // poterle chiamare per nome: "risoluzione" e "nuovi slot in arrivo".
    bool resolving;
    static GameManager _instance;
    public static GameManager Instance => _instance;

    System.Random rng;
    public PlayerState player;
    public PlayerState ai;

    int currentTurn = 1;
    bool playerPhase = true;
    bool matchEnded;
    string matchResult;

    // Stato letto dalla HUD: senza questi il layout non puo' distinguere le fasi
    // 7 (attacco risolto) e 8 (reel in corso), che altrimenti differiscono solo
    // per il grigio dei bottoni.
    public int CurrentTurn => currentTurn;
    public bool PlayerPhase => playerPhase;
    public bool AwaitingEndTurn => awaitingEndTurn;
    public bool InputLocked => inputLocked;
    public bool Resolving => resolving;
    public bool MatchEnded => matchEnded;
    public string MatchResult => matchResult;

    /// <summary>
    /// Il giocatore puo' agire. Serve a chi non e' un Button e quindi non passa
    /// da UpdateHUD: il mazzo cliccabile, per esempio.
    /// </summary>
    public bool CanAct => !matchEnded && playerPhase && !awaitingEndTurn && !inputLocked;

    /// <summary>
    /// Il generatore seminato della partita. Lo usa anche HandManager per
    /// mescolare il mazzo: con lo stesso seed la sequenza di pesca si ripete, ed
    /// e' quello che rende "la prossima carta" un fatto e non un tiro di dado.
    /// </summary>
    public System.Random Rng => rng;

    readonly Dictionary<CardInstance, CardView> viewByInstance = new Dictionary<CardInstance, CardView>();
    readonly Dictionary<CardInstance, List<AbilityBase>> abilitiesByInstance = new Dictionary<CardInstance, List<AbilityBase>>();
    readonly Dictionary<SlotInstance, SlotView> slotViewByInstance = new Dictionary<SlotInstance, SlotView>();
    readonly List<SlotView> enemySlotViews = new List<SlotView>();

    Transform playerBoardRootClone;
    public Transform PlayerBoardRootClone => playerBoardRootClone;

    public int MaxPlayerAP => playerBaseAP + maxBonusAP;

    void Awake()
    {
        Logger.SetSink(AppendLog);
        _instance = this;

        if (handManager == null)
            throw new InvalidOperationException("HandManager missing");

        btnAttack.onClick.AddListener(OnAttack);
        btnEndTurn.onClick.AddListener(OnEndTurn);
    }

    void Start()
    {
        rng = new System.Random(seed);
        player = new PlayerState("Player", playerMaxHp, playerBaseAP);
        ai = new PlayerState("Boss", enemyMaxHp, 0);

        ClearChildrenUnder(playerBoardRoot);
        SpawnInitialEmptySpots();

        var cloneGO = Instantiate(playerBoardRoot.gameObject, playerBoardRoot.parent);
        cloneGO.name = $"{playerBoardRoot.name}_Clone";
        cloneGO.transform.SetSiblingIndex(playerBoardRoot.GetSiblingIndex());
        playerBoardRootClone = cloneGO.transform;

        // Il clone e' il fantasma usato durante il drag: deve essere invisibile
        // finche' non serve. Spegnere solo l'Image della radice non basta, la
        // casella ha anche la cornice come figli.
        for (int i = 0; i < playerBoardRootClone.childCount; i++)
        {
            var child = playerBoardRootClone.GetChild(i);
            if (child.gameObject.name != EmptySpot.name) continue;
            SetSpotGraphicsVisible(child.gameObject, false);
        }

        SpawnEnemySlots();
        SpawnStartingHand();

        ClearLog();
        Logger.Info($"Match start | Player {player.hp}/{player.maxHp} HP | Boss {ai.hp}/{ai.maxHp} HP | AP {playerBaseAP}");

        UpdateAllViews();
        UpdateHUD();
        StartTurn(player, ai, true);
    }

    void SpawnStartingHand()
    {
        int savedAP = player.actionPoints;
        player.actionPoints = Mathf.Max(savedAP, StartingHandSize * Mathf.Max(1, drawCardCost));
        for (int i = 0; i < StartingHandSize; i++)
            handManager.DrawCard();
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
            if (outline != null)
            {
                outline.enabled = false;
                outline.effectDistance = new Vector2(5f, 5f);
                outline.useGraphicAlpha = false;
                outline.effectColor = Color.white;
            }

            var btn = spotGO.GetComponent<Button>();
            var t = spotGO.transform;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnEmptySpotClicked(t));
        }
    }

    List<GameObject> BuildEnemySlotPool()
    {
        var flat = new List<GameObject>();

        if (slotBatchManager != null && slotBatchManager.batch != null && slotBatchManager.batch.Count > 0)
        {
            foreach (var binding in slotBatchManager.batch)
            {
                if (binding?.prefab == null) continue;
                for (int i = 0; i < Mathf.Max(1, binding.count); i++)
                    flat.Add(binding.prefab);
            }
        }

        if (flat.Count > 0) return flat;

        foreach (var binding in enemySlots)
        {
            if (binding?.prefab == null) continue;
            for (int i = 0; i < Mathf.Max(1, binding.count); i++)
                flat.Add(binding.prefab);
        }

        return flat;
    }

    void AddSlotFromTemplate(PlayerState owner, SlotDefinition.Spec def, GameObject prefab, Transform root, List<SlotView> outViews)
    {
        var si = new SlotInstance(def);
        var go = Instantiate(prefab, root);
        go.name = prefab.name;
        go.SetActive(true);

        var view = go.GetComponent<SlotView>();
        view.Init(this, owner, si);
        slotViewByInstance[si] = view;
        outViews.Add(view);

        foreach (var ab in go.GetComponents<AbilityBase>())
            ab.Bind(null, owner, player);
    }

    void SpawnEnemySlots()
    {
        enemySlotViews.Clear();
        slotViewByInstance.Clear();
        DetachAndDestroy(aiBoardRoot);

        var flat = BuildEnemySlotPool();
        if (flat.Count == 0) return;

        for (int i = flat.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (flat[i], flat[j]) = (flat[j], flat[i]);
        }

        int lanes = playerBoardRoot.childCount;
        for (int lane = 0; lane < lanes; lane++)
        {
            var prefab = flat[lane % flat.Count];
            var sd = prefab.GetComponent<SlotDefinition>();
            if (sd == null) continue;
            AddSlotFromTemplate(ai, sd.BuildSpec(), prefab, aiBoardRoot, enemySlotViews);
        }
    }

    public void UpdateAllViews()
    {
        var viewsSnapshot = viewByInstance.Values.ToList();
        foreach (var view in viewsSnapshot)
        {
            if (view.instance != null && !view.instance.alive)
                RemoveCard(view.owner, view.instance);
        }

        foreach (var view in viewByInstance.Values)
            view.Refresh();

        for (int i = enemySlotViews.Count - 1; i >= 0; i--)
        {
            var slotView = enemySlotViews[i];
            if (!slotView.instance.alive)
            {
                RemoveSlotView(slotView);
                continue;
            }
            slotView.Refresh();
        }
    }

    public void UpdateHUD()
    {
        if (matchEnded) return;

        bool enable = playerPhase && !awaitingEndTurn && !inputLocked;
        btnAttack.interactable = enable;
        btnEndTurn.interactable = !inputLocked;
        // La pesca non ha piu' un bottone: e' il mazzo a lato, e DeckView si
        // regola da solo su GameManager.CanAct.

        // Il tetto degli AP e' MaxPlayerAP, non playerBaseAP: con il vecchio
        // denominatore un guadagno da abilita' stampava "5/4".
        if (hpText != null) hpText.text = $"{player.hp}/{player.maxHp}";
        if (apText != null) apText.text = $"{player.actionPoints}/{MaxPlayerAP}";
        if (EnemyHptxt != null) EnemyHptxt.text = $"{ai.hp}/{ai.maxHp}";
    }

    void StartTurn(PlayerState owner, PlayerState opponent, bool isPlayerPhase)
    {
        playerPhase = isPlayerPhase;
        awaitingEndTurn = false;
        owner.ResetAP(playerBaseAP);
        ResetCombatModifiers();

        EventBus.Publish(GameEventType.TurnStart, new EventContext
        {
            owner = owner,
            opponent = opponent,
            phase = $"TURN {currentTurn}"
        });

        Logger.Info($"Turn {currentTurn} start | HP {player.hp}-{ai.hp} | AP {owner.actionPoints}/{playerBaseAP}");

        UpdateAllViews();
        UpdateHUD();
    }

    public bool TrySpendPlayerAP(int amount, string reason = null)
    {
        int cost = Mathf.Max(0, amount);
        if (cost <= 0) return true;

        if (player.actionPoints < cost)
        {
            if (!string.IsNullOrEmpty(reason))
                Logger.Info($"{reason}: not enough AP");
            UpdateHUD();
            return false;
        }

        player.actionPoints -= cost;
        UpdateHUD();
        return true;
    }

    public int GainPlayerAP(int amount, string reason = null)
    {
        if (amount <= 0) return 0;

        int before = player.actionPoints;
        player.actionPoints = Mathf.Min(player.actionPoints + amount, MaxPlayerAP);
        int gained = player.actionPoints - before;

        if (gained > 0 && !string.IsNullOrEmpty(reason))
            Logger.Info($"{reason}: +{gained} AP");

        UpdateHUD();
        return gained;
    }

    public void ResetCombatModifiers()
    {
        if (player != null)
        {
            foreach (var card in player.board)
                card?.ClearCombatBonuses();
        }

        for (int i = 0; i < enemySlotViews.Count; i++)
            enemySlotViews[i]?.instance?.ClearCombatBonuses();
    }

    public CardView GetPlayerCardViewAtLane(int lane)
    {
        if (lane < 0 || lane >= playerBoardRoot.childCount) return null;
        return playerBoardRoot.GetChild(lane).GetComponentInChildren<CardView>(false);
    }

    public SlotView GetEnemySlotViewAtLane(int lane)
    {
        if (lane < 0 || lane >= aiBoardRoot.childCount) return null;
        return aiBoardRoot.GetChild(lane).GetComponentInChildren<SlotView>(false);
    }

    public CardInstance GetPlayerCardAtLane(int lane)
    {
        var view = GetPlayerCardViewAtLane(lane);
        return view?.instance != null && view.instance.alive ? view.instance : null;
    }

    public SlotInstance GetEnemySlotAtLane(int lane)
    {
        var view = GetEnemySlotViewAtLane(lane);
        return view?.instance != null && view.instance.alive ? view.instance : null;
    }

    public List<CardInstance> GetOrderedPlayerCards()
    {
        var ordered = new List<CardInstance>();
        for (int lane = 0; lane < playerBoardRoot.childCount; lane++)
        {
            var card = GetPlayerCardAtLane(lane);
            if (card != null) ordered.Add(card);
        }
        return ordered;
    }

    public void DealBossPressureFromBreak(CardInstance source, int amount, int lane)
    {
        int damage = Mathf.Max(0, amount);
        if (source == null || damage <= 0) return;

        ai.TakeDamage(damage);
        source.PushHint($"Boss -{damage}");
        Logger.Info($"Lane {lane + 1}: {source.def.cardName} breaks through for {damage} boss damage");
        UpdateHUD();
    }

    void OnAttack()
    {
        if (awaitingEndTurn || matchEnded || !playerPhase || inputLocked)
        {
            UpdateHUD();
            return;
        }

        StartCoroutine(ResolveAttackRoutine());
    }

    /// <summary>
    /// La risoluzione scorre una corsia per volta. Risolvendole tutte in un frame
    /// il giocatore vedeva solo lo stato finale e doveva ricostruire la catena
    /// leggendo il log; qui ogni riga di log cade mentre succede la cosa che
    /// descrive, perche' le Logger.Info stanno dentro LaneResolver e la sequenza
    /// e' temporizzata.
    ///
    /// awaitingEndTurn e lo sblocco dell'input stanno in fondo di proposito:
    /// anticiparli permetterebbe di attaccare due volte o di chiudere il turno a
    /// meta' risoluzione.
    /// </summary>
    IEnumerator ResolveAttackRoutine()
    {
        resolving = true;
        inputLocked = true;
        UpdateHUD();

        ResetCombatModifiers();
        EventBus.Publish(GameEventType.Custom, new EventContext
        {
            owner = player,
            opponent = ai,
            phase = "PrepareBattle"
        });

        SynergyResolver.Resolve(this, player, ai);
        yield return new WaitForSeconds(resolveOpeningDelay);

        int lanes = Mathf.Max(playerBoardRoot.childCount, aiBoardRoot.childCount);
        for (int lane = 0; lane < lanes; lane++)
        {
            var card = GetPlayerCardAtLane(lane);
            var slot = GetEnemySlotAtLane(lane);

            // Corsia vuota su entrambi i lati: non c'e' niente da guardare, e
            // fermarsi lo stesso sarebbe solo tempo morto.
            if (card == null && slot == null) continue;

            LaneResolver.Resolve(lane, card, slot, player, ai);
            yield return new WaitForSeconds(resolveLaneDelay);

            // I morti si contano qui e non alla fine: cosi la morte si vede nella
            // corsia in cui e' avvenuta. Gli indici restano validi, RemoveSlotView
            // e RemoveCard rimettono il segnaposto allo stesso sibling index.
            CleanupDestroyedSlots();
            UpdateAllViews();
            yield return new WaitForSeconds(resolveLaneGap);
        }

        CleanupDestroyedSlots();
        UpdateAllViews();

        resolving = false;
        inputLocked = false;
        awaitingEndTurn = true;
        Logger.Info($"Attack phase end | Boss {ai.hp}/{ai.maxHp} | Player {player.hp}/{player.maxHp}");
        UpdateHUD();
    }

    void RandomizePlayerBoard()
    {
        var ordered = GetOrderedPlayerCards();
        if (ordered.Count == 0) return;

        int flipsDone = 0;
        var flipCandidates = new List<CardInstance>();
        foreach (var card in ordered)
        {
            if (rng.NextDouble() <= Mathf.Clamp01(card.def.endTurnFlipChance))
                flipCandidates.Add(card);
        }

        while (flipCandidates.Count > 0 && flipsDone < maxChaosFlipsPerTurn && rng.NextDouble() < chaosFlipChance)
        {
            int pick = rng.Next(flipCandidates.Count);
            var card = flipCandidates[pick];
            flipCandidates.RemoveAt(pick);

            card.Flip();
            EventBus.Publish(GameEventType.Flip, new EventContext
            {
                owner = player,
                opponent = ai,
                source = card
            });

            if (viewByInstance.TryGetValue(card, out var view))
                view.FlipSide(false);

            Logger.Info($"Chaos: {card.def.cardName} flips to {card.side}");
            flipsDone++;
        }

        if (playerBoardRoot.childCount < 2 || rng.NextDouble() >= chaosSwapChance)
            return;

        var swappableLanes = new List<int>();
        for (int lane = 0; lane < playerBoardRoot.childCount - 1; lane++)
        {
            if (GetPlayerCardAtLane(lane) != null && GetPlayerCardAtLane(lane + 1) != null)
                swappableLanes.Add(lane);
        }

        if (swappableLanes.Count == 0) return;

        int swapLane = swappableLanes[rng.Next(swappableLanes.Count)];
        // Stesso percorso dello scambio per trascinamento: il giocatore subisce
        // questo cambiamento, quindi deve almeno vederlo succedere.
        AnimateLaneSwap(GetPlayerCardViewAtLane(swapLane), GetPlayerCardViewAtLane(swapLane + 1));
        Logger.Info($"Chaos: lane {swapLane + 1} swaps with lane {swapLane + 2}");
    }

    void AccumulateFlipCharges()
    {
        foreach (var card in GetOrderedPlayerCards())
        {
            if (card.side != Side.Retro) continue;
            int gained = card.GainCharge(1);
            if (gained <= 0) continue;
            card.PushHint($"Charge {card.flipCharge}/{CardInstance.MaxFlipCharge}");
        }
    }

    void AdvanceSlotPatterns()
    {
        for (int i = 0; i < enemySlotViews.Count; i++)
        {
            var slotView = enemySlotViews[i];
            if (slotView?.instance == null || !slotView.instance.alive) continue;

            Side oldSide = slotView.instance.side;
            slotView.instance.AdvanceFlip();
            slotView.Refresh();

            if (slotView.instance.side != oldSide)
                Logger.Info($"Slot shift: {slotView.instance.def.SlotName} -> {slotView.instance.side}");
        }
    }

    public void OnCardDoubleClicked(CardView view)
    {
        TryFlipCard(view);
    }

    bool TryFlipCard(CardView view)
    {
        if (awaitingEndTurn || matchEnded || !playerPhase || inputLocked)
        {
            UpdateHUD();
            return false;
        }

        if (view == null || view.instance == null || view.owner != player)
            return false;

        if (!TrySpendPlayerAP(flipCardCost, "Flip"))
            return false;

        view.instance.Flip();

        EventBus.Publish(GameEventType.Flip, new EventContext
        {
            owner = player,
            opponent = ai,
            source = view.instance
        });

        Logger.Info($"Flip: {view.instance.def.cardName} -> {view.instance.side}");
        UpdateAllViews();
        UpdateHUD();
        return true;
    }

    public void SwapCardPositions(CardView a, CardView b)
    {
        if (matchEnded || awaitingEndTurn || !playerPhase || inputLocked)
        {
            UpdateHUD();
            return;
        }

        if (a == null || b == null || a == b)
            return;

        if (!TrySpendPlayerAP(swapCardCost, "Swap"))
            return;

        int idxA = GetLaneIndexFor(a.instance);
        int idxB = GetLaneIndexFor(b.instance);

        if (!AnimateLaneSwap(a, b)) return;

        Logger.Info($"Swap: lane {idxA + 1} with lane {idxB + 1}");
        UpdateAllViews();
        UpdateHUD();
    }

    /// <summary>
    /// Scambia due corsie e lascia che le carte ci arrivino animate.
    ///
    /// Il tween NON puo' stare sul container: l'HorizontalLayoutGroup lo
    /// riposiziona a ogni layout pass e vincerebbe lui. Si sposta invece la
    /// grafica della carta indietro alla posizione di partenza e si lascia fare a
    /// CardView.FollowContainer, che e' la stessa animazione del rilascio dopo un
    /// trascinamento — carta sollevata compresa.
    /// </summary>
    bool AnimateLaneSwap(CardView a, CardView b)
    {
        if (a == null || b == null || a == b) return false;

        var cA = a.PlayerBoardContainer != null ? a.PlayerBoardContainer : a.EnsurePlayerBoardContainer(playerBoardRoot);
        var cB = b.PlayerBoardContainer != null ? b.PlayerBoardContainer : b.EnsurePlayerBoardContainer(playerBoardRoot);
        if (cA == null || cB == null || cA == cB) return false;
        if (cA.parent != playerBoardRoot || cB.parent != playerBoardRoot) return false;

        Vector3 worldA = a.RectTransform.position;
        Vector3 worldB = b.RectTransform.position;

        int idxA = cA.GetSiblingIndex();
        int idxB = cB.GetSiblingIndex();
        cA.SetSiblingIndex(idxB);
        cB.SetSiblingIndex(idxA);

        // Il layout group applica le nuove posizioni solo a fine frame: qui serve
        // subito, altrimenti si rimetterebbe la grafica rispetto al posto vecchio.
        Canvas.ForceUpdateCanvases();

        a.RectTransform.position = worldA;
        b.RectTransform.position = worldB;
        return true;
    }

    void OnEndTurn()
    {
        if (matchEnded || !playerPhase || inputLocked) return;

        RandomizePlayerBoard();
        AccumulateFlipCharges();
        AdvanceSlotPatterns();

        EventBus.Publish(GameEventType.TurnEnd, new EventContext
        {
            owner = player,
            opponent = ai,
            phase = "TurnEnd"
        });

        CleanupDestroyedSlots();
        UpdateAllViews();
        Logger.Info($"Turn {currentTurn} end | HP {player.hp}-{ai.hp}");

        if (IsGameOver() || currentTurn >= turns)
        {
            EndMatch();
            return;
        }

        currentTurn++;

        if (slotBatchManager != null)
        {
            SetButtonsInteractable(false);
            int laneCount = playerBoardRoot.childCount;

            // Il respawn avviene mentre le lane sono coperte dal reel: quando il
            // rullo si ferma sfuma e rivela lo slot vero, gia in posizione.
            slotBatchManager.RollNewSlots(
                laneCount,
                chosenPrefabs => RespawnEnemySlotsFromList(chosenPrefabs),
                _ => StartCoroutine(EnterEnemySlotsRoutine()));
        }
        else
        {
            StartTurn(player, ai, true);
        }
    }

    /// <summary>
    /// Gli slot entrano uno per volta e si vede cosa li ha modificati.
    ///
    /// Le statistiche vengono fotografate prima e dopo <see cref="StartTurn"/>:
    /// e' li' che le abilita' di inizio turno applicano i loro effetti, e cio' che
    /// e' cambiato viene evidenziato mentre la riga di log lo racconta. Senza
    /// questo confronto gli slot comparirebbero gia' coi valori finali e le
    /// abilita' che li hanno prodotti resterebbero invisibili.
    ///
    /// L'input resta bloccato fino all'ultimo slot: SetButtonsInteractable(true)
    /// sta in fondo.
    /// </summary>
    IEnumerator EnterEnemySlotsRoutine()
    {
        int lanes = aiBoardRoot.childCount;
        var baseHp = new int[lanes];
        var baseBlock = new int[lanes];
        var baseAtk = new int[lanes];

        for (int lane = 0; lane < lanes; lane++)
        {
            var slot = GetEnemySlotAtLane(lane);
            if (slot == null) continue;
            baseHp[lane] = slot.health;
            baseBlock[lane] = slot.ComputeSelfBlock();
            baseAtk[lane] = slot.def.atkDamage + slot.tempAtkBonus;
        }

        StartTurn(player, ai, true);

        for (int lane = 0; lane < lanes && lane < aiBoardRoot.childCount; lane++)
        {
            var view = GetEnemySlotViewAtLane(lane);
            var slot = view != null ? view.instance : null;
            if (slot == null || !slot.alive) continue;

            view.PlayEnter();

            string abilities = AbilityNames(view);
            Logger.Info($"Lane {lane + 1}: {slot.def.SlotName} entra in {slot.side}" +
                        $" | ATK {slot.def.atkDamage} DEF {slot.ComputeSelfBlock()} HP {slot.health}/{slot.def.maxHealth}" +
                        (string.IsNullOrEmpty(abilities) ? "" : $" | {abilities}"));

            AnnounceSlotChange(lane, view, slot, baseHp[lane], baseBlock[lane], baseAtk[lane]);

            yield return new WaitForSeconds(slotEnterDelay);
        }

        SetButtonsInteractable(true);
        UpdateHUD();
    }

    /// <summary>Evidenzia e racconta le statistiche mosse dalle abilita' di inizio turno.</summary>
    void AnnounceSlotChange(int lane, SlotView view, SlotInstance slot, int hp, int block, int atk)
    {
        int nowHp = slot.health;
        int nowBlock = slot.ComputeSelfBlock();
        int nowAtk = slot.def.atkDamage + slot.tempAtkBonus;

        if (nowAtk != atk)
        {
            view.PulseStat(GamePalette.Danger);
            Logger.Info($"Lane {lane + 1}: {slot.def.SlotName} ATK {atk} -> {nowAtk}");
        }
        else if (nowBlock != block)
        {
            view.PulseStat(GamePalette.Retro);
            Logger.Info($"Lane {lane + 1}: {slot.def.SlotName} DEF {block} -> {nowBlock}");
        }
        else if (nowHp != hp)
        {
            view.PulseStat(GamePalette.Good);
            Logger.Info($"Lane {lane + 1}: {slot.def.SlotName} HP {hp} -> {nowHp}");
        }
    }

    static string AbilityNames(Component owner)
    {
        var abilities = owner.GetComponents<AbilityBase>();
        if (abilities == null || abilities.Length == 0) return string.Empty;

        var names = new List<string>(abilities.Length);
        foreach (var ability in abilities)
            names.Add(AbilityCatalog.Name(ability));
        return string.Join(" · ", names);
    }

    void SetButtonsInteractable(bool on)
    {
        inputLocked = !on;
        btnAttack.interactable = on;
        btnEndTurn.interactable = on;
    }

    void RespawnEnemySlotsFromList(List<GameObject> prefabs)
    {
        for (int i = enemySlotViews.Count - 1; i >= 0; i--)
        {
            var slotView = enemySlotViews[i];
            if (slotView != null && slotView.instance != null)
                slotViewByInstance.Remove(slotView.instance);
        }
        enemySlotViews.Clear();

        DetachAndDestroy(aiBoardRoot);

        int lanes = playerBoardRoot.childCount;
        for (int lane = 0; lane < lanes; lane++)
        {
            if (lane >= prefabs.Count || prefabs[lane] == null)
            {
                if (EmptySlot != null)
                {
                    var empty = Instantiate(EmptySlot, aiBoardRoot);
                    empty.name = EmptySlot.name;
                    empty.SetActive(true);
                }
                continue;
            }

            var prefab = prefabs[lane];
            var definition = prefab.GetComponent<SlotDefinition>();
            if (definition == null) continue;
            AddSlotFromTemplate(ai, definition.BuildSpec(), prefab, aiBoardRoot, enemySlotViews);
        }
    }

    void RemoveCard(PlayerState owner, CardInstance card)
    {
        if (abilitiesByInstance.TryGetValue(card, out var abilities))
        {
            for (int i = 0; i < abilities.Count; i++)
                abilities[i].Unbind();
            abilitiesByInstance.Remove(card);
        }

        if (!viewByInstance.TryGetValue(card, out var view))
            return;

        var sel = SelectionManager.Instance;
        if (sel != null && sel.SelectedOwned == view)
            sel.ClearAll();

        Transform parent = view.transform.parent;
        int laneIndex = view.transform.GetSiblingIndex();

        var boardContainer = view.PlayerBoardContainer;
        if (boardContainer != null && boardContainer.parent == playerBoardRoot)
        {
            parent = boardContainer.parent;
            laneIndex = boardContainer.GetSiblingIndex();
            boardContainer.SetParent(null, false);   // vedi RemoveSlotView
            Destroy(boardContainer.gameObject);
        }

        viewByInstance.Remove(card);
        owner.board.Remove(card);
        // La pila degli scarti nel rail conta le carte del giocatore uscite dal
        // gioco: e' l'unico posto in cui questa informazione esiste.
        if (owner == player) HandManager?.NotifyDiscarded();
        card.Dispose();
        if (view.transform.parent != null) view.transform.SetParent(null, false);
        Destroy(view.gameObject);

        if (owner == player)
        {
            var spotGO = Instantiate(EmptySpot, parent);
            spotGO.name = EmptySpot.name;
            spotGO.SetActive(true);
            spotGO.transform.SetSiblingIndex(laneIndex);

            var outline = spotGO.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = false;
                outline.effectDistance = new Vector2(5f, 5f);
                outline.useGraphicAlpha = false;
                outline.effectColor = Color.white;
            }

            var btn = spotGO.GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnEmptySpotClicked(spotGO.transform));
        }
    }

    void PlayCardFromHand(CardView handCard, Transform emptySpot)
    {
        if (!TrySpendPlayerAP(playCardCost, "Play"))
            return;

        var definition = handCard.GetComponentInParent<CardDefinition>();
        var parent = emptySpot.parent;
        int laneIndex = emptySpot.GetSiblingIndex();

        emptySpot.SetParent(null, false);   // vedi DetachAndDestroy
        Destroy(emptySpot.gameObject);

        var card = new CardInstance(definition.BuildSpec(), rng);
        card.AssignGM(this);
        player.board.Add(card);

        GameObject go = Instantiate(definition.gameObject, parent);
        go.name = definition.gameObject.name;
        go.SetActive(true);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;

        var view = go.GetComponentInChildren<CardView>();
        view.Init(this, player, card);
        view.EnsurePlayerBoardContainer(playerBoardRoot);
        view.PlayerBoardContainer.SetSiblingIndex(laneIndex);
        viewByInstance[card] = view;

        var abilities = go.GetComponents<AbilityBase>().ToList();
        foreach (var ability in abilities)
            ability.Bind(card, player, ai);
        abilitiesByInstance[card] = abilities;

        handManager.OnHandCardDroppedToBoard(handCard);
        handManager.RemoveFromHand(handCard);

        EventBus.Publish(GameEventType.CardPlayed, new EventContext
        {
            owner = player,
            opponent = ai,
            source = card,
            phase = "FromHandToBoard"
        });

        Logger.Info($"Play: {card.def.cardName} to lane {laneIndex + 1}");
        UpdateAllViews();
        UpdateHUD();
    }

    void CleanupDestroyedSlots()
    {
        for (int i = enemySlotViews.Count - 1; i >= 0; i--)
        {
            if (!enemySlotViews[i].instance.alive)
                RemoveSlotView(enemySlotViews[i]);
        }
    }

    void RemoveSlotView(SlotView view)
    {
        Transform parent = view.transform.parent;
        int laneIndex = view.transform.GetSiblingIndex();

        enemySlotViews.Remove(view);
        slotViewByInstance.Remove(view.instance);
        view.instance.Dispose();
        // Distacco immediato: subito sotto viene istanziato l'EmptySlot che lo
        // rimpiazza e senza questo la lane resterebbe contata due volte fino a
        // fine frame (childCount gonfiato -> lane sfasate e reel disallineato).
        view.transform.SetParent(null, false);
        Destroy(view.gameObject);

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
            return GetEnemySlotAtLane(GetLaneIndexFor(card));

        if (obj is SlotInstance slot)
            return GetPlayerCardAtLane(GetLaneIndexFor(slot));

        return null;
    }

    public int GetLaneIndexFor(object obj)
    {
        if (obj is CardInstance card && viewByInstance.TryGetValue(card, out var cardView))
        {
            if (cardView.PlayerBoardContainer != null && cardView.PlayerBoardContainer.parent == playerBoardRoot)
                return cardView.PlayerBoardContainer.GetSiblingIndex();

            if (cardView.transform.parent != null && cardView.transform.parent.parent == playerBoardRoot)
                return cardView.transform.parent.GetSiblingIndex();

            return cardView.transform.GetSiblingIndex();
        }

        if (obj is SlotInstance slot && slotViewByInstance.TryGetValue(slot, out var slotView))
            return slotView.transform.GetSiblingIndex();

        return -1;
    }

    void ClearChildrenUnder(Transform root) => DetachAndDestroy(root);

    /// <summary>
    /// Svuota 'root' SUBITO. Destroy e' differito a fine frame: senza il distacco,
    /// childCount e GetChild(i) restano sporchi per un frame e il LayoutGroup
    /// impagina il doppio dei figli (slot che si stringono / saltano di posto).
    /// </summary>
    static void DetachAndDestroy(Transform root)
    {
        if (root == null) return;
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var child = root.GetChild(i);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
    }

    /// <summary>Tetto del buffer di log: sopra i ~16000 caratteri TMP smette di renderizzare.</summary>
    const int MaxLogChars = 6000;

    /// <summary>Notifica al pannello di log che il testo e' cambiato (autoscroll).</summary>
    public static event Action LogChanged;

    public void AppendLog(string msg)
    {
        _logBuf.AppendLine(msg);

        if (_logBuf.Length > MaxLogChars)
        {
            // Taglia in testa fino al primo a capo utile: mai a meta' di una riga.
            int cut = _logBuf.Length - MaxLogChars;
            for (int i = cut; i < _logBuf.Length && i < cut + 400; i++)
            {
                if (_logBuf[i] != '\n') continue;
                cut = i + 1;
                break;
            }
            _logBuf.Remove(0, cut);
        }

        if (logText != null) logText.text = _logBuf.ToString();
        LogChanged?.Invoke();
    }

    public void ClearLog()
    {
        _logBuf.Clear();
        if (logText != null) logText.text = string.Empty;
        LogChanged?.Invoke();
    }

    bool IsGameOver() => player.hp <= 0 || ai.hp <= 0;

    void EndMatch()
    {
        if (matchEnded) return;
        matchEnded = true;
        // UpdateHUD esce subito su matchEnded, quindi i bottoni resterebbero
        // accesi. Il mazzo non passa da qui: DeckView guarda CanAct, che su
        // matchEnded e' gia' false.
        SetButtonsInteractable(false);

        matchResult = player.hp > ai.hp ? "Player ahead" :
                      player.hp < ai.hp ? "Boss ahead" :
                      "Tie";
        Logger.Info($"Match end | Player {player.hp}/{player.maxHp} | Boss {ai.hp}/{ai.maxHp} | {matchResult}");
    }

    public void OnCardClicked(CardView view)
    {
        if (matchEnded || view == null) return;

        bool isHandCard = view.owner == null && view.instance == null;
        if (isHandCard)
        {
            if (awaitingEndTurn || !playerPhase || inputLocked)
            {
                UpdateHUD();
                return;
            }

            var emptySpot = SelectionManager.Instance != null ? SelectionManager.Instance.SelectedEmptySpot : null;
            if (emptySpot == null)
            {
                // "Casella prima, carta poi" non e' scopribile: invece di uscire in
                // silenzio, accendi tutte le caselle libere e dillo nel log.
                HighlightFreeSpots(true);
                Logger.Info("Play: scegli prima una casella libera");
                return;
            }

            PlayCardFromHand(view, emptySpot);
            HighlightFreeSpots(false);
            SelectionManager.Instance?.SelectEmptySpot(null);
            return;
        }

        if (view.owner != player || view.instance == null) return;
        SelectionManager.Instance.SelectOwned(view);
    }

    public void OnEmptySpotClicked(Transform emptySpot)
    {
        if (matchEnded || emptySpot == null) return;
        HighlightFreeSpots(false);
        SelectionManager.Instance?.SelectEmptySpot(emptySpot);
    }

    /// <summary>Accende o spegne tutti i grafici di una casella (radice + cornice).</summary>
    public static void SetSpotGraphicsVisible(GameObject spot, bool visible)
    {
        if (spot == null) return;
        foreach (var graphic in spot.GetComponentsInChildren<Graphic>(true))
            graphic.enabled = visible;
    }

    /// <summary>
    /// Accende l'Outline di tutte le caselle libere: e' l'unico segnale che rende
    /// scopribile la sequenza "casella prima, carta poi" (stato 3 del layout).
    /// La casella gia' selezionata resta accesa comunque.
    /// </summary>
    public void HighlightFreeSpots(bool on)
    {
        if (playerBoardRoot == null || EmptySpot == null) return;

        var selected = SelectionManager.Instance != null ? SelectionManager.Instance.SelectedEmptySpot : null;

        for (int i = 0; i < playerBoardRoot.childCount; i++)
        {
            var child = playerBoardRoot.GetChild(i);
            if (child.gameObject.name != EmptySpot.name) continue;

            var outline = child.GetComponent<Outline>();
            if (outline == null) continue;
            outline.enabled = on || child == selected;
        }
    }
}
