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
    public int playerBaseAP = 3;
    public int seed = 12345;

    [Header("Start Constraints")]
    [Min(1)] public int CardsPerSide = 3;
    [Min(1)] public int StartingHandSize = 3;

    [Header("Balance")]
    [Min(1)] public int playerMaxHp = 20;
    [Min(1)] public int enemyMaxHp = 24;
    [Min(1)] public int attackCost = 1;
    [Min(0)] public int drawCardCost = 1;
    [Min(0)] public int playCardCost = 1;
    [Min(0)] public int flipCardCost = 1;
    [Min(0)] public int swapCardCost = 1;
    [Min(0)] public int maxBonusAP = 2;

    [Header("Difficolta'")]
    [Tooltip("L'unica manopola. 0 = allenamento, 1 = il boss non perdona. Muove " +
             "insieme vita e attacco delle caselle, quante carte il fine turno " +
             "rimescola e quanti punti azione restano al giocatore.")]
    [Range(0f, 1f)] public float difficulty = 0.5f;

    /// <summary>Vita delle caselle del pool: la corazza del boss si ispessisce con la difficolta'.</summary>
    public float SlotHealthScale => Mathf.Lerp(0.8f, 1.5f, difficulty);
    /// <summary>Attacco delle caselle: quanto fa male restare scoperti.</summary>
    public float SlotAttackScale => Mathf.Lerp(0.7f, 1.4f, difficulty);
    /// <summary>Quante carte a terra si girano da sole a fine turno.</summary>
    public int ChaosFlips => Mathf.RoundToInt(Mathf.Lerp(1f, 3f, difficulty));
    /// <summary>Quante coppie di carte a terra si scambiano di posto a fine turno.</summary>
    public int ChaosSwaps => Mathf.RoundToInt(Mathf.Lerp(0f, 2f, difficulty));
    /// <summary>Punti azione a inizio turno: la difficolta' li toglie, non li aggiunge.</summary>
    public int EffectiveBaseAP => Mathf.Max(1, playerBaseAP - Mathf.FloorToInt(difficulty * 2f));

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
    public string RollSummary { get; private set; } = "Abbina carta e slot: +1 ATK o BLOCCO";

    /// <summary>Maschera delle corsie che hanno fatto combinazione nell'ultimo giro.</summary>
    public int RollPayoutLanes { get; private set; }
    /// <summary>Cambia a ogni vincita: chi disegna la cassa lo osserva e lampeggia.</summary>
    public int RollPayoutSerial { get; private set; }
    public bool RollPayoutJackpot { get; private set; }
    public bool CanAttack => CanAct && player != null && player.actionPoints >= Mathf.Max(1, attackCost);

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

    /// <summary>
    /// Le caselle numerate del boss. Non e' un dettaglio del rullo: e' la sua
    /// corazza, e la partita e' il tentativo di smontarla pezzo per pezzo.
    /// </summary>
    readonly BossPool bossPool = new BossPool();
    public BossPool Pool => bossPool;

    Transform playerBoardRootClone;
    public Transform PlayerBoardRootClone => playerBoardRootClone;

    public int MaxPlayerAP => EffectiveBaseAP + maxBonusAP;

    void Awake()
    {
        Logger.SetSink(AppendLog);
        _instance = this;

        if (handManager == null)
            throw new InvalidOperationException("HandManager missing");

        btnAttack.onClick.AddListener(OnAttack);
        btnEndTurn.onClick.AddListener(OnEndTurn);
    }

    void OnDestroy()
    {
        foreach (var pair in abilitiesByInstance)
            foreach (var ability in pair.Value) if (ability != null) ability.Unbind();
        if (player != null) foreach (var card in player.board) card?.Dispose();
        foreach (var view in enemySlotViews) if (view != null) view.instance?.Dispose();
        if (_instance == this) _instance = null;
    }

    void Start()
    {
        rng = new System.Random(seed);
        player = new PlayerState("Player", playerMaxHp, EffectiveBaseAP);
        ai = new PlayerState("Boss", enemyMaxHp, 0);

        bossPool.Build(BuildEnemySlotPool(), SlotHealthScale, SlotAttackScale);

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
        Logger.Info($"Match start | Player {player.hp}/{player.maxHp} HP | Boss {ai.hp}/{ai.maxHp} HP | AP {EffectiveBaseAP}" +
                    $" | difficolta' {difficulty:0.00} | corazza {bossPool.Summary()}");

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

    /// <summary>
    /// I prefab distinti che formano il pool, in ordine: l'ordine diventa il
    /// numero della casella. Niente ripetizioni — due copie della stessa casella
    /// avrebbero due vite separate e il numero stampato non vorrebbe dire piu' niente.
    /// </summary>
    List<GameObject> BuildEnemySlotPool()
    {
        var flat = new List<GameObject>();

        if (slotBatchManager != null && slotBatchManager.batch != null && slotBatchManager.batch.Count > 0)
        {
            foreach (var binding in slotBatchManager.batch)
                if (binding?.prefab != null && !flat.Contains(binding.prefab)) flat.Add(binding.prefab);
        }

        if (flat.Count > 0) return flat;

        foreach (var binding in enemySlots)
            if (binding?.prefab != null && !flat.Contains(binding.prefab)) flat.Add(binding.prefab);

        return flat;
    }

    void AddSlotFromTemplate(PlayerState owner, SlotDefinition.Spec def, GameObject prefab, Transform root, List<SlotView> outViews, BossPool.Entry origin = null)
    {
        var si = new SlotInstance(def, def.flipPattern != null && def.flipPattern.Length > 0 ? rng.Next(def.flipPattern.Length) : 0, origin);
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

        var flat = bossPool.AlivePrefabs();
        if (flat.Count == 0) return;

        for (int i = flat.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (flat[i], flat[j]) = (flat[j], flat[i]);
        }

        int lanes = playerBoardRoot.childCount;
        for (int lane = 0; lane < lanes && lane < flat.Count; lane++)
        {
            var entry = bossPool.Of(flat[lane]);
            if (entry == null) continue;
            AddSlotFromTemplate(ai, entry.spec, entry.prefab, aiBoardRoot, enemySlotViews, entry);
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
        btnAttack.interactable = CanAttack;
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
        ApplyRollReward();
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

    public void ResetCombatModifiers(bool includeSlots = true)
    {
        if (player != null)
        {
            foreach (var card in player.board)
                card?.ClearCombatBonuses();
        }

        if (includeSlots)
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

    /// <summary>
    /// Il danno che avanza dopo aver finito una casella arriva al boss.
    ///
    /// E' l'unico modo di fargli male finche' la corazza tiene, ed e' cio' che
    /// rende diverso "uccidere una casella" da "uccidere una casella con il
    /// colpo giusto": pareggiare la sua vita non gli toglie niente, eccederla
    /// gli toglie la differenza. Le cariche accumulate stando coperti servono
    /// esattamente a questo.
    /// </summary>
    public void OverflowToBoss(int amount, object attacker, SlotInstance broken)
    {
        int damage = Mathf.Max(0, amount);
        if (damage <= 0) return;

        ai.TakeDamage(damage);
        string who = attacker is CardInstance c ? c.def.cardName : "?";
        if (attacker is CardInstance card) card.PushHint($"SFONDA -{damage}");
        Logger.Info($"Traboccamento: {who} sfonda {broken?.def.SlotName} e il boss paga {damage}");
        UpdateHUD();
    }

    /// <summary>
    /// Speculare: il colpo che supera la vita della carta arriva al giocatore.
    /// La corsia si e' scoperta a meta' colpo e il resto e' passato.
    /// </summary>
    public void OverflowToPlayer(int amount, object attacker, CardInstance fallen)
    {
        int damage = Mathf.Max(0, amount);
        if (damage <= 0) return;

        player.TakeDamage(damage);
        string who = attacker is SlotInstance s ? s.def.SlotName : "?";
        Logger.Info($"Traboccamento: {who} passa oltre {fallen?.def.cardName} e il giocatore paga {damage}");
        UpdateHUD();
    }

    /// <summary>Annuncia che una casella e' uscita dal pool per sempre.</summary>
    public void AnnouncePoolBreak(SlotInstance slot)
    {
        if (slot == null) return;
        int number = slot.PoolNumber;
        Logger.Info($"Casella {(number > 0 ? "#" + number + " " : "")}{slot.def.SlotName} distrutta: fuori dal rullo. Corazza {bossPool.Summary()}");
    }

    void OnAttack()
    {
        if (awaitingEndTurn || matchEnded || !playerPhase || inputLocked)
        {
            UpdateHUD();
            return;
        }

        if (!TrySpendPlayerAP(Mathf.Max(1, attackCost), "Attacco")) return;
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
    IEnumerator ResolveAttackRoutine(bool playerAttacks = true, bool rollAfter = false)
    {
        resolving = true;
        inputLocked = true;
        UpdateHUD();

        ResetCombatModifiers(includeSlots: false);
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

            LaneResolver.Resolve(lane, card, slot, player, ai, playerAttacks);
            yield return new WaitForSeconds(resolveLaneDelay);

            // I morti si contano qui e non alla fine: cosi la morte si vede nella
            // corsia in cui e' avvenuta. Gli indici restano validi, RemoveSlotView
            // e RemoveCard rimettono il segnaposto allo stesso sibling index.
            CleanupDestroyedSlots();
            UpdateAllViews();
            if (IsGameOver()) break;
            yield return new WaitForSeconds(resolveLaneGap);
        }

        CleanupDestroyedSlots();
        UpdateAllViews();

        resolving = false;
        inputLocked = false;
        awaitingEndTurn = true;
        Logger.Info($"Attack phase end | Boss {ai.hp}/{ai.maxHp} | Player {player.hp}/{player.maxHp}");
        if (IsGameOver()) { EndMatch(); yield break; }
        UpdateHUD();
        if (rollAfter) OnEndTurn();
    }

    /// <summary>
    /// Il rimescolamento di fine turno: le carte a terra si girano e si
    /// scambiano di posto da sole.
    ///
    /// E' l'avversario vero del gioco. Il rullo cambia le caselle, questo cambia
    /// la tua fila: la disposizione che avevi costruito non sopravvive al turno,
    /// e ogni turno ricomincia da una posizione che non hai scelto. Rimetterla a
    /// posto — insegna accanto a chi colpisce, risonanza dove vuoi sfondare — e'
    /// il lavoro del giocatore, e la manopola della difficolta' decide quanto ce
    /// n'e' da fare.
    ///
    /// Le carte scosse per prime sono quelle nelle corsie che il giro NON ha
    /// abbinato: il caso morde dove non stavi guardando, non dove avevi appena
    /// costruito qualcosa.
    /// </summary>
    IEnumerator RandomizePlayerBoard()
    {
        var ordered = GetOrderedPlayerCards();
        if (ordered.Count == 0) yield break;

        // 1. Si girano.
        var flipCandidates = new List<CardInstance>();
        foreach (var card in ordered)
            if (rng.NextDouble() < Mathf.Clamp01(card.def.endTurnFlipChance)) flipCandidates.Add(card);

        var shaken = new List<CardInstance>();
        foreach (var candidate in flipCandidates)
            if (!SynergyResolver.Resonates(this, GetLaneIndexFor(candidate)))
                shaken.Add(candidate);

        int flipsDone = 0;
        while (flipCandidates.Count > 0 && flipsDone < ChaosFlips)
        {
            var pool = shaken.Count > 0 ? shaken : flipCandidates;
            int pick = rng.Next(pool.Count);
            var card = pool[pick];
            pool.RemoveAt(pick);
            flipCandidates.Remove(card);
            shaken.Remove(card);

            card.Flip();
            EventBus.Publish(GameEventType.Flip, new EventContext
            {
                owner = player,
                opponent = ai,
                source = card
            });

            // Animato, non solo aggiornato: e' un cambiamento che il giocatore
            // subisce, quindi deve vederlo girare (ROADMAP A5).
            if (viewByInstance.TryGetValue(card, out var view))
                view.FlipSide(false);

            card.PushHint($"SCOSSA -> {card.side}");
            Logger.Info($"Caos: {card.def.cardName} si gira su {card.side}");
            flipsDone++;
            yield return new WaitForSeconds(0.4f);
        }

        // 2. Si scambiano di posto.
        int swapsDone = 0;
        while (swapsDone < ChaosSwaps)
        {
            var swappableLanes = new List<int>();
            for (int lane = 0; lane < playerBoardRoot.childCount - 1; lane++)
                if (GetPlayerCardAtLane(lane) != null && GetPlayerCardAtLane(lane + 1) != null)
                    swappableLanes.Add(lane);

            if (swappableLanes.Count == 0) yield break;

            int swapLane = swappableLanes[rng.Next(swappableLanes.Count)];
            var a = GetPlayerCardViewAtLane(swapLane);
            var b = GetPlayerCardViewAtLane(swapLane + 1);

            // Stesso percorso dello scambio per trascinamento: il giocatore
            // subisce questo cambiamento, quindi deve almeno vederlo succedere.
            if (!AnimateLaneSwap(a, b)) yield break;

            a?.instance?.PushHint("SPOSTATA");
            b?.instance?.PushHint("SPOSTATA");
            Logger.Info($"Caos: la corsia {swapLane + 1} si scambia con la {swapLane + 2}");
            swapsDone++;
            yield return new WaitForSeconds(0.35f);
        }
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

    public int CountEnemyFaction(Faction faction)
    {
        int count = 0;
        for (int lane = 0; lane < aiBoardRoot.childCount; lane++)
            if (GetEnemySlotAtLane(lane)?.def.faction == faction) count++;
        return count;
    }

    void ApplyRollReward()
    {
        var a = GetEnemySlotAtLane(0);
        var b = GetEnemySlotAtLane(1);
        var c = GetEnemySlotAtLane(2);
        bool leftPair  = a != null && b != null && a.def.faction == b.def.faction;
        bool rightPair = b != null && c != null && b.def.faction == c.def.faction;
        bool pair = leftPair || rightPair;
        bool triple = pair && a != null && c != null && a.def.faction == c.def.faction;
        RollSummary = "Abbina carta e slot: +1 ATK o BLOCCO";

        // Le caselle che hanno fatto combinazione: la cassa del rullo le
        // illumina, cosi la vincita si vede sulla macchina e non solo nel testo.
        RollPayoutLanes = triple ? 0b111 : leftPair ? 0b011 : rightPair ? 0b110 : 0;
        RollPayoutJackpot = triple;
        if (pair) RollPayoutSerial++;

        if (!pair) return;
        int gained = GainPlayerAP(1, "Combo rullo");
        RollSummary = (triple ? "TRIS" : "COPPIA ADIACENTE") + "  +" + gained + " AP";
        if (triple)
        {
            foreach (var card in GetOrderedPlayerCards())
                if (card.side == Side.Retro) card.GainCharge(1);
            RollSummary += " / +1 carica ai Retro";
        }
        Logger.Info("Rullo: " + RollSummary);
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
        if (IsGameOver()) EndMatch();
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

        if (!awaitingEndTurn)
        {
            // Passing gives up our attacks, never the enemy response.
            StartCoroutine(ResolveAttackRoutine(playerAttacks: false, rollAfter: true));
            return;
        }
        AccumulateFlipCharges();

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

        yield return RandomizePlayerBoard();
        if (IsGameOver()) { EndMatch(); yield break; }
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
        btnAttack.interactable = on && CanAttack;
        btnEndTurn.interactable = on && !matchEnded;
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
                SpawnEmptySlotAt();
                continue;
            }

            var prefab = prefabs[lane];

            // La casella torna in campo dalla SUA riga del pool: stessa vita
            // residua, stesso numero, stesse statistiche scalate dalla
            // difficolta'. Passando invece da SlotDefinition.BuildSpec() —
            // com'era — ogni giro rimetteva in campo una casella nuova di
            // fabbrica: le ferite del turno prima svanivano, il numero stampato
            // spariva e la manopola della difficolta' non toccava nessuno.
            var entry = bossPool.Of(prefab);
            if (entry != null)
            {
                if (!entry.alive) { SpawnEmptySlotAt(); continue; }
                AddSlotFromTemplate(ai, entry.spec, prefab, aiBoardRoot, enemySlotViews, entry);
                continue;
            }

            var definition = prefab.GetComponent<SlotDefinition>();
            if (definition == null) continue;
            AddSlotFromTemplate(ai, definition.BuildSpec(), prefab, aiBoardRoot, enemySlotViews);
        }
    }

    /// <summary>Un buco nella corazza: la corsia mostra la casella vuota.</summary>
    void SpawnEmptySlotAt()
    {
        if (EmptySlot == null) return;
        var empty = Instantiate(EmptySlot, aiBoardRoot);
        empty.name = EmptySlot.name;
        empty.SetActive(true);
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

        var card = new CardInstance(definition.BuildSpec(), rng) { side = Side.Fronte };
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
