using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class CardView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Image Handling")]
    [Tooltip("Sprite del retro (assegnare come Source Image in Inspector)")]
    [SerializeField] private GameObject Template; // child con grafica fronte
    [SerializeField] private Sprite backImage;
    [SerializeField] private Image artworkMonster;

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

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _rootCanvas = GetComponentInParent<Canvas>();
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // UI di base sempre sicura da fare in preview/editor
        if (hintText != null) hintText.gameObject.SetActive(false);

        // Se questa CardView è già stata inizializzata a runtime, esci.
        if (instance != null) return;

        // Modalità "preview" (prefab in editor o scene senza runtime CardInstance)
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
        highlight.useGraphicAlpha = false;        // evita che lalpha/texture influenzi loutline
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

            // Il fronte è l'immagine impostata nel componente Image (Source Image)
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

            // doppio click per flippare le carte già sul board
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
    private bool IsHandCard() => owner == null && instance == null && gm != null;
    private bool IsBoardCard() => owner != null && instance != null;
    private bool CanDragBoardCard() => gm != null && _rt != null && IsBoardCard() && _rt.parent == gm.playerBoardRoot;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_rt == null) return;

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
            _dragPlaceholder = CreateDragPlaceholder();
            if (_rootCanvas != null)
            {
                _rt.SetParent(_rootCanvas.transform, true);
                _rt.SetAsLastSibling();
            }
            gm?.HandManager?.OnHandCardBeginDrag(this, _dragPlaceholder != null ? _dragPlaceholder.transform : null);
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
        _rt.SetParent(_rootCanvas.transform, true);
        _rt.SetAsLastSibling();
        _canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_dragging || _rt == null || _rootCanvas == null) return;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_rootCanvas.transform as RectTransform, eventData.position, _rootCanvas.worldCamera, out var localPoint))
        {
            _rt.position = _rootCanvas.transform.TransformPoint(localPoint);
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
            return;
        }

        HandleHandDrop(eventData);
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
            RestoreDraggedBoardCard();
            gm?.HandManager?.OnHandCardEndDrag(this);
        }
        else
        {
            _dragOriginalParent = null;
            gm?.HandManager?.OnHandCardEndDrag(this);
        }

        _draggingHand = false;
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
        if (_rt != null && _dragOriginalParent != null)
        {
            int insertIndex = _dragPlaceholder != null ? _dragPlaceholder.transform.GetSiblingIndex() : _dragOriginalSibling;
            insertIndex = Mathf.Clamp(insertIndex, 0, _dragOriginalParent.childCount);
            _rt.SetParent(_dragOriginalParent, false);
            _rt.SetSiblingIndex(insertIndex);
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

    private GameObject CreateDragPlaceholder()
    {
        if (_dragOriginalParent == null || _rt == null) return null;

        var go = new GameObject("CardPlaceholder");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(_dragOriginalParent, false);
        rt.SetSiblingIndex(_dragOriginalSibling);
        rt.sizeDelta = _rt.rect.size;

        var srcLayout = _rt.GetComponent<LayoutElement>();
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
    }

    private void UpdateHandPlaceholderIndex()
    {
        if (!_draggingHand || _dragPlaceholder == null || _dragOriginalParent == null || _rt == null)
            return;

        gm?.HandManager?.ReorderHandDuringDrag(_rt, _dragPlaceholder.transform);
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
}
