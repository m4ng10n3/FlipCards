using DG.Tweening;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;
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
    [Header("Hover Activation Thresholds")]
    [SerializeField] private float maxLocalRotationAngle = 40f;

    private Sprite frontImage;

    [Header("MoveInHand Parameters")]
    [SerializeField] private bool MoveInHandAnimations = true;
    [SerializeField] private float MoveInHandRotationAngle = 30;
    [SerializeField] private float MoveInHandTransition = .15f;
    [SerializeField] private int MoveInHandVibration = 5;

    [Header("Scale Parameters")]
    [SerializeField] private bool scaleAnimations = true;
    [SerializeField] private float scaleOnHover = 1.15f;
    [SerializeField] private float scaleOnSelect = 1.25f;
    [SerializeField] private float scaleTransition = .15f;
    [SerializeField] private Ease scaleEase = Ease.OutBack;

    [Header("Select Parameters")]
    [SerializeField] private float selectPunchAmount = 20;

    [Header("Hover Parameters")]
    [SerializeField] private float autoTiltAmount = 30;
    [SerializeField] private float manualTiltAmount = 20;
    [SerializeField] private float tiltSpeed = 20;

    [SerializeField] private float hoverPunchAngle = 5;
    [SerializeField] private float hoverTransition = .15f;

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

    private RectTransform _handContainer;
    private Tween _handMoveTween;
    private Quaternion _targetHandRotation = Quaternion.identity;
    private Vector3 _handCurveOffset = Vector3.zero;
    private Vector3? _dragTarget;
    private bool _hasDragTarget;
    private bool _requestReturnToHand;
    private bool _selected;
    private bool _hoverVisualActive;
    private bool _moveInHandRequested;
    private bool _moveInHandImmediate;

    private int savedIndex;

    private RectTransform _playerBoardContainer;
    private Tween _boardMoveTween;
    private Quaternion _targetBoardRotation = Quaternion.identity;
    private Tween _scaleTween;
    private Tween _hoverPunchTween;
    private Tween _selectTween;
    private Tween _moveInHandTween;

    public RectTransform RectTransform => _rt;
    public Canvas RootCanvas => _rootCanvas;
    public Canvas Canvas => _canvas;
    public bool IsDragging { get => _dragging; set { _dragging = value; } }
    public bool IsDraggingFromBoard => _draggingFromBoard;
    public bool IsDraggingHand => _draggingHand;
    public bool IsHovering { get => _hovering; set { _hovering = value; Debug.Log(name + $" hover state: {value}"); } }
    public bool RequestReturnToHand { get => _requestReturnToHand; set => _requestReturnToHand = value; }
    public bool Selected { get => _selected; set { if (_selected == value) return; _selected = value; } }
    public bool MoveInHandRequest { get => _moveInHandRequested; set { _moveInHandRequested = value; } }

    public RectTransform handContainer { get => _handContainer; set => _handContainer = value; }
    public Vector3? dragTarget { get => _dragTarget; set => _dragTarget = value; }

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
        highlight = GetComponent<Outline>();
        _canvas = GetComponent<Canvas>() ?? gameObject.AddComponent<Canvas>();
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
        KillAllTweens();
        ReleaseHandContainer();
        ReleaseBoardContainer();
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

    public void EvaluateHandCurve(out Vector3 positionOffset, out Quaternion rotation)
    {
        positionOffset = Vector3.zero;
        rotation = Quaternion.identity;

        if (curveParameters == null || _handContainer == null || _handContainer.parent == null)
            return;

        var parent = _handContainer.parent as RectTransform;
        int slotCount = parent != null ? parent.childCount : _handContainer.parent.childCount;
        int slotIndex = _handContainer.GetSiblingIndex();
        float normalized = slotCount <= 1 ? 0.5f : (float)slotIndex / (slotCount - 1);

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
        if (_handContainer == null && _playerBoardContainer == null) 
            return Quaternion.identity;
        if (_handContainer  != null && _rt.IsChildOf(_handContainer))
        {
            return _handContainer.parent.rotation;
        }
        else
        {
            return _playerBoardContainer.parent.rotation;
        }
    }

    private void Update()
    {
        if (_rt == null) return;

        var anchorRotation = GetAnchorRotation();
        bool inHand = _handContainer != null && _rt.IsChildOf(_handContainer);
        bool isLastInHand = false;
        if (inHand)
        {
            var parent = _handContainer.parent as RectTransform;
            if (parent != null) isLastInHand = _handContainer.GetSiblingIndex() == parent.childCount - 1;
        }
        bool doHover = _hovering && !_moveInHandRequested && !_dragging && (!_selected ? (!inHand || isLastInHand) : true);
        if (_handContainer != null && !_dragging && _rt.IsChildOf(_handContainer))
            UpdateHandContainerTarget();

        if (_dragging && _dragTarget != null)
        {
            if (_handContainer != null) _draggingHand = _rt.IsChildOf(_handContainer);
            else _draggingHand = false;
            if (_playerBoardContainer != null) _draggingFromBoard = _rt.IsChildOf(_playerBoardContainer);
            else _draggingFromBoard = false;
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = 10;
            _rt.position = Vector3.Lerp(_rt.position, _dragTarget.Value, Mathf.Clamp01(handFollowSpeed * Time.deltaTime));
        }
        else if (doHover)
        {
            FollowContainer();
            HoverMotion(anchorRotation);
        }
        else if (_moveInHandRequested)
        {
            MoveInHand(_moveInHandImmediate);
        }
        else
        {
            _canvas.overrideSorting = false;
            FollowContainer();
        }
        if (!doHover) CardTilt(anchorRotation);
    }
    private void HoverMotion(Quaternion anchorRotation)
    {
        var baseRotation = anchorRotation;

        if (_handContainer != null && _rt.IsChildOf(_handContainer)) baseRotation *= _targetHandRotation;
        else if (_playerBoardContainer!= null && _rt.IsChildOf(_playerBoardContainer)) baseRotation *= _targetBoardRotation;

        float tiltX = 0f;
        float tiltY = 0f;
        float tiltZ = 0f;

        Vector2 screenPos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
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
        Vector3 currentLocal = (Quaternion.Inverse(baseRotation) * _rt.rotation).eulerAngles;

        float lerpX = Mathf.LerpAngle(currentLocal.x, tiltX, tiltSpeed * Time.deltaTime);
        float lerpY = Mathf.LerpAngle(currentLocal.y, tiltY, tiltSpeed * Time.deltaTime);
        float lerpZ = Mathf.LerpAngle(currentLocal.z, tiltZ, (tiltSpeed * 0.5f) * Time.deltaTime);

        var targetRot = baseRotation * Quaternion.Euler(lerpX, lerpY, lerpZ);
        _rt.rotation = Quaternion.Lerp(_rt.rotation, targetRot, handFollowRotationSpeed * Time.deltaTime);
        float clampAng = Quaternion.Angle(baseRotation, _rt.rotation);
        if (clampAng > maxLocalRotationAngle)
        {
            float t = maxLocalRotationAngle / Mathf.Max(clampAng, 0.0001f);
            _rt.rotation = Quaternion.Slerp(baseRotation, _rt.rotation, t);
        }
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

        float tiltX = 0f;
        float tiltY = 0f;
        float tiltZ = 0f;

        tiltX = sine * autoTiltAmount;
        tiltY = cosine * autoTiltAmount;

        Vector3 currentLocal = (Quaternion.Inverse(baseRotation) * _rt.rotation).eulerAngles;

        float lerpX = Mathf.LerpAngle(currentLocal.x, tiltX, tiltSpeed * Time.deltaTime);
        float lerpY = Mathf.LerpAngle(currentLocal.y, tiltY, tiltSpeed * Time.deltaTime);
        float lerpZ = Mathf.LerpAngle(currentLocal.z, tiltZ, (tiltSpeed * 0.5f) * Time.deltaTime);

        var targetRot = baseRotation * Quaternion.Euler(lerpX, lerpY, lerpZ);
        _rt.rotation = Quaternion.Lerp(_rt.rotation, targetRot, handFollowRotationSpeed * Time.deltaTime);
        float clampAng = Quaternion.Angle(baseRotation, _rt.rotation);
        if (clampAng > maxLocalRotationAngle)
        {
            float t = maxLocalRotationAngle / Mathf.Max(clampAng, 0.0001f);
            _rt.rotation = Quaternion.Slerp(baseRotation, _rt.rotation, t);
        }
    }

    private void FollowContainer()
    {
        if (_playerBoardContainer == null && _handContainer == null )
            return;

        var targetPosLocal = Vector3.zero;

        bool inHand = _handContainer != null && _rt != null && _rt.IsChildOf(_handContainer);
        if (inHand)
            targetPosLocal = _handCurveOffset;

        if (_selected)
            targetPosLocal += Vector3.up * selectPunchAmount;

        bool handTweenActive = _handMoveTween != null && _handMoveTween.IsActive() && _handMoveTween.IsPlaying();
        if (!handTweenActive)
            _rt.localPosition = Vector3.Lerp(_rt.localPosition, targetPosLocal, handFollowSpeed * Time.deltaTime);
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

    public RectTransform EnsureHandContainer(Transform parent)
    {
        if (_handContainer == null)
        {
            var go = new GameObject("HandContainer", typeof(RectTransform));
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
            _handContainer.gameObject.name = "HandContainer";
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

    public void MoveInHand(bool immediate = false)
    {
        if (_handContainer == null || _rt == null || !_moveInHandRequested)
            return;

        UpdateHandContainerTarget();
        KillHandTweens();

        var targetPos = _handCurveOffset + (_selected ? Vector3.up * selectPunchAmount : Vector3.zero);

        if (immediate || handTweenDuration <= 0f)
        {
            _rt.localPosition = targetPos;
        }
        else
        {
            _handMoveTween = _rt
                .DOLocalMove(targetPos, handTweenDuration)
                .SetEase(handTweenEase)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        if (MoveInHandAnimations)
        {
            KillTween(ref _moveInHandTween);
            _moveInHandTween = _rt
                .DOPunchRotation(Vector3.forward * MoveInHandRotationAngle, MoveInHandTransition, MoveInHandVibration, 1)
                .SetUpdate(true);
        }

        _moveInHandRequested = false;
        _moveInHandImmediate = false;
    }

    public void ApplySelect(bool state)
    {
        if (_rt == null) return;

        Selected = state;

        if (_selectTween != null && _selectTween.IsActive()) _selectTween.Complete(true);
        KillTween(ref _selectTween);
        if (state)
        {
            _selectTween = _rt
                .DOPunchPosition(_rt.up * selectPunchAmount, scaleTransition, 10, 1)
                .SetUpdate(true);
        }
            
        float targetScale = 1f;
        if (state) targetScale = scaleOnSelect;
        else if (_hovering) targetScale = scaleOnHover;

        if (scaleAnimations)
        {
            if (_scaleTween != null && _scaleTween.IsActive()) _scaleTween.Complete(true);
            KillTween(ref _scaleTween);
            _scaleTween = transform.DOScale(targetScale, scaleTransition)
                .SetEase(scaleEase)
                .SetUpdate(true);
        }
    }

    public void ApplyPointerEnter()
    {
        if (_rt == null) return;

        if (_handContainer != null && _rt.IsChildOf(_handContainer))
        {
            UpdateHandContainerTarget();
        }

        if (scaleAnimations)
        {
            float targetScale = _selected ? scaleOnSelect : scaleOnHover;
            if (_scaleTween != null && _scaleTween.IsActive()) _scaleTween.Complete(true);
            KillTween(ref _scaleTween);
            _scaleTween = transform.DOScale(targetScale, scaleTransition)
                .SetEase(scaleEase)
                .SetUpdate(true);
        }

        if (_hoverPunchTween != null && _hoverPunchTween.IsActive()) _hoverPunchTween.Complete(true);
        KillTween(ref _hoverPunchTween);
        _hoverPunchTween = _rt
            .DOPunchRotation(Vector3.forward * hoverPunchAngle, hoverTransition, 20, 1)
            .SetUpdate(true);
    }

    public void ResetHoverVisual()
    {
        if (!scaleAnimations) return;

        float targetScale = _selected ? scaleOnSelect : 1f;
        if (_scaleTween != null && _scaleTween.IsActive()) _scaleTween.Complete(true);
        KillTween(ref _scaleTween);
        _scaleTween = transform.DOScale(targetScale, scaleTransition)
            .SetEase(scaleEase)
            .SetUpdate(true);
    }


    public void UpdateHandContainerTarget()
    {
        if (_handContainer == null)
            return;

        EvaluateHandCurve(out var positionOffset, out var rotation);
        _handCurveOffset = positionOffset;
        _targetHandRotation = rotation;
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

    private void KillTween(ref Tween t, bool complete = false)
    {
        if (t != null)
        {
            t.Kill(complete);
            t = null;
        }
    }

    void OnTransformParentChanged()
    {
        CacheRootCanvas();
    }

    void OnDisable()
    {
        if (_hovering)
            _hovering = false;
    }

    private void KillAllTweens()
    {
        _rt?.DOKill();
        transform.DOKill(); // ensure lingering tweens tied to this GameObject are removed
        KillTween(ref _scaleTween);
        KillTween(ref _hoverPunchTween);
        KillTween(ref _selectTween);
        KillTween(ref _moveInHandTween);
        KillHandTweens();
        KillBoardTweens();
    }
}
