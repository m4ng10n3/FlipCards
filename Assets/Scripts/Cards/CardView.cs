using DG.Tweening;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
[RequireComponent(typeof(RectTransform))]

public class CardView : MonoBehaviour
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
    [Header("Flip Animation")]
    [SerializeField] private float flipDuration = 0.25f;
    [SerializeField] private Ease flipEase = Ease.InOutQuad;
    [Header("Rotation Parameters")]
    [SerializeField] private float autoTiltAmount = 30;
    [SerializeField] private float manualTiltAmount = 20;
    [SerializeField] private float tiltSpeed = 20;

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

    private Outline highlight;
    private int _lastHp = int.MinValue;
    private Canvas _rootCanvas;
    private RectTransform _rt;
    private Canvas _canvas;
    private bool _dragging;
    private bool _hovering;
    private bool _draggingHand;
    private bool _draggingFromBoard;
    private CurveParameters _lastCurveAsset;
    private int _lastCurveVersion = -1;

    private RectTransform _handContainer;
    private Tween _handMoveTween;
    private Quaternion _targetHandRotation = Quaternion.identity;
    private Vector3 _dragTargetWorld;
    private bool _hasDragTarget;

    private int savedIndex;

    private RectTransform _playerBoardContainer;
    private Tween _boardMoveTween;
    private Quaternion _targetBoardRotation = Quaternion.identity;

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
        highlight = GetComponent<Outline>();
        _canvas = GetComponent<Canvas>() ?? gameObject.AddComponent<Canvas>();
        SetHoverState(false);
        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
        CacheRootCanvas();

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

        Template.GetComponent<Image>().preserveAspect = false;
        Template.GetComponent<Image>().useSpriteMesh = false;
        Template.GetComponent<Image>().maskable = false;

        frontImage = Template.GetComponent<Image>().sprite;

        Refresh();
        hintText.gameObject.SetActive(false);
        GetComponent<CardDefinition>()?.BindRuntime(gm, owner, instance, this);
    }

    void OnDestroy()
    {
        ReleaseHandContainer();
        ReleaseBoardContainer();
    }

    public RectTransform RectTransform => _rt;
    public Canvas RootCanvas => _rootCanvas;
    public Canvas Canvas => _canvas;
    public bool IsDragging => _dragging;
    public bool IsDraggingFromBoard => _draggingFromBoard;
    public bool IsDraggingHand => _draggingHand;
    public bool IsHovering => _hovering;
    public bool HasDragTarget => _hasDragTarget;

    public void SetDraggingFlags(bool dragging, bool draggingHand, bool draggingFromBoard)
    {
        _dragging = dragging;
        _draggingHand = draggingHand;
        _draggingFromBoard = draggingFromBoard;
    }

    public void SetDragTargetWorld(Vector3 worldPosition, bool hasTarget = true)
    {
        _dragTargetWorld = worldPosition;
        _hasDragTarget = hasTarget;
    }

    public void ClearDragTarget()
    {
        _hasDragTarget = false;
    }

    public void SetCanvasSorting(bool enabled, int sortingOrder = 10)
    {
        if (_canvas == null) return;
        _canvas.overrideSorting = enabled;
        _canvas.sortingOrder = sortingOrder;
    }

    public void SetHoverState(bool hovering)
    {
        _hovering = hovering;
        if (gm == null) return;

        gm.hoveredCard = hovering ? this : null;
    }

    public bool TryScreenPointToWorldOnRoot(Vector2 screenPos, out Vector3 worldPoint)
    {
        worldPoint = _rt != null ? _rt.position : Vector3.zero;
        if (_rootCanvas == null) return false;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rootCanvas.transform as RectTransform,
                screenPos,
                _rootCanvas.worldCamera,
                out var localPoint))
        {
            worldPoint = _rootCanvas.transform.TransformPoint(localPoint);
            return true;
        }

        return false;
    }

    public void SetHighlight(bool setting)
    {
        highlight.enabled = setting;
    }
 
    private void CacheRootCanvas()
    {
        _rootCanvas = null;
        var canvases = GetComponentsInParent<Canvas>(includeInactive: true);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != _canvas)
            {
                _rootCanvas = canvases[i];
                break;
            }
        }

        if (_rootCanvas == null)
            _rootCanvas = _canvas;
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

    public void FlipSide(bool immediate = false)
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

    public void UpdateHpOnly()
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
    private Quaternion GetAnchorRotation()
    {
        if (_draggingHand)
            return _handContainer != null && _handContainer.parent != null ? _handContainer.parent.rotation : Quaternion.identity;

        if (_draggingFromBoard)
            return _playerBoardContainer != null && _playerBoardContainer.parent != null ? _playerBoardContainer.parent.rotation : Quaternion.identity;

        if (_handContainer != null && _rt != null && _rt.IsChildOf(_handContainer) && _handContainer.parent != null)
            return _handContainer.parent.rotation;

        if (_playerBoardContainer != null && _rt != null && _rt.IsChildOf(_playerBoardContainer) && _playerBoardContainer.parent != null)
            return _playerBoardContainer.parent.rotation;

        return _rt != null && _rt.parent != null ? _rt.parent.rotation : Quaternion.identity;
    }

    private void Update()
    {
        if (_rt == null) return;

        var anchorRotation = GetAnchorRotation();

        if (_dragging && _hasDragTarget)
        {
            _rt.position = Vector3.Lerp(_rt.position, _dragTargetWorld, Mathf.Clamp01(handFollowSpeed * Time.deltaTime));
        }
        else
        {
            FollowContainer();
        }

        CardTilt(anchorRotation);
    }

    private void CardTilt(Quaternion anchorRotation)
    {
        if (_rt == null || _rootCanvas == null)
            return;

        savedIndex = _dragging || _rt.parent == null ? savedIndex : _rt.parent.GetSiblingIndex();

        bool inHand = _draggingHand || (_handContainer != null && _rt.IsChildOf(_handContainer));
        bool inBoard = _draggingFromBoard || (_playerBoardContainer != null && _rt.IsChildOf(_playerBoardContainer));

        var baseRotation = anchorRotation;
        if (inHand) baseRotation *= _targetHandRotation;
        else if (inBoard) baseRotation *= _targetBoardRotation;

        float sine = Mathf.Sin(Time.time + savedIndex);
        float cosine = Mathf.Cos(Time.time + savedIndex);

        Vector2 screenPos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        float tiltX = 0f;
        float tiltY = 0f;
        float tiltZ = 0f;

        bool hoverActive = _hovering && !_dragging;

        if (hoverActive)
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

                var localAnchor = Quaternion.Inverse(parentTransform.rotation) * baseRotation;
                Vector3 axisX = localAnchor * Vector3.right;
                Vector3 axisY = localAnchor * Vector3.up;

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

        Vector3 currentLocal = (Quaternion.Inverse(baseRotation) * _rt.rotation).eulerAngles;

        float lerpX = Mathf.LerpAngle(currentLocal.x, tiltX, tiltSpeed * Time.deltaTime);
        float lerpY = Mathf.LerpAngle(currentLocal.y, tiltY, tiltSpeed * Time.deltaTime);
        float lerpZ = Mathf.LerpAngle(currentLocal.z, tiltZ, (tiltSpeed * 0.5f) * Time.deltaTime);

        var targetRot = baseRotation * Quaternion.Euler(lerpX, lerpY, lerpZ);
        _rt.rotation = Quaternion.Lerp(_rt.rotation, targetRot, handFollowRotationSpeed * Time.deltaTime);
    }

    private void FollowContainer()
    {
        if (_playerBoardContainer == null && _handContainer == null )
            return;

        var targetPosLocal = Vector3.zero;
        _rt.localPosition = Vector3.Lerp(_rt.localPosition,targetPosLocal,handFollowSpeed * Time.deltaTime);
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

    public RectTransform HandContainer => _handContainer;

    public RectTransform EnsureHandContainer(Transform parent)
    {
        if (_handContainer == null)
        {
            var go = new GameObject($"{name}_Container", typeof(RectTransform));
            _handContainer = go.GetComponent<RectTransform>();
            _handContainer.localScale = _rt.localScale;
            _handContainer.localRotation = Quaternion.identity;
            if (_rt != null)
            {
                var crt = GetComponent<RectTransform>().rect.size;
                _handContainer.sizeDelta = crt;

                _handContainer.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, crt.x);
                _handContainer.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, crt.y);
            }    
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

            if (!worldPositionStays)
            {
                var asRt = _rt as RectTransform;
                if (asRt != null)
                    asRt.anchoredPosition = Vector2.zero;
                else
                    _rt.localPosition = Vector3.zero;
            }
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

    void OnTransformParentChanged()
    {
        CacheRootCanvas();
    }

    void OnDisable()
    {
        if (gm?.hoveredCard == this)
            SetHoverState(false);
    }
}
