using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using DG.Tweening;
[RequireComponent(typeof(RectTransform))]

public class CardView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Image Handling")]
    [Tooltip("Sprite del retro (assegnare come Source Image in Inspector)")]
    [SerializeField] private GameObject Template; // child con grafica fronte
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
    private CanvasGroup _canvasGroup;
    private RectTransform _rt;
    private Vector3 _dragStartPos;
    private bool _dragging;
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
    private RectTransform _handContainer;
    private Tween _handMoveTween;
    private Quaternion _targetHandRotation = Quaternion.identity;
    private Vector3 _dragOriginalScale = Vector3.one;
    private RectTransform _dragContainer;
    private Vector3 _dragTargetWorld;
    private bool _hasDragTarget;

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _rootCanvas = GetComponentInParent<Canvas>();
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // UI di base sempre sicura da fare in preview/editor
        if (hintText != null) hintText.gameObject.SetActive(false);

        // Se questa CardView e gia stata inizializzata a runtime, esci.
        if (instance != null) return;

        // Modalita "preview" (prefab in editor o scene senza runtime CardInstance)
        var inline = GetComponent<CardDefinition>();
        if (inline == null) return;

        var def = inline.BuildSpec();

        if (nameText != null) nameText.text = def.cardName;
        if (factionText != null) factionText.text = def.faction.ToString();
        if (sideText != null) sideText.text = "Side";
        if (hpText != null) hpText.text = def.maxHealth.ToString();
        if (AttackPwrText != null) AttackPwrText.text = def.frontDamage.ToString();
        if (BlockPwrText != null) BlockPwrText.text = def.frontBlockValue.ToString();
    }

    public void Init(GameManager gm, PlayerState owner, CardInstance instance)
    {
        // --- BIND RUNTIME ---
        this.gm = gm;
        this.owner = owner;
        this.instance = instance;

        // --- HIGHLIGHT (Outline) ---
        if (highlight == null) highlight = gameObject.AddComponent<Outline>();
        highlight.effectDistance = new Vector2(5, 5);
        highlight.useGraphicAlpha = false;        // evita che l'alpha/texture influenzi l'outline
        highlight.effectColor = Color.white;      // colore di default
        highlight.enabled = false;

        // --- BUTTON / CLICK ---
        btn = GetComponent<Button>() ?? gameObject.AddComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnClicked);

        // --- IMAGE DI SFONDO / SPRITE ---
        var bg = Template != null ? Template.GetComponent<Image>() : null;
        if (btn.targetGraphic == null && bg != null) btn.targetGraphic = bg;

        if (bg != null)
        {
            Template.GetComponent<Image>().preserveAspect = false;            // rispetta il RectTransform
            Template.GetComponent<Image>().useSpriteMesh = false;
            Template.GetComponent<Image>().maskable = false;

            // Il fronte e l'immagine impostata nel componente Image (Source Image)
            frontImage = Template.GetComponent<Image>().sprite;
        }

        // --- UI STATE ---
        Refresh();                                  // mostra lo stato reale dell'istanza
        if (hintText != null) hintText.gameObject.SetActive(false);

        // --- EVENTI ---
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
    }

    private void UnsubscribeAllEvents()
    {
        if (_evtHandler != null)
        {
            EventBus.Unsubscribe(GameEventType.AttackResolved, _evtHandler);
            EventBus.Unsubscribe(GameEventType.Flip, _evtHandler);
            EventBus.Unsubscribe(GameEventType.AttackDeclared, _evtHandler);
            EventBus.Unsubscribe(GameEventType.TurnEnd, _evtHandler);
            EventBus.Unsubscribe(GameEventType.Info, _evtHandler);
            EventBus.Unsubscribe(GameEventType.TurnStart, _evtHandler);
            _evtHandler = null;
        }
    }

    public void OnClicked()
    {
        if (gm != null)
        {
            gm.OnCardClicked(this);

            // doppio click per flippare le carte gia sul board
            if (IsBoardCard() && _lastClickTime > 0f && Time.time - _lastClickTime <= DoubleClickThreshold)
                gm.OnCardDoubleClicked(this);

            _lastClickTime = Time.time;
            return;
        }
        SetHighlight(highlight == null ? false : !highlight.enabled);
    }

    public void SetHighlight(bool setting)
    {
        if (highlight == null) return;
            highlight.enabled = setting;
    }

    public void Refresh()
    {
        if (instance == null) return;

        var def = instance.def;

        if (nameText != null) nameText.text = def.cardName;
        if (factionText != null) factionText.text = def.faction.ToString();
        if (sideText != null) sideText.text = instance.side.ToString();
        if (hpText != null) hpText.text = instance.health + "";
        if (AttackPwrText != null) AttackPwrText.text = "" + def.frontDamage;
        if (BlockPwrText != null) BlockPwrText.text = "" + def.frontBlockValue;

        _lastHp = instance.health;

        FlipSide();
    }

    private void FlipSide()
    {
        bool isFront = (instance != null && instance.side == Side.Fronte);

        // Cambia solo lo sprite (niente SetNativeSize / scale)
        var newSprite = isFront ? frontImage : (backImage != null ? backImage : frontImage);
        Template.GetComponent<Image>().type = Image.Type.Simple; // per sicurezza
        Template.GetComponent<Image>().preserveAspect = false;   // il RectTransform decide; cambia a true se vuoi letterboxing
        Template.GetComponent<Image>().sprite = newSprite;
        //img.useSpriteMesh = false;
        // Mostra/nascondi i testi a seconda del lato

        if (highlight != null)
            highlight.effectColor = isFront ? Color.white : Color.white; // retro nero => outline bianco

        if (nameText) nameText.enabled = isFront;
        if (hpText) hpText.enabled = isFront;
        if (AttackPwrText) AttackPwrText.enabled = isFront;
        if (BlockPwrText) BlockPwrText.enabled = isFront;
        if (artworkMonster) artworkMonster.enabled = isFront;
        if (hintText) hintText.enabled = isFront;
    }


    void OnGameEvent(GameEventType t, EventContext ctx)
    {
        if (instance == null) return;

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
        if (instance == null) return;
        if (hpText != null)
            hpText.text = instance.health + "";
        _lastHp = instance.health;
    }

    public void Blink() { StartCoroutine(BlinkRoutine()); }
    IEnumerator BlinkRoutine()
    {
        if (Template.GetComponent<Image>() == null) yield break;
        var c = Template.GetComponent<Image>().color;
        Template.GetComponent<Image>().color = Color.yellow;
        yield return new WaitForSeconds(0.08f);
        Template.GetComponent<Image>().color = c;
    }

    public void ShowHint(string msg)
    {
        if (hintText == null)
        {
            Logger.Info("[Card] " + msg);
            return;
        }

        hintText.gameObject.SetActive(true);
        hintText.text = string.IsNullOrEmpty(hintText.text) ? msg : hintText.text + "\n" + msg;
    }

    public void HideHint()
    {
        if (hintText != null)
        {
            hintText.text = string.Empty;
            hintText.gameObject.SetActive(false);
        }
    }

    // === Drag & Drop (mano -> tabellone) ===
    private bool IsHandCard()
    {
        var handRoot = gm != null ? gm.HandManager?.HandRoot : null;
        if (owner != null || instance != null || handRoot == null || _rt == null)
            return false;

        bool isDirectChild = _rt.parent == handRoot;
        bool isContainerChild = _handContainer != null && _handContainer.parent == handRoot && _rt.parent == _handContainer;
        return isDirectChild || isContainerChild;
    }
    private bool IsBoardCard() => owner != null && instance != null;
    private bool CanDragBoardCard() => gm != null && _rt != null && IsBoardCard() && _rt.parent == gm.playerBoardRoot;

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
        if (_rootCanvas != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(_rootCanvas.transform as RectTransform, eventData.position, _rootCanvas.worldCamera, out var lp))
            _dragTargetWorld = _rootCanvas.transform.TransformPoint(lp);

        if (IsHandCard())
        {
            _dragging = true;
            _draggingHand = true;
            _draggingFromBoard = false;
            _dragStartPos = _rt.position;
            _dragOriginalParent = _rt.parent;
            _dragOriginalSibling = _rt.GetSiblingIndex();
            _dragOriginalLocalScale = _rt.localScale;
            _dragOriginalLocalRotation = _rt.localRotation;
            _dragOriginalScale = _rt.localScale;
            _dragPlaceholder = null;
            SetupDragContainer();
            _hasDragTarget = true;
            if (_dragContainer != null)
                _dragContainer.SetAsLastSibling();
            ApplyDragPickupRotation();
            gm?.HandManager?.OnHandCardBeginDrag(this, _handContainer);
            _canvasGroup.blocksRaycasts = false; // consente di colpire lo spot sottostante
            return;
        }

        if (!CanDragBoardCard()) return;

        if (_rootCanvas == null) return;

        _dragging = true;
        _draggingFromBoard = true;
        _dragStartPos = _rt.position;
        _dragOriginalParent = _rt.parent;
        _dragOriginalSibling = _rt.GetSiblingIndex();
        _dragOriginalLocalScale = _rt.localScale;
        _dragOriginalLocalRotation = _rt.localRotation;
        _dragPlaceholder = CreateDragPlaceholder();
        ShowCloneEmptySpot();
        SetupDragContainer();
        _hasDragTarget = true;
        if (_dragContainer != null)
            _dragContainer.SetAsLastSibling();
        ApplyDragPickupRotation();
        _canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_dragging || _rt == null || _rootCanvas == null) return;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_rootCanvas.transform as RectTransform, eventData.position, _rootCanvas.worldCamera, out var localPoint))
        {
            var worldPoint = _rootCanvas.transform.TransformPoint(localPoint);
            _dragTargetWorld = worldPoint;
            _hasDragTarget = true;
            if (_dragContainer != null)
                _dragContainer.position = _dragTargetWorld;
            if (_draggingHand)
                UpdateHandPlaceholderIndex();
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_dragging) return;
        _dragging = false;
        _canvasGroup.blocksRaycasts = true;

        if (_draggingFromBoard)
        {
            HandleBoardDrop(eventData);
            CleanupDragContainer();
            _hasDragTarget = false;
            return;
        }

        HandleHandDrop(eventData);
        CleanupDragContainer();
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
        var target = FindBoardCardUnderPointer(eventData);
        RestoreDraggedBoardCard();

        if (target != null && gm != null)
        {
            gm.SwapCardPositions(this, target);
        }
        else if (_rt != null)
        {
            _rt.position = _dragStartPos;
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

    private void ReturnToHandSlot()
    {
        if (_rt == null) return;

        var target = _handContainer != null ? _handContainer : _dragOriginalParent as RectTransform;
        if (target == null) return;

        _rt.SetParent(target, true);
        _rt.localRotation = Quaternion.identity;
        _rt.localScale = _dragOriginalScale;

        var asRt = _rt as RectTransform;
        if (asRt != null)
            asRt.anchoredPosition = Vector2.zero;
        else
            _rt.localPosition = Vector3.zero;
    }

    private GameObject CreateDragPlaceholder()
    {
        if (_dragOriginalParent == null || _rt == null) return null;

        var go = new GameObject("CardPlaceholder");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(_dragOriginalParent, false);
        rt.SetSiblingIndex(_dragOriginalSibling);
        var reference = _draggingHand && _handContainer != null ? _handContainer : _rt;
        rt.sizeDelta = reference.rect.size;

        var srcLayout = reference.GetComponent<LayoutElement>();
        if (srcLayout != null)
        {
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = srcLayout.preferredWidth > 0 ? srcLayout.preferredWidth : rt.sizeDelta.x;
            le.preferredHeight = srcLayout.preferredHeight > 0 ? srcLayout.preferredHeight : rt.sizeDelta.y;
            le.flexibleWidth = srcLayout.flexibleWidth;
            le.flexibleHeight = srcLayout.flexibleHeight;
            le.minWidth = srcLayout.minWidth;
            le.minHeight = srcLayout.minHeight;
        }
        else
        {
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = rt.sizeDelta.x;
            le.preferredHeight = rt.sizeDelta.y;
        }

        return go;
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
        // Let the play logic clone the dragged card before destroying the hand copy.
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
        if (!_draggingHand || gm == null || _rt == null)
            return;

        gm.HandManager?.ReorderHandDuringDrag(this, _rt.position);
    }

    private void ShowCloneEmptySpot()
    {
        if (gm == null) return;
        var clone = gm.PlayerBoardRootClone;
        if (clone == null) return;

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
        if (EventSystem.current == null || gm == null || gm.EmptySpot == null) return null;

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
        if (EventSystem.current == null || gm == null || gm.playerBoardRoot == null) return null;

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        for (int i = 0; i < results.Count; i++)
        {
            var view = results[i].gameObject.GetComponentInParent<CardView>();
            if (view != null && view != this && view.transform.parent == gm.playerBoardRoot)
                return view;
        }

        return null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (IsHandCard())
            gm?.HandManager?.SetHoveredCard(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (IsHandCard())
            gm?.HandManager?.ClearHoveredCard(this);
    }

    private void ApplyDragPickupRotation()
    {
        if (_rt == null) return;

        Vector3 target = _dragContainer != null ? _dragContainer.position : _dragTargetWorld;
        float distance = Vector3.Distance(_rt.position, target);
        float sign = Mathf.Sign(target.x - _rt.position.x);
        if (Mathf.Approximately(sign, 0f))
            sign = 1f;

        float angle = Mathf.Clamp(distance * dragRotationMagnitude * sign, -dragMaxTilt, dragMaxTilt);
        _rt.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void FollowDragContainer()
    {
        if (_rt == null) return;

        if (_dragContainer != null)
            _dragContainer.position = _dragTargetWorld;

        Vector3 targetPos = _dragContainer != null ? _dragContainer.position : _dragTargetWorld;
        float moveT = Mathf.Clamp01(handFollowSpeed * Time.deltaTime);
        _rt.position = Vector3.Lerp(_rt.position, targetPos, moveT);

        Vector3 remaining = targetPos - _rt.position;
        float distance = remaining.magnitude;

        if (distance > dragTiltDistanceThreshold)
        {
            ApplyDragPickupRotation();
        }
        else
        {
            Quaternion targetRot = _dragContainer != null ? _dragContainer.rotation : Quaternion.identity;
            _rt.rotation = Quaternion.Lerp(_rt.rotation, targetRot, handFollowRotationSpeed * Time.deltaTime);
        }
    }

    private void LateUpdate()
    {
        FollowHandContainer();
        if (_dragging && _hasDragTarget)
            FollowDragContainer();
    }

    private void FollowHandContainer()
    {
        if (_handContainer == null || _rt == null || _draggingHand)
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

    private void SetupDragContainer()
    {
        if (_rootCanvas == null || _rt == null) return;

        if (_dragContainer == null)
        {
            var go = new GameObject($"{name}_DragContainer", typeof(RectTransform));
            _dragContainer = go.GetComponent<RectTransform>();
        }

        _dragContainer.SetParent(_rootCanvas.transform, true);
        _dragContainer.position = _dragTargetWorld;
        _dragContainer.rotation = Quaternion.identity;
        _dragContainer.localScale = Vector3.one;

        if (_rt.parent != _rootCanvas.transform)
            _rt.SetParent(_rootCanvas.transform, true);
        _rt.localRotation = Quaternion.identity;
    }

    private void CleanupDragContainer()
    {
        if (_dragContainer != null)
        {
            Destroy(_dragContainer.gameObject);
            _dragContainer = null;
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

