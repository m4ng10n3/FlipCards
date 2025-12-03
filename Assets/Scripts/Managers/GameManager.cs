using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [Serializable] public class PrefabCardBinding { public GameObject prefab; [Min(1)] public int count = 1; }
    [Serializable] public class PrefabSlotBinding { public GameObject prefab; [Min(1)] public int count = 1; }

    [Header("Roots")] public Transform playerBoardRoot; public Transform aiBoardRoot;
    [Header("UI")] public Button btnAttack; public Button btnEndTurn; 
    [Header("LOG")] public Text logText; static readonly StringBuilder _logBuf = new StringBuilder(4096);

    [Header("HUD")]
    public Text hpText;
    public Text apText;
    public Text EnemyHptxt;


    [Header("Match parameters")] public int turns = 10; public int playerBaseAP = 3; public int seed = 12345;
    [Header("Start constraints")]
    [Min(1)] public int CardsPerSide = 3;
    [Min(1)] public int StartingHandSize = 3;

    [Header("Refs")]
    [SerializeField] private HandManager handManager;
    public HandManager HandManager => handManager;

    [Header("Empty Spot")] public GameObject EmptySpot;
    [Header("Empty Slot")] public GameObject EmptySlot;

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

    Transform playerBoardRootClone;
    public Transform PlayerBoardRootClone => playerBoardRootClone;

    void Awake()
    {
        Logger.SetSink(AppendLog);
        EventBus.Publish(GameEventType.Info, new EventContext { phase = "GameManager ready" });
        _instance = this;
        if (handManager == null) throw new InvalidOperationException("HandManager missing");

        btnAttack.onClick.AddListener(OnAttack);
        btnEndTurn.onClick.AddListener(OnEndTurn);
    }

    void Start()
    {
        rng = new System.Random(seed);
        player = new PlayerState("Player", playerBaseAP);
        ai = new PlayerState("AI", 0);

        ClearChildrenUnder(playerBoardRoot);

        SpawnInitialEmptySpots();
        var cloneGO = Instantiate(playerBoardRoot.gameObject, playerBoardRoot.parent);
        cloneGO.name = $"{playerBoardRoot.name}_Clone";
        cloneGO.transform.SetSiblingIndex(playerBoardRoot.GetSiblingIndex());
        playerBoardRootClone = cloneGO.transform;

        for (int i = 0; i < playerBoardRootClone.childCount; i++)
        {
            var child = playerBoardRootClone.GetChild(i);
            if (child.gameObject.name != EmptySpot.name) continue;
            child.GetComponent<Image>().enabled = false;
        }

        SpawnEnemySlots();

        SpawnStartingHand();

        EventBus.Publish(GameEventType.Info, new EventContext { phase = "=== MATCH START ===" });

        UpdateAllViews();
        UpdateHUD();
        StartTurn(player, ai, true);
    }

    void SpawnStartingHand()
    {
        player.actionPoints = StartingHandSize;
        for (int i = 0; i < StartingHandSize; i++) handManager.DrawCard();
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
            outline.enabled = false;
            outline.effectDistance = new Vector2(5f, 5f);
            outline.useGraphicAlpha = false;
            outline.effectColor = Color.white;

            var btn = spotGO.GetComponent<Button>();
            var t = spotGO.transform;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnEmptySpotClicked(t));
        }
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

    void SpawnEnemySlots()
    {
        enemySlotViews.Clear();
        var toKill = new List<GameObject>();
        foreach (Transform t in aiBoardRoot) toKill.Add(t.gameObject);
        for (int i = 0; i < toKill.Count; i++) Destroy(toKill[i]);
        slotViewByInstance.Clear();

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
    public void UpdateAllViews()
    {
        var viewsSnapshot = viewByInstance.Values.ToList();
        foreach (var v in viewsSnapshot)
        {
            if (v.instance != null && !v.instance.alive)
            {
                RemoveCard(v.owner, v.instance);
            }
        }

        foreach (var v in viewByInstance.Values)
            v.Refresh();

        for (int i = enemySlotViews.Count - 1; i >= 0; i--)
        {
            var sv = enemySlotViews[i];
            if (!sv.instance.alive)
            {
                RemoveSlotView(sv);
                continue;
            }
            sv.Refresh();
        }
    }

public void UpdateHUD()
    {
        if (matchEnded) return;
        var enable = playerPhase && !awaitingEndTurn;
        btnAttack.interactable = enable;
        handManager.btnDraw.interactable = enable;

        hpText.text = $"{player.hp}";
        apText.text = $"{player.actionPoints}/{playerBaseAP}";
        EnemyHptxt.text = $"{ai.hp}";
    }
    
    void StartTurn(PlayerState owner, PlayerState opponent, bool isPlayerPhase)
    {
        playerPhase = isPlayerPhase;
        awaitingEndTurn = false;
        owner.actionPoints = owner == player ? playerBaseAP : 0;
        EventBus.Publish(GameEventType.TurnStart, new EventContext { owner = owner, opponent = opponent, phase = $"TURN {currentTurn}" });
        if (playerPhase) RandomizePlayerLayoutAndSides(); else ExecuteAiTurnStartActions();
        UpdateAllViews();
        UpdateHUD();
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

    public void OnCardDoubleClicked(CardView view)
    {
        TryFlipCard(view);
    }

    private bool TryFlipCard(CardView view)
    {
        if (awaitingEndTurn || matchEnded || !playerPhase) { UpdateHUD(); return false; }
        if (player.actionPoints <= 0) { EventBus.Publish(GameEventType.Info, new EventContext { phase = "Not enough Player PA" }); UpdateHUD(); return false; }

        if (view == null || view.instance == null || view.owner != player)
            return false;

        view.instance.Flip();
        player.actionPoints -= 1;

        EventBus.Publish(GameEventType.Flip, new EventContext
        {
            owner = player,
            opponent = ai,
            source = view.instance
        });

        UpdateAllViews();
        UpdateHUD();
        return true;
    }
    void OnAttack()
    {
        if (awaitingEndTurn || matchEnded || !playerPhase) { UpdateHUD(); return; }

        int lanes = playerBoardRoot.childCount;
        for (int lane = 0; lane < lanes; lane++)
        {
            var pView = playerBoardRoot.GetChild(lane).GetComponentInChildren<CardView>(false);
            if (pView == null || pView.instance == null) continue;

            var ci = pView.instance;
            if (!ci.alive || ci.side != Side.Fronte) continue;

            SlotView sView = null;
            if (lane < aiBoardRoot.childCount)
                sView = aiBoardRoot.GetChild(lane).GetComponentInChildren<SlotView>(false);

            if (sView == null || !sView.instance.alive)
            {
                int dmg = ci.def.frontDamage;
                ci.DealDamageToPlayer(player, ai, dmg, "DirectToEnemy");
            }
            else
            {
                var si = sView.instance;
                if (!si.alive) continue;

                ci.Attack(player, ai, si);
            }
        }

        CleanupDestroyedSlots();
        UpdateAllViews();
        awaitingEndTurn = true; 
        UpdateHUD();
    }

    public void SwapCardPositions(CardView a, CardView b)
    {
        if (matchEnded || awaitingEndTurn || !playerPhase) { UpdateHUD(); return; }
        if (a == null || b == null || a == b || player.actionPoints <= 0)
            return;

        var cA = a.PlayerBoardContainer != null
            ? a.PlayerBoardContainer
            : a.EnsurePlayerBoardContainer(playerBoardRoot);

        var cB = b.PlayerBoardContainer != null
            ? b.PlayerBoardContainer
            : b.EnsurePlayerBoardContainer(playerBoardRoot);

        if (cA == null || cB == null || cA == cB)
            return;

        if (cA.parent != playerBoardRoot || cB.parent != playerBoardRoot)
            return;

        int idxA = cA.GetSiblingIndex();
        int idxB = cB.GetSiblingIndex();

        Vector3 posA = cA.localPosition;
        Vector3 posB = cB.localPosition;
        Quaternion rotA = cA.localRotation;
        Quaternion rotB = cB.localRotation;

        player.actionPoints -= 1;

        cA.SetSiblingIndex(idxB);
        cB.SetSiblingIndex(idxA);

        a.UpdateBoardContainerTarget(posB, rotB);
        b.UpdateBoardContainerTarget(posA, rotA);

        EventBus.Publish(GameEventType.Info, new EventContext
        {
            owner = player,
            opponent = ai,
            source = a.instance,
            target = b.instance,
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

    void ExecuteAiTurnStartActions() { ApplyEnemySlotsEffectsLeftToRight(); }

    void ApplyEnemySlotsEffectsLeftToRight()
    {
        int lanes = Mathf.Min(aiBoardRoot.childCount, playerBoardRoot.childCount);
        var aiChildren = new Transform[lanes];
        for (int i = 0; i < lanes; i++) aiChildren[i] = aiBoardRoot.GetChild(i);

        for (int lane = 0; lane < lanes; lane++)
        {
            var sView = aiChildren[lane].GetComponentInChildren<SlotView>(false);
            if (sView == null || !sView.instance.alive)
                continue;

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



    void RemoveCard(PlayerState owner, CardInstance ci)
    {
        if (abilitiesByInstance.TryGetValue(ci, out var list))
        {
            for (int i = 0; i < list.Count; i++) list[i].Unbind();
            abilitiesByInstance.Remove(ci);
        }

        var view = viewByInstance[ci];

        var sel = SelectionManager.Instance;
        if (sel != null && sel.SelectedOwned == view) sel.SelectOwned(null);

        Transform parent = view.transform.parent;
        int laneIndex = view.transform.GetSiblingIndex();

        var boardContainer = view.PlayerBoardContainer;
        if (boardContainer != null && boardContainer.parent == playerBoardRoot)
        {
            parent = boardContainer.parent;
            laneIndex = boardContainer.GetSiblingIndex();
            Destroy(boardContainer.gameObject);
        }

        viewByInstance.Remove(ci);
        owner.board.Remove(ci);
        ci.Dispose();
        Destroy(view.gameObject);

        if (owner == player)
        {
            var spotGO = Instantiate(EmptySpot, parent);
            spotGO.name = EmptySpot.name;
            spotGO.SetActive(true);
            spotGO.transform.SetSiblingIndex(laneIndex);

            var outline = spotGO.GetComponent<Outline>();
            outline.enabled = false;
            outline.effectDistance = new Vector2(5f, 5f);
            outline.useGraphicAlpha = false;
            outline.effectColor = Color.white;

            var btn = spotGO.GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnEmptySpotClicked(spotGO.transform));
        }

        EventBus.Publish(GameEventType.Info, new EventContext
        {
            owner = owner,
            opponent = owner == player ? ai : player,
            source = ci,
            phase = "[GM] Removed destroyed card (replaced by EmptySpot if player)"
        });
    }


    void PlayCardFromHand(CardView handCard, Transform emptySpot)
    {
        var cd = handCard.GetComponent<CardDefinition>();
        var parent = emptySpot.parent;
        int laneIndex = emptySpot.GetSiblingIndex();

        Destroy(emptySpot.gameObject);

        var spec = cd.BuildSpec();
        var ci = new CardInstance(spec, rng);
        ci.AssignGM(this);
        player.board.Add(ci);

        GameObject go = Instantiate(handCard.gameObject, parent);
        go.name = handCard.gameObject.name;
        go.SetActive(true);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;

        var view = go.GetComponent<CardView>();
        view.Init(this, player, ci);
        view.EnsurePlayerBoardContainer(playerBoardRoot);
        view.PlayerBoardContainer.SetSiblingIndex(laneIndex);

        view.SetHighlight(false);
        viewByInstance[ci] = view;

        var opponent = ai;
        var abilities = go.GetComponents<AbilityBase>().ToList();
        foreach (var ab in abilities) ab.Bind(ci, player, opponent);
        abilitiesByInstance[ci] = abilities;

        handManager.OnHandCardDroppedToBoard(handCard);
        handManager.RemoveFromHand(handCard.gameObject);

        EventBus.Publish(GameEventType.CardPlayed, new EventContext
        {
            owner = player,
            opponent = ai,
            source = ci,
            phase = "FromHandToEmptySpot"
        });

        UpdateAllViews();
        UpdateHUD();
    }


    void CleanupDestroyedSlots()
    {
        for (int i = enemySlotViews.Count - 1; i >= 0; i--)
            if (!enemySlotViews[i].instance.alive) RemoveSlotView(enemySlotViews[i]);
    }

    

    void RemoveSlotView(SlotView v)
    {
        Transform parent = v.transform.parent;
        int laneIndex = v.transform.GetSiblingIndex();

        enemySlotViews.Remove(v);
        slotViewByInstance.Remove(v.instance);

        Destroy(v.gameObject);

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
        {
            var atkView = viewByInstance[card];
            Transform oppRoot = (atkView.owner == player) ? aiBoardRoot : playerBoardRoot;
            int lane = atkView.PlayerBoardContainer.transform.GetSiblingIndex();

            if (lane < 0 || lane >= oppRoot.childCount)
                return null;

            if (atkView.owner == player)
            {
                var sView = oppRoot.GetChild(lane).GetComponentInChildren<SlotView>(false);
                if (sView == null)
                    return null;

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
            if (cView == null)
                return null;

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

    public void OnCardClicked(CardView view)
    {
        if (matchEnded || view == null) return;

        bool isHandCard = view.owner == null && view.instance == null;
        if (isHandCard)
        {
            if (awaitingEndTurn || !playerPhase) { UpdateHUD(); return; }
            var emptySpot = SelectionManager.Instance != null ? SelectionManager.Instance.SelectedEmptySpot : null;
            if (emptySpot == null) return;

            PlayCardFromHand(view, emptySpot);
            SelectionManager.Instance?.SelectEmptySpot(null);
            return;
        }

        if (view.owner != player || view.instance == null) return;

        SelectionManager.Instance.SelectOwned(view);
    }



    public void OnEmptySpotClicked(Transform emptySpot)
    {
        if (matchEnded || emptySpot == null) return;

        if (SelectionManager.Instance != null)
            SelectionManager.Instance.SelectEmptySpot(emptySpot);
    }



}
