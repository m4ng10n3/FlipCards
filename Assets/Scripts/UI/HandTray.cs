using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// La mano vive quasi fuori dallo schermo e sale tutta insieme quando il
/// puntatore entra nella sua area, coprendo in parte le corsie del giocatore:
/// e' il momento in cui stai scegliendo cosa giocare, non in cui stai leggendo il
/// tavolo. Uscendo dall'area, torna giu'.
///
/// Il componente sta sull'**area di attivazione**, e la mano e' un suo figlio: se
/// fossero fratelli, passare il puntatore da un punto vuoto a una carta
/// genererebbe un PointerExit sull'area e la mano scenderebbe sotto le dita.
///
/// L'area di attivazione **non cresce**: resta la striscia in fondo allo schermo,
/// perche' la sua Image e' un Raycast Target e allargandola si stende un blocco
/// invisibile sopra le corsie. A tenere su la mano mentre si sceglie ci pensa
/// <see cref="ContainsPointer"/>, che misura le carte dove stanno adesso: fuori
/// dalle carte la mano scende, e con la mano vuota non sale affatto.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class HandTray : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Riferimenti")]
    public RectTransform handRoot;

    [Header("Quote")]
    public float restY;
    public float raisedY;
    [Tooltip("Altezza della striscia di richiamo in fondo allo schermo: e' l'unica " +
             "parte fissa dell'area. Il resto lo danno le carte, dove stanno adesso.")]
    public float restHeight;

    [Header("Animazione")]
    public float duration = 0.18f;
    public Ease ease = Ease.OutCubic;

    RectTransform _rt;
    Tween _tween;
    bool _raised;
    PointerEventData _pointer;
    float _outsideSince = -1f;

    public bool IsRaised => _raised;

    void Awake()
    {
        _rt = (RectTransform)transform;
        Apply(false, immediate: true);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _pointer = eventData;
        _outsideSince = -1f;
        // Con la mano vuota non c'e' niente da sollevare, e sollevarla vorrebbe
        // dire mettere un'area morta davanti alle corsie per niente.
        if (!AnyHandCard()) return;
        Apply(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Durante il trascinamento di una carta il puntatore esce dall'area per
        // portarla sulla corsia: se la mano scendesse, la carta trascinata
        // seguirebbe il suo container fuori schermo.
        if (eventData != null && eventData.dragging) return;
        if (eventData != null && ContainsPointer(eventData.position)) return;
        if (_outsideSince < 0f) _outsideSince = Time.unscaledTime;
    }

    /// <summary>
    /// L'area della mano non e' un rettangolo fisso: e' la **striscia di
    /// richiamo** in fondo allo schermo piu' le carte dove stanno adesso,
    /// sollevamento e ingrandimento compresi.
    ///
    /// Prima era il rect dell'area, che da sollevata arrivava a 440 e teneva su
    /// la mano anche sopra le carte, cioe' sopra le corsie: per toccare una
    /// carta in campo bisognava uscire sopra la corsia e rientrare. E con la
    /// mano vuota restava su un'area senza niente dentro.
    ///
    /// Si misura il **figlio grafico** (<see cref="CardView.RectTransform"/>) e
    /// non la radice: la radice sta ferma sul container, la grafica e' quella
    /// che si vede e che il puntatore sta seguendo. Niente `raycastPadding`,
    /// che e' piu' largo della carta apposta per non perdere l'hover: usarlo
    /// qui rimetterebbe l'alone davanti al tabellone.
    /// </summary>
    bool ContainsPointer(Vector2 position)
    {
        if (!AnyHandCard()) return false;

        var canvas = GetComponentInParent<Canvas>();
        var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        if (InEntryStrip(position, camera)) return true;

        foreach (Transform container in handRoot)
        {
            var view = container.GetComponentInChildren<CardView>();
            if (view == null || view.instance != null || view.RectTransform == null) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(view.RectTransform, position, camera)) return true;
        }
        return false;
    }

    /// <summary>La fascia bassa alta quanto la mano a riposo: e' cio' che fa salire la mano.</summary>
    bool InEntryStrip(Vector2 position, Camera camera)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, position, camera, out var local))
            return false;

        var r = _rt.rect;
        return local.x >= r.xMin && local.x <= r.xMax &&
               local.y >= r.yMin && local.y <= r.yMin + Mathf.Max(1f, restHeight);
    }

    bool AnyHandCard()
    {
        if (handRoot == null) return false;
        foreach (Transform container in handRoot)
        {
            var view = container.GetComponentInChildren<CardView>();
            if (view != null && view.instance == null) return true;
        }
        return false;
    }

    void LateUpdate()
    {
        if (!_raised || Mouse.current == null || (_pointer != null && _pointer.dragging)) return;
        if (ContainsPointer(Mouse.current.position.ReadValue())) { _outsideSince = -1f; return; }
        if (_outsideSince < 0f) _outsideSince = Time.unscaledTime;
        if (Time.unscaledTime - _outsideSince >= 0.08f) Apply(false);
    }

    void OnDisable()
    {
        _pointer = null;
        Apply(false, immediate: true);
    }

    void Apply(bool raised, bool immediate = false)
    {
        if (_raised == raised && !immediate) return;
        _raised = raised;

        if (handRoot == null) return;

        float targetY = raised ? raisedY : restY;

        _tween?.Kill();
        if (immediate || duration <= 0f)
            handRoot.anchoredPosition = new Vector2(handRoot.anchoredPosition.x, targetY);
        else
            _tween = handRoot.DOAnchorPosY(targetY, duration)
                             .SetEase(ease)
                             .SetUpdate(true)
                             .SetLink(gameObject);

        // Il rettangolo dell'area NON cresce con la mano, e non e' una svista.
        // L'Image dell'area e' un Raycast Target trasparente: portandola a
        // raisedHeight si stendeva un blocco invisibile alto 440 sopra la meta'
        // bassa delle corsie, e il clic sulle carte in campo finiva li' invece
        // che sulla carta. A tenere su la mano mentre si sceglie ci pensa
        // ContainsPointer, che misura le carte vere.
        _rt.sizeDelta = new Vector2(_rt.sizeDelta.x, restHeight);
    }

    /// <summary>Forza la discesa: serve a fine drag, quando il puntatore e' altrove.</summary>
    public void Lower() => Apply(false);
}
