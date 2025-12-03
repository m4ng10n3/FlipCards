using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
[RequireComponent(typeof(RectTransform))]

public class CardView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Image Handling")]
    [Tooltip("Sprite del retro (assegnare come Source Image in Inspector)")]
    [SerializeField] private GameObject Template;
    [SerializeField] private Sprite backImage;
    [SerializeField] private Image artworkMonster;
    [SerializeField] private CurveParameters curveParameters;
    [Header("Hand Follow")]
    [SerializeField] private float handFollowSpeed = 18f;
    [SerializeField] private float handFollowRotationSpeed = 18f;
    [SerializeField] private float handTweenDuration = 0.2f;
    [SerializeField] private Ease handTweenEase = Ease.OutCubic;
    [Header("Drag Motion")]
    [SerializeField] private float dragRotationMagnitude = 6f;
    [SerializeField] private float dragMaxTilt = 60f;
    [SerializeField] private float dragTiltDistanceThreshold = 0.1f;
    [Header("Flip Animation")]
    [SerializeField] private float flipDuration = 0.25f;
    [SerializeField] private Ease flipEase = Ease.InOutQuad;
    [Header("Rotation Parameters")]
    [SerializeField] private float autoTiltAmount = 30;
    [SerializeField] private float manualTiltAmount = 20;
    [SerializeField] private float tiltSpeed = 20;

    [Header("Input System")]
    [SerializeField] private InputActionReference pointerPositionAction;

    private Sprite frontImage;

    [Header("Legacy UI Text (assign in prefab)")]
    public Text nameText;
    public Text factionText;
    public Text sideText;
    public Text hpText;
    public Text AttackPwrText;
    public Text BlockPwrText;

    [Header("Runtime wiring")]
    [HideInInspector] public GameManager gm;
    [HideInInspector] public PlayerState owner;
    [HideInInspector] public CardInstance instance { get; private set; }

    [SerializeField] private Text hintText;

    private Button btn;
    private Outline highlight;
    private int _lastHp = int.MinValue;
    private EventBus.Handler _evtHandler;
    private Canvas _rootCanvas;
    private RectTransform _rt;
    private Vector3 _dragStartPos;
    private bool _dragging;
    public bool _hovering;

    private bool _draggingFromBoard;
    private Transform _dragOriginalParent;

    private GameObject _dragPlaceholder;
    private GameObject _cloneEmptySpotDuringDrag;
    private Image _cloneEmptySpotImage;
    private int _dragOriginalSibling;
    private float _lastClickTime;
    private bool _draggingHand;
    private Vector3 _dragOriginalLocalScale;
    private Quaternion _dragOriginalLocalRotation;
    private const float DoubleClickThreshold = 0.3f;
    private static readonly List<RaycastResult> _raycastBuffer = new List<RaycastResult>(8);
    private CurveParameters _lastCurveAsset;
    private int _lastCurveVersion = -1;
    private float curveRotationOffset;

    private RectTransform _handContainer;
    private Tween _handMoveTween;
    private Quaternion _targetHandRotation = Quaternion.identity;
    private Vector3 _dragOriginalScale = Vector3.one;
    private RectTransform _dragContainer;
    private Vector3 _dragTargetWorld;
    private bool _hasDragTarget;
    private bool _returningToHand;

    private int savedIndex;

    private RectTransform _playerBoardContainer;
    private Tween _boardMoveTween;
    private Quaternion _targetBoardRotation = Quaternion.identity;

    private Quaternion _initialLocalRotation;

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _initialLocalRotation = _rt.localRotation;
        highlight = GetComponent<Outline>();

        _rootCanvas = GetComponentInParent<Canvas>();

        hintText.gameObject.SetActive(false);

        if (instance != null) return;

        var inline = GetComponent<CardDefinition>();
        if (inline == null) return;

        var def = inline.BuildSpec();

        nameText.text = def.cardName;
        factionText.text = def.faction.ToString();
        sideText.text = "Side";
        hpText.text = def.maxHealth.ToString();
        AttackPwrText.text = def.frontDamage.ToString();
        BlockPwrText.text = def.frontBlockValue.ToString();
    }

    public void Init(GameManager gm, PlayerState owner, CardInstance instance)
    {
        this.gm = gm;
        this.owner = owner;
        this.instance = instance;

        highlight = GetComponent<Outline>();
        highlight.effectDistance = new Vector2(5, 5);
        highlight.useGraphicAlpha = false;
        highlight.effectColor = Color.white;
        highlight.enabled = false;

        btn = GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnClicked);

        var bg = Template.GetComponent<Image>();
        if (btn.targetGraphic == null && bg != null) btn.targetGraphic = bg;

        Template.GetComponent<Image>().preserveAspect = false;
        Template.GetComponent<Image>().useSpriteMesh = false;
        Template.GetComponent<Image>().maskable = false;

        frontImage = Template.GetComponent<Image>().sprite;

        Refresh();
        hintText.gameObject.SetActive(false);

        _evtHandler = OnGameEvent;
        EventBus.Subscribe(GameEventType.AttackResolved, _evtHandler);
        EventBus.Subscribe(GameEventType.Flip, _evtHandler);
        EventBus.Subscribe(GameEventType.AttackDeclared, _evtHandler);
        EventBus.Subscribe(GameEventType.TurnEnd, _evtHandler);
        EventBus.Subscribe(GameEventType.Info, _evtHandler);
        EventBus.Subscribe(GameEventType.TurnStart, _evtHandler);
    }

    void OnDestroy()
    {
        UnsubscribeAllEvents();
        ReleaseHandContainer();
        ReleaseBoardContainer();
    }

    private void UnsubscribeAllEvents()
    {
        EventBus.Unsubscribe(GameEventType.AttackResolved, _evtHandler);
        EventBus.Unsubscribe(GameEventType.Flip, _evtHandler);
        EventBus.Unsubscribe(GameEventType.AttackDeclared, _evtHandler);
        EventBus.Unsubscribe(GameEventType.TurnEnd, _evtHandler);
        EventBus.Unsubscribe(GameEventType.Info, _evtHandler);
        EventBus.Unsubscribe(GameEventType.TurnStart, _evtHandler);
        _evtHandler = null;
    }

    public void OnClicked()
    {
        gm.OnCardClicked(this);
        if (IsBoardCard() && _lastClickTime > 0f && Time.time - _lastClickTime <= DoubleClickThreshold)
            gm.OnCardDoubleClicked(this);
        _lastClickTime = Time.time;
    }

    public void SetHighlight(bool setting)
    {
        highlight.enabled = setting;
    }

    public void Refresh()
    {
        var def = instance.def;

        nameText.text = def.cardName;
        factionText.text = def.faction.ToString();
        sideText.text = instance.side.ToString();
        hpText.text = instance.health + "";
        AttackPwrText.text = "" + def.frontDamage;
        BlockPwrText.text = "" + def.frontBlockValue;

        _lastHp = instance.health;

        FlipSide(immediate: true);
    }

    private void FlipSide(bool immediate = false)
    {
        if (immediate || flipDuration <= 0f || _rt == null || !Application.isPlaying)
        {
            ApplySideVisuals();
            return;
        }

        _rt.DOKill();

        Vector3 startEuler = _rt.localEulerAngles;
        float halfDuration = flipDuration * 0.5f;

        var seq = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject);

        seq.Append(
                _rt.DOLocalRotate(
                    new Vector3(startEuler.x, startEuler.y + 90f, startEuler.z),
                    halfDuration
                ).SetEase(flipEase)
            )
        .AppendCallback(ApplySideVisuals)
        .Append(
                _rt.DOLocalRotate(
                    startEuler,
                    halfDuration
                ).SetEase(flipEase)
            );
    }

    private void ApplySideVisuals()
    {
        bool isFront = instance.side == Side.Fronte;

        var img = Template.GetComponent<Image>();
        var newSprite = isFront ? frontImage : (backImage != null ? backImage : frontImage);
        img.type = Image.Type.Simple;
        img.preserveAspect = false;
        img.sprite = newSprite;

        highlight.effectColor = isFront ? Color.white : Color.white;
        nameText.enabled = isFront;
        hpText.enabled = isFront;
        AttackPwrText.enabled = isFront;
        BlockPwrText.enabled = isFront;
        artworkMonster.enabled = isFront;
        hintText.enabled = isFront;
    }

    void OnGameEvent(GameEventType t, EventContext ctx)
    {
        switch (t)
        {
            case GameEventType.AttackResolved:
                if (ctx.target == instance && ctx.amount > 0)
                {
                    ShowHint($"-{ctx.amount}HP");
                    UpdateHpOnly();
                    Blink();
                }
                if (ctx.source == instance && ctx.amount > 0)
                {
                    ShowHint($"Dealt {ctx.amount}");
                }
                break;

            case GameEventType.AttackDeclared:
                if (ctx.source == instance) ShowHint("Attack!");
                else if (ctx.target == instance) ShowHint("Under attack!");
                break;

            case GameEventType.TurnEnd:
                HideHint();
                break;

            case GameEventType.Info:
                if (ctx.source == instance && !string.IsNullOrEmpty(ctx.phase) && ctx.phase.StartsWith("HINT:"))
                    ShowHint(ctx.phase.Substring("HINT:".Length).Trim());
                break;

            case GameEventType.TurnStart:
                HideHint();
                break;

            case GameEventType.Flip:
                if (ctx.source == instance || ctx.target == instance)
                {
                    FlipSide();
                    Blink();
                }
                break;
        }
    }

    private void UpdateHpOnly()
    {
        hpText.text = instance.health + "";
        _lastHp = instance.health;
    }

    public void Blink() { StartCoroutine(BlinkRoutine()); }
    IEnumerator BlinkRoutine()
    {
        var c = Template.GetComponent<Image>().color;
        Template.GetComponent<Image>().color = Color.yellow;
        yield return new WaitForSeconds(0.08f);
        Template.GetComponent<Image>().color = c;
    }

    public void ShowHint(string msg)
    {
        hintText.gameObject.SetActive(true);
        hintText.text = string.IsNullOrEmpty(hintText.text) ? msg : hintText.text + "\n" + msg;
    }

    public void HideHint()
    {
        hintText.text = string.Empty;
        hintText.gameObject.SetActive(false);
    }

    private bool IsHandCard()
    {
        var handRoot = gm.HandManager.HandRoot;
        if (owner != null || instance != null)
            return false;

        bool isDirectChild = _rt.parent == handRoot;
        bool isContainerChild = _handContainer != null && _handContainer.parent == handRoot && _rt.parent == _handContainer;
        return isDirectChild || isContainerChild;
    }
    private bool IsBoardCard() => owner != null && instance != null;
    private bool CanDragBoardCard() =>
    IsBoardCard() &&
    _rt.IsChildOf(gm.playerBoardRoot);

    public void EvaluateHandCurve(float normalized, int slotCount, out Vector3 positionOffset, out Quaternion rotation)
    {
        positionOffset = Vector3.zero;
        rotation = Quaternion.identity;

        if (curveParameters == null)
            return;

        int siblings = Mathf.Max(0, slotCount);
        float yOff = curveParameters.positioning.Evaluate(normalized) * curveParameters.positioningInfluence * siblings;
        if (siblings < 5) yOff = 0f;
        positionOffset = Vector3.up * yOff;

        float centered = normalized - 0.5f;
        float symmetryT = Mathf.Clamp01(Mathf.Abs(centered) * 2f);
        float rotZ = Mathf.Sign(centered) * curveParameters.rotation.Evaluate(symmetryT) * curveParameters.rotationInfluence;
        rotation = Quaternion.Euler(0f, 0f, rotZ);
        curveRotationOffset = rotZ;
    }

    public bool ConsumeCurveDirtyFlag()
    {
        int currentVersion = curveParameters != null ? curveParameters.version : -1;
        bool changed = curveParameters != _lastCurveAsset || currentVersion != _lastCurveVersion;
        _lastCurveAsset = curveParameters;
        _lastCurveVersion = currentVersion;
        return changed;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_rt == null) return;

        _dragTargetWorld = _rt.position;
        if (_rootCanvas != null &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rootCanvas.transform as RectTransform,
                eventData.position,
                _rootCanvas.worldCamera,
                out var lp))
        {
            _dragTargetWorld = _rootCanvas.transform.TransformPoint(lp);
        }

        if (IsHandCard())
        {
            _dragging = true;
            _draggingHand = true;
            _draggingFromBoard = false;

            _dragStartPos = _rt.position;
            _dragOriginalParent = _rt.parent;
            _dragOriginalSibling = _rt.parent.GetSiblingIndex();
            _dragOriginalLocalScale = _rt.localScale;
            _dragOriginalLocalRotation = _rt.localRotation;
            _dragOriginalScale = _rt.localScale;

            _dragPlaceholder = null;
            _hasDragTarget = true;

            if (_handContainer != null)
                _handContainer.SetAsLastSibling();
            else
                _rt.SetAsLastSibling();

            gm?.HandManager?.OnHandCardBeginDrag(this, _handContainer);

            return;
        }

        if (!CanDragBoardCard()) return;
        if (_rootCanvas == null) return;

        _dragging = true;
        _draggingFromBoard = true;

        _dragStartPos = _rt.position;
        _dragOriginalParent = _rt.parent;
        _dragOriginalSibling = _rt.parent.GetSiblingIndex();
        _dragOriginalLocalScale = _rt.localScale;
        _dragOriginalLocalRotation = _rt.localRotation;

        ShowCloneEmptySpot();
        _hasDragTarget = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_dragging || _rt == null || _rootCanvas == null) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rootCanvas.transform as RectTransform,
                eventData.position,
                _rootCanvas.worldCamera,
                out var localPoint))
        {
            var worldPoint = _rootCanvas.transform.TransformPoint(localPoint);
            _dragTargetWorld = worldPoint;
            _hasDragTarget = true;

            if (_draggingHand)
                UpdateHandPlaceholderIndex();
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_dragging) return;

        _dragging = false;

        if (_draggingFromBoard)
        {
            HandleBoardDrop(eventData);
            _hasDragTarget = false;
            return;
        }

        HandleHandDrop(eventData);
        _hasDragTarget = false;
    }

    private void HandleHandDrop(PointerEventData eventData)
    {
        bool played = false;
        var spot = FindEmptySpotUnderPointer(eventData);
        if (spot != null && gm != null)
        {
            ClearDragPlaceholder();
            gm.OnEmptySpotClicked(spot);
            gm.OnCardClicked(this);
            played = true;
        }

        if (!played)
        {
            ReturnToHandSlot();
            _draggingHand = false;
            gm?.HandManager?.OnHandCardEndDrag(this);
        }
        else
        {
            _dragOriginalParent = null;
            _draggingHand = false;
            gm?.HandManager?.OnHandCardEndDrag(this);
        }

    }

    private void HandleBoardDrop(PointerEventData eventData)
    {
        HideCloneEmptySpot();
        Vector3 releaseWorldPos = _rt != null ? _rt.position : _dragStartPos;
        var target = FindBoardCardUnderPointer(eventData);
        RestoreDraggedBoardCard();
        if (target != null && gm != null)
        {
            gm.SwapCardPositions(this, target);
        }
        else if (_rt != null)
        {
            ReturnBoardCardToSlot(releaseWorldPos);
        }
        _draggingFromBoard = false;
    }

    private void RestoreDraggedBoardCard()
    {
        if (_draggingHand)
        {
            ReturnToHandSlot();
            return;
        }

        var dragTransform = _rt;

        if (dragTransform != null && _dragOriginalParent != null)
        {
            int insertIndex = _dragPlaceholder != null ? _dragPlaceholder.transform.GetSiblingIndex() : _dragOriginalSibling;
            insertIndex = Mathf.Clamp(insertIndex, 0, _dragOriginalParent.childCount);
            dragTransform.SetParent(_dragOriginalParent, false);
            dragTransform.SetSiblingIndex(insertIndex);
            dragTransform.localRotation = _draggingHand && _handContainer != null ? Quaternion.identity : _dragOriginalLocalRotation;

            if (_rt != null && _rt.parent != dragTransform)
                _rt.SetParent(dragTransform, true);

            _rt.localRotation = _dragOriginalLocalRotation;
            _rt.localScale = _dragOriginalLocalScale;
            var asRt = _rt as RectTransform;
            if (asRt != null) asRt.anchoredPosition = Vector2.zero;
        }

        if (_dragPlaceholder != null)
        {
            Destroy(_dragPlaceholder);
            _dragPlaceholder = null;
        }

        _dragOriginalParent = null;
    }

    private void ReturnBoardCardToSlot(Vector3 releaseWorldPos)
    {
        if (_rt == null) return;

    _rt.DOKill();

    float duration = Mathf.Max(0f, handTweenDuration);
    if (duration <= 0f)
    {
        _rt.position = _dragStartPos;
        _rt.localRotation = _dragOriginalLocalRotation;
        _rt.localScale = _dragOriginalLocalScale;
        return;
    }

    _rt.position = releaseWorldPos;

    var seq = DOTween.Sequence()
        .SetUpdate(true)
        .SetLink(gameObject);

    seq.Join(_rt.DOMove(_dragStartPos, duration).SetEase(handTweenEase));

    seq.OnComplete(() =>
    {
        if (_rt == null) return;
        _rt.localRotation = _dragOriginalLocalRotation;
        _rt.localScale = _dragOriginalLocalScale;
    });
    }

    private void ReturnToHandSlot()
    {
        if (_rt == null) return;

        var target = _handContainer != null ? _handContainer : _dragOriginalParent as RectTransform;
        if (target == null) return;

        _rt.DOKill();

        float duration = Mathf.Max(0f, handTweenDuration);
        var asRt = _rt as RectTransform;
        Vector3 targetWorldPos = target.position;
        Quaternion targetWorldRot = target.rotation;

        if (duration <= 0f)
        {
            _rt.position = targetWorldPos;
            _rt.rotation = targetWorldRot;
            _rt.SetParent(target, true);
            _rt.localRotation = Quaternion.identity;
            _rt.localScale = _dragOriginalScale;
            if (asRt != null)
                asRt.anchoredPosition = Vector2.zero;
            else
                _rt.localPosition = Vector3.zero;
            _returningToHand = false;
            return;
        }

        _returningToHand = true;

        var seq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        seq.Join(_rt.DOMove(targetWorldPos, duration).SetEase(handTweenEase));
        seq.Join(_rt.DORotateQuaternion(targetWorldRot, duration).SetEase(handTweenEase));

        seq.Join(_rt.DOScale(_dragOriginalScale, duration).SetEase(handTweenEase));

        seq.OnKill(() => _returningToHand = false);
        seq.OnComplete(() =>
        {
            if (_rt == null || target == null) return;

            _rt.SetParent(target, true);
            _rt.localRotation = Quaternion.identity;
            _rt.localScale = _dragOriginalScale;
            var rt = _rt as RectTransform;
            if (rt != null)
                rt.anchoredPosition = Vector2.zero;
            else
                _rt.localPosition = Vector3.zero;
            _returningToHand = false;
        });
    }

    private void ClearDragPlaceholder()
    {
        if (_dragPlaceholder != null)
        {
            Destroy(_dragPlaceholder);
            _dragPlaceholder = null;
        }

        if (_draggingHand && gm != null && gm.HandManager != null)
            StartCoroutine(RemoveHandCardAfterDrop());
    }

    private IEnumerator RemoveHandCardAfterDrop()
    {
        yield return null;

        var hand = gm != null ? gm.HandManager : null;
        if (hand == null || this == null)
            yield break;

        hand.OnHandCardDroppedToBoard(this);
        hand.RemoveFromHand(gameObject);

        if (this != null)
        {
            Destroy(gameObject);
            hand.UpdateCardsPosition();
        }
    }

    private void UpdateHandPlaceholderIndex()
    {
        if (!_draggingHand)
            return;

        gm.HandManager.ReorderHandDuringDrag(this, _rt.position);
    }

    private void ShowCloneEmptySpot()
    {
        var clone = gm.PlayerBoardRootClone;

        int index = _dragOriginalSibling;
        if (index < 0 || index >= clone.childCount) return;

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
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        for (int i = 0; i < results.Count; i++)
        {
            var view = results[i].gameObject.GetComponentInParent<CardView>();
            if (view != null && view != this && view.transform.IsChildOf(gm.playerBoardRoot))
                return view;
        }

        return null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovering = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovering = false;
    }
    private void FollowDragContainer()
    {
        if (_rt == null) return;

        Vector3 targetPos = _dragTargetWorld;

        float moveT = Mathf.Clamp01(handFollowSpeed * Time.deltaTime);
        _rt.position = Vector3.Lerp(_rt.position, targetPos, moveT);

        Vector3 remaining = targetPos - _rt.position;
        float distance = remaining.magnitude;

        Quaternion targetRot = Quaternion.identity;
        _rt.rotation = Quaternion.Lerp(_rt.rotation, targetRot, handFollowRotationSpeed * Time.deltaTime);
    }

    private void LateUpdate()
    {
        if (_dragging && _hasDragTarget) FollowDragContainer();
        else
        {
            FollowHandContainer();
            FollowBoardContainer();
        }
        CardTilt();
    }

    private void CardTilt()
    {
        if (_rt == null || _rootCanvas == null)
            return;

        savedIndex = _dragging ? savedIndex : _rt.parent.GetSiblingIndex();

        float sine = Mathf.Sin(Time.time + savedIndex);
        float cosine = Mathf.Cos(Time.time + savedIndex);

        Vector2 screenPos = Vector2.zero;

        if (Mouse.current != null)
        {
            screenPos = Mouse.current.position.ReadValue();
        }

        float tiltX = 0f;
        float tiltY = 0f;

        float tiltZ = curveRotationOffset;
        if (curveParameters != null && _rt.parent != null)
            tiltZ = curveRotationOffset *
                    (curveParameters.rotationInfluence * _rt.parent.childCount - 1);

        if (_hovering)
        {
            var canvasRect = _rootCanvas.transform as RectTransform;
            if (canvasRect != null &&
                RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    canvasRect,
                    screenPos,
                    _rootCanvas.worldCamera,
                    out var pointerWorld))
            {
                var parentTransform = _rt.parent != null ? _rt.parent : (Transform)_rt;

                Vector3 cardCenterWorld = _rt.position;

                Vector3 deltaParent =
                    parentTransform.InverseTransformPoint(pointerWorld) -
                    parentTransform.InverseTransformPoint(cardCenterWorld);

                Vector3 axisX = _initialLocalRotation * Vector3.right;
                Vector3 axisY = _initialLocalRotation * Vector3.up;

                float offsetX = Vector3.Dot(deltaParent, axisX);
                float offsetY = Vector3.Dot(deltaParent, axisY);

                Rect r = _rt.rect;
                float halfW = Mathf.Max(1f, r.width * 0.5f);
                float halfH = Mathf.Max(1f, r.height * 0.5f);

                float normX = Mathf.Clamp(offsetX / halfW, -1f, 1f);
                float normY = Mathf.Clamp(offsetY / halfH, -1f, 1f);

                tiltX = -normY * manualTiltAmount;
                tiltY = normX * manualTiltAmount;
            }
        }
        else
        {
            tiltX = sine * autoTiltAmount;
            tiltY = cosine * autoTiltAmount;
        }

        Vector3 current = transform.localEulerAngles;

        float lerpX = Mathf.LerpAngle(current.x, tiltX, tiltSpeed * Time.deltaTime);
        float lerpY = Mathf.LerpAngle(current.y, tiltY, tiltSpeed * Time.deltaTime);
        float lerpZ = Mathf.LerpAngle(current.z, tiltZ, (tiltSpeed * 0.5f) * Time.deltaTime);

        transform.localEulerAngles = new Vector3(lerpX, lerpY, lerpZ);
    }

    private void FollowBoardContainer()
    {
        if (_playerBoardContainer == null || _rt == null || _draggingFromBoard || _draggingHand)
            return;

        var targetPosLocal = Vector3.zero;
        _rt.localPosition = Vector3.Lerp(
            _rt.localPosition,
            targetPosLocal,
            handFollowSpeed * Time.deltaTime
        );

        var parentRot = _playerBoardContainer.parent != null
            ? _playerBoardContainer.parent.rotation
            : Quaternion.identity;

        var targetRot = parentRot * _targetBoardRotation;
        _rt.rotation = Quaternion.Lerp(
            _rt.rotation,
            targetRot,
            handFollowRotationSpeed * Time.deltaTime
        );
    }
    public RectTransform PlayerBoardContainer => _playerBoardContainer;

    public RectTransform EnsurePlayerBoardContainer(Transform parent)
    {
        if (_playerBoardContainer == null)
        {
            var go = new GameObject($"{name}_BoardContainer", typeof(RectTransform));
            _playerBoardContainer = go.GetComponent<RectTransform>();
            _playerBoardContainer.localScale = Vector3.one;
            _playerBoardContainer.localRotation = Quaternion.identity;
        }

        if (parent != null && _playerBoardContainer.parent != parent)
            _playerBoardContainer.SetParent(parent, false);

        if (_rt != null)
        {
            _playerBoardContainer.position = _rt.position;
            _playerBoardContainer.rotation = Quaternion.identity;
            var crt = GetComponent<RectTransform>().rect.size;
            _playerBoardContainer.sizeDelta = crt;

            _playerBoardContainer.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, crt.x);
            _playerBoardContainer.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, crt.y);

            if (_rt.parent != _playerBoardContainer && !_draggingFromBoard)
            {
                _rt.SetParent(_playerBoardContainer, true);
                _rt.localPosition = Vector3.zero;
            }
        }

        return _playerBoardContainer;
    }

    public void UpdateBoardContainerTarget(Vector3 localPosition, Quaternion localRotation)
    {
        if (_playerBoardContainer == null)
            return;

        _targetBoardRotation = localRotation;
        KillBoardTweens();

        if (handTweenDuration <= 0f)
        {
            _playerBoardContainer.localPosition = localPosition;
            _playerBoardContainer.localRotation = Quaternion.identity;
            return;
        }

        _boardMoveTween = _playerBoardContainer
            .DOLocalMove(localPosition, handTweenDuration)
            .SetEase(handTweenEase)
            .SetUpdate(true)
            .SetLink(gameObject);

        _playerBoardContainer.localRotation = Quaternion.identity;
    }

    public void ReleaseBoardContainer()
    {
        if (_playerBoardContainer != null)
        {
            KillBoardTweens();
            Destroy(_playerBoardContainer.gameObject);
            _playerBoardContainer = null;
        }
    }

    private void KillBoardTweens()
    {
        if (_boardMoveTween != null)
        {
            _boardMoveTween.Kill();
            _boardMoveTween = null;
        }
    }

    private void FollowHandContainer()
    {
        if (_handContainer == null || _rt == null || _draggingHand || _returningToHand)
            return;

        var targetPosLocal = Vector3.zero;
        _rt.localPosition = Vector3.Lerp(_rt.localPosition, targetPosLocal, handFollowSpeed * Time.deltaTime);

        var parentRot = _handContainer.parent != null ? _handContainer.parent.rotation : Quaternion.identity;
        var targetRot = parentRot * _targetHandRotation;
        _rt.rotation = Quaternion.Lerp(_rt.rotation, targetRot, handFollowRotationSpeed * Time.deltaTime);
    }

    public RectTransform HandContainer => _handContainer;

    public RectTransform EnsureHandContainer(Transform parent)
    {
        if (_handContainer == null)
        {
            var go = new GameObject($"{name}_Container", typeof(RectTransform));
            _handContainer = go.GetComponent<RectTransform>();
            _handContainer.localScale = Vector3.one;
            _handContainer.localRotation = Quaternion.identity;
            if (_rt != null)
                _handContainer.sizeDelta = _rt.rect.size;
        }

        if (parent != null && _handContainer.parent != parent)
            _handContainer.SetParent(parent, false);

        if (_rt != null)
        {
            _handContainer.position = _rt.position;
            _handContainer.rotation = Quaternion.identity;

            if (_rt.parent != _handContainer && !_draggingHand)
            {
                _rt.SetParent(_handContainer, true);
                _rt.localPosition = Vector3.zero;
            }
        }

        return _handContainer;
    }

    public void SetHandContainer(RectTransform container, bool reparent, bool worldPositionStays = true)
    {
        if (container == null)
            return;

        _handContainer = container;

        if (reparent && _rt != null)
        {
            _rt.SetParent(_handContainer, worldPositionStays);
            _rt.localRotation = Quaternion.identity;

            var asRt = _rt as RectTransform;
            if (asRt != null)
                asRt.anchoredPosition = Vector2.zero;
            else
                _rt.localPosition = Vector3.zero;
        }
    }

    public void UpdateHandContainerTarget(Vector3 localPosition, Quaternion localRotation)
    {
        if (_handContainer == null)
            return;

        _targetHandRotation = localRotation;
        KillHandTweens();

        if (handTweenDuration <= 0f)
        {
            _handContainer.localPosition = localPosition;
            _handContainer.localRotation = Quaternion.identity;
            return;
        }

        _handMoveTween = _handContainer.DOLocalMove(localPosition, handTweenDuration)
        .SetEase(handTweenEase)
        .SetUpdate(true)
        .SetLink(gameObject);

        _handContainer.localRotation = Quaternion.identity;
    }

    public void ReleaseHandContainer()
    {
        if (_handContainer != null)
        {
            KillHandTweens();
            Destroy(_handContainer.gameObject);
            _handContainer = null;
        }
    }

    private void KillHandTweens()
    {
        if (_handMoveTween != null)
        {
            _handMoveTween.Kill();
            _handMoveTween = null;
        }
    }
}

