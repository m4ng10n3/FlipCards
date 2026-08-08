using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
/// L'area e' ancorata al fondo e cresce verso l'alto: da sollevata deve arrivare a
/// contenere le carte, altrimenti l'uscita scatterebbe appena il puntatore sale.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class HandTray : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Riferimenti")]
    public RectTransform handRoot;

    [Header("Quote")]
    public float restY;
    public float raisedY;
    public float restHeight;
    public float raisedHeight;

    [Header("Animazione")]
    public float duration = 0.18f;
    public Ease ease = Ease.OutCubic;

    RectTransform _rt;
    Tween _tween;
    bool _raised;

    public bool IsRaised => _raised;

    void Awake()
    {
        _rt = (RectTransform)transform;
        Apply(false, immediate: true);
    }

    public void OnPointerEnter(PointerEventData eventData) => Apply(true);

    public void OnPointerExit(PointerEventData eventData)
    {
        // Durante il trascinamento di una carta il puntatore esce dall'area per
        // portarla sulla corsia: se la mano scendesse, la carta trascinata
        // seguirebbe il suo container fuori schermo.
        if (eventData != null && eventData.dragging) return;
        Apply(false);
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

        // L'area segue lo stato: da sollevata deve coprire anche le carte, o
        // il puntatore ne uscirebbe subito e la mano rimbalzerebbe.
        _rt.sizeDelta = new Vector2(_rt.sizeDelta.x, raised ? raisedHeight : restHeight);
    }

    /// <summary>Forza la discesa: serve a fine drag, quando il puntatore e' altrove.</summary>
    public void Lower() => Apply(false);
}
