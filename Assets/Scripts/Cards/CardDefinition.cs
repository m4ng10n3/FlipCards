using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class CardDefinition : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [System.Serializable]
    public struct Spec
    {
        // Identit
        public string cardName;
        public Faction faction;
        public CardZone zone;
        // Stats base
        public int maxHealth;

        // Fronte
        public int frontDamage;
        public int frontBlockValue;

        // Retro (passivi)
        public int backDamageBonusSameFaction;
        public int backBlockBonusSameFaction;
        public int backBonusPAIfTwoRetroSameFaction;
        // dentro Spec
        public float endTurnFlipChance;

        public override string ToString() => $"{cardName} [{faction}]";
    }

    [Header("Identity")]
    public string cardName = "Card";
    public Faction faction = Faction.A;

    public CardZone zone = CardZone.Board;


    [Header("Stats")]
    [Min(1)] public int maxHealth = 3;

    [Header("Front")]
    [Min(0)] public int frontDamage = 2;
    [Min(0)] public int frontBlockValue = 0;

    [Header("Back (passive)")]
    [Min(0)] public int backDamageBonusSameFaction = 0;
    [Min(0)] public int backBlockBonusSameFaction = 0;
    [Min(0)] public int backBonusPAIfTwoRetroSameFaction = 0;

    [Header("Behaviour")]
    [Range(0, 1f)] public float endTurnFlipChance = 0.3f;

    [Header("Runtime")]
    [SerializeField] private CardView cardView;

    private const float DoubleClickThreshold = 0.3f;
    private GameManager gm;
    private PlayerState owner;
    private CardInstance instance;
    private EventBus.Handler _evtHandler;
    private GameObject _cloneEmptySpotDuringDrag;
    private Image _cloneEmptySpotImage;
    private int _dragOriginalSibling;
    private float _lastClickTime;
    private static readonly List<RaycastResult> _raycastBuffer = new List<RaycastResult>(8);

    // Ex-BuildRuntimeDefinition: ora ritorna la Spec senza creare ScriptableObject
    public Spec BuildSpec()
    {
        return new Spec
        {
            cardName = cardName,
            faction = faction,
            maxHealth = maxHealth,
            frontDamage = frontDamage,
            frontBlockValue = frontBlockValue,
            backDamageBonusSameFaction = backDamageBonusSameFaction,
            backBlockBonusSameFaction = backBlockBonusSameFaction,
            backBonusPAIfTwoRetroSameFaction = backBonusPAIfTwoRetroSameFaction,
            endTurnFlipChance = endTurnFlipChance,
            zone = zone
        };
    }

    private void Awake()
    {
        if (cardView == null)
            cardView = GetComponent<CardView>() ?? GetComponentInChildren<CardView>();

        if (cardView == null)
            Debug.LogError("CardDefinition richiede un CardView nello stesso prefab", this);
    }

    public void BindRuntime(GameManager gm, PlayerState owner, CardInstance instance, CardView view)
    {
        cardView = view ?? cardView ?? GetComponent<CardView>() ?? GetComponentInChildren<CardView>();
        this.gm = gm ?? cardView?.gm;
        this.owner = owner ?? cardView?.owner;
        this.instance = instance ?? cardView?.instance;

        UnsubscribeAllEvents();
        if (this.instance != null)
        {
            _evtHandler = OnGameEvent;
            EventBus.Subscribe(GameEventType.AttackResolved, _evtHandler);
            EventBus.Subscribe(GameEventType.Flip, _evtHandler);
            EventBus.Subscribe(GameEventType.AttackDeclared, _evtHandler);
            EventBus.Subscribe(GameEventType.TurnEnd, _evtHandler);
            EventBus.Subscribe(GameEventType.Info, _evtHandler);
            EventBus.Subscribe(GameEventType.TurnStart, _evtHandler);
        }
    }

    private void OnDestroy()
    {
        UnsubscribeAllEvents();
    }

    private void UnsubscribeAllEvents()
    {
        if (_evtHandler == null) return;
        EventBus.Unsubscribe(GameEventType.AttackResolved, _evtHandler);
        EventBus.Unsubscribe(GameEventType.Flip, _evtHandler);
        EventBus.Unsubscribe(GameEventType.AttackDeclared, _evtHandler);
        EventBus.Unsubscribe(GameEventType.TurnEnd, _evtHandler);
        EventBus.Unsubscribe(GameEventType.Info, _evtHandler);
        EventBus.Unsubscribe(GameEventType.TurnStart, _evtHandler);
        _evtHandler = null;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        LogPointerEvent("PointerDown", eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        LogPointerEvent("PointerUp", eventData);
        if (cardView.IsDragging)
        {
            OnEndDrag(eventData);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        LogPointerEvent("BeginDrag", eventData);
        EnsureRuntimeRefs();
        if (cardView == null || cardView.IsDragging) return;
        if (cardView.RectTransform == null) throw new System.InvalidOperationException("CardView missing RectTransform");
        if (cardView.RootCanvas == null) throw new System.InvalidOperationException("CardView missing Canvas");

        Vector3 worldPoint = cardView.RectTransform != null ? cardView.RectTransform.position : Vector3.zero;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                cardView.RootCanvas.transform as RectTransform,
                eventData.position,
                cardView.RootCanvas.worldCamera,
                out var localPoint))
        {
            worldPoint = cardView.RootCanvas.transform.TransformPoint(localPoint);
        }

        cardView.dragTarget = worldPoint;
        cardView.IsDragging = true;

        if (IsHandCard())
        {
            if (cardView.handContainer == null) throw new System.InvalidOperationException("Hand container not created");

            gm.HandManager.OnHandCardBeginDrag(cardView);
            return;
        }


        _dragOriginalSibling = cardView.RectTransform.parent.GetSiblingIndex();
        ShowCloneEmptySpot();
    }

    public void OnDrag(PointerEventData eventData)
    {
        LogPointerEvent("Drag", eventData);
        EnsureRuntimeRefs();
        if (cardView == null || !cardView.IsDragging || cardView.RectTransform == null) return;

        Vector3 worldPoint = cardView.RectTransform != null ? cardView.RectTransform.position : Vector3.zero;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                cardView.RootCanvas.transform as RectTransform,
                eventData.position,
                cardView.RootCanvas.worldCamera,
                out var localPoint))
        {
            worldPoint = cardView.RootCanvas.transform.TransformPoint(localPoint);
        }

        cardView.dragTarget = worldPoint;
        cardView.IsDragging = true;

        if (cardView.IsDraggingHand && gm != null && gm.HandManager != null)
            gm.HandManager.ReorderHandDuringDrag(cardView, cardView.RectTransform.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        LogPointerEvent("EndDrag", eventData);
        EnsureRuntimeRefs();
        if (cardView == null || !cardView.IsDragging) return;

        if (cardView.IsDraggingFromBoard)
            HandleBoardDrop(eventData);
        else if (cardView.IsDraggingHand)
            HandleHandDrop(eventData);

        cardView.dragTarget = null;
        cardView.IsDragging = false;

        if (cardView.handContainer != null && cardView.handContainer.parent == gm?.HandManager?.HandRoot)
            cardView.RequestReturnToHand = true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        LogPointerEvent("PointerEnter", eventData);

        EnsureRuntimeRefs();
        if (cardView == null) return;
        cardView.IsHovering = true;
        cardView.ApplyPointerEnter();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        LogPointerEvent("PointerExit", eventData);
        EnsureRuntimeRefs();
        if (cardView == null) return;
        cardView.IsHovering = false;
        cardView.ResetHoverVisual();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        LogPointerEvent("PointerClick", eventData);
        EnsureRuntimeRefs();
        if (cardView == null) return;
        if (cardView.IsDragging || (eventData != null && eventData.dragging)) return;
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;
        if (gm == null) return;

        gm.OnCardClicked(cardView);
        if (IsBoardCard() && _lastClickTime > 0f && Time.time - _lastClickTime <= DoubleClickThreshold)
            gm.OnCardDoubleClicked(cardView);
        _lastClickTime = Time.time;
        cardView.ApplySelect(!cardView.Selected);
    }

    private void HandleHandDrop(PointerEventData eventData)
    {
        if (gm == null || cardView == null) return;

        var spot = FindEmptySpotUnderPointer(eventData);
        if (spot != null)
        {
            gm.OnEmptySpotClicked(spot);
            gm.OnCardClicked(cardView);
        }

        gm.HandManager.OnHandCardEndDrag(cardView);
    }

    private void HandleBoardDrop(PointerEventData eventData)
    {
        HideCloneEmptySpot();
        var target = FindBoardCardUnderPointer(eventData);
        if (target != null && gm != null)
        {
            gm.SwapCardPositions(cardView, target);
        }
    }

    private void ShowCloneEmptySpot()
    {
        if (gm == null || cardView == null) return;
        var clone = gm.PlayerBoardRootClone;

        int index = _dragOriginalSibling;
        if (clone == null || index < 0 || index >= clone.childCount) return;

        var spot = clone.GetChild(index);
        if (spot == null) return;

        var spotGO = spot.gameObject;
        if (gm.EmptySpot != null && spotGO.name != gm.EmptySpot.name) return;

        var spotImage = spotGO.GetComponent<Image>();
        if (spotImage == null) return;

        _cloneEmptySpotDuringDrag = spotGO;
        _cloneEmptySpotImage = spotImage;

        if (!_cloneEmptySpotDuringDrag.activeSelf)
            _cloneEmptySpotDuringDrag.SetActive(true);
        _cloneEmptySpotImage.enabled = true;
    }

    private void HideCloneEmptySpot()
    {
        if (_cloneEmptySpotImage != null)
            _cloneEmptySpotImage.enabled = false;
        _cloneEmptySpotDuringDrag = null;
        _cloneEmptySpotImage = null;
    }

    private Transform FindEmptySpotUnderPointer(PointerEventData eventData)
    {
        if (gm == null) return null;

        var boardRoot = gm.playerBoardRoot;
        var cloneRoot = gm.PlayerBoardRootClone;

        _raycastBuffer.Clear();
        EventSystem.current.RaycastAll(eventData, _raycastBuffer);
        for (int i = 0; i < _raycastBuffer.Count; i++)
        {
            var t = _raycastBuffer[i].gameObject.transform;
            while (t != null)
            {
                if (t.gameObject.name != gm.EmptySpot.name)
                {
                    t = t.parent;
                    continue;
                }

                if (cloneRoot != null && t.IsChildOf(cloneRoot))
                {
                    if (!t.gameObject.activeInHierarchy) break;

                    int idx = t.GetSiblingIndex();
                    if (boardRoot != null && idx < boardRoot.childCount)
                    {
                        var realSpot = boardRoot.GetChild(idx);
                        if (realSpot != null && realSpot.gameObject.activeInHierarchy && realSpot.gameObject.name == gm.EmptySpot.name)
                            return realSpot;
                    }
                }
                else if (boardRoot == null || t.IsChildOf(boardRoot))
                {
                    return t;
                }

                t = t.parent;
            }
        }
        return null;
    }

    private CardView FindBoardCardUnderPointer(PointerEventData eventData)
    {
        if (gm == null) return null;

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        for (int i = 0; i < results.Count; i++)
        {
            var view = results[i].gameObject.GetComponentInParent<CardView>();
            if (view != null && view != cardView && view.transform.IsChildOf(gm.playerBoardRoot))
                return view;
        }

        return null;
    }

    private void OnGameEvent(GameEventType t, EventContext ctx)
    {
        if (cardView == null) return;

        switch (t)
        {
            case GameEventType.AttackResolved:
                if (ctx.target == cardView.instance && ctx.amount > 0)
                {
                    cardView.ShowHint($"-{ctx.amount}HP");
                    cardView.UpdateHpOnly();
                    cardView.Blink();
                }
                if (ctx.source == cardView.instance && ctx.amount > 0)
                {
                    cardView.ShowHint($"Dealt {ctx.amount}");
                }
                break;

            case GameEventType.AttackDeclared:
                if (ctx.source == cardView.instance) cardView.ShowHint("Attack!");
                else if (ctx.target == cardView.instance) cardView.ShowHint("Under attack!");
                break;

            case GameEventType.TurnEnd:
                cardView.HideHint();
                break;

            case GameEventType.Info:
                if (ctx.source == cardView.instance && !string.IsNullOrEmpty(ctx.phase) && ctx.phase.StartsWith("HINT:"))
                    cardView.ShowHint(ctx.phase.Substring("HINT:".Length).Trim());
                break;

            case GameEventType.TurnStart:
                cardView.HideHint();
                break;

            case GameEventType.Flip:
                if (ctx.source == cardView.instance || ctx.target == cardView.instance)
                {
                    cardView.FlipSide();
                    cardView.Blink();
                }
                break;
        }
    }

    private bool IsHandCard()
    {
        if (cardView == null || gm == null) return false;
        if (owner != null || instance != null)
            return false;

        var handRoot = gm.HandManager.HandRoot;
        var rt = cardView.RectTransform;
        bool isDirectChild = rt != null && rt.parent == handRoot;
        bool isContainerChild = cardView.handContainer != null && cardView.handContainer.parent == handRoot && rt != null && rt.parent == cardView.handContainer;
        return isDirectChild || isContainerChild;
    }

    private bool IsBoardCard() => owner != null && instance != null;

    private void EnsureRuntimeRefs()
    {
        if (cardView == null) return;
        gm ??= cardView.gm;
        owner ??= cardView.owner;
        instance ??= cardView.instance;
    }

    private void LogPointerEvent(string evt, PointerEventData data)
    {
        if (data == null)
        {
            //Debug.Log($"[Input] {evt} on {gameObject.name} (no event data)");
            return;
        }

        //Debug.Log($"[Input] {evt} on {gameObject.name} pos:{data.position} btn:{data.button} dragging:{data.dragging} clickCount:{data.clickCount}");
    }
}
