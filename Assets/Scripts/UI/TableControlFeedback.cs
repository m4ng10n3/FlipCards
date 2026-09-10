using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Risposta alla pressione di un comando fisico. Gli ascoltatori restano i
/// bottoni veri del gioco: qui non si decide niente, si fa solo vedere.
///
/// Col <see cref="cap"/> assegnato il cappello scende lungo lo stelo e il
/// basamento resta fermo, come chiede
/// <c>SceneKit_v1/Layout/animation_spec.md</c>. Senza, si ripiega su una
/// riduzione di scala: e' il caso dei comandi disegnati in un pezzo solo.
/// </summary>
public sealed class TableControlFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Tooltip("Ripiego: l'intera grafica si rimpicciolisce.")]
    public RectTransform visual;
    [Tooltip("Cappello del fungo. Se assegnato, e' l'unica cosa che si muove.")]
    public RectTransform cap;
    [Tooltip("Corsa del cappello in pixel.")]
    public float travel = 9f;

    bool _pressed;
    float _capRestY;
    bool _capCached;
    UnityEngine.UI.Button _button;
    UnityEngine.UI.Image _capImage;

    void Awake()
    {
        _button = GetComponent<UnityEngine.UI.Button>();
        if (cap != null) _capImage = cap.GetComponent<UnityEngine.UI.Image>();
    }

    public void OnPointerDown(PointerEventData e) { _pressed = true; }
    public void OnPointerUp(PointerEventData e) { _pressed = false; }
    public void OnPointerExit(PointerEventData e) { _pressed = false; }

    void Update()
    {
        if (cap != null)
        {
            if (!_capCached) { _capRestY = cap.anchoredPosition.y; _capCached = true; }

            // Il tint di stato del Button colpisce solo la targetGraphic: senza
            // questo, a comando disabilitato il basamento si spegne e il
            // cappello resta acceso, cioe' il bottone sembra rotto e non spento.
            if (_capImage != null && _button != null)
                _capImage.color = _button.interactable ? Color.white : _button.colors.disabledColor;
            // Discesa piu' rapida della risalita: 80 ms contro 100, come da specifica.
            float speed = _pressed ? 1f / 0.08f : 1f / 0.10f;
            float target = _capRestY - (_pressed ? travel : 0f);
            var p = cap.anchoredPosition;
            p.y = Mathf.MoveTowards(p.y, target, travel * speed * Time.unscaledDeltaTime);
            cap.anchoredPosition = p;
            return;
        }

        if (visual != null)
            visual.localScale = Vector3.Lerp(visual.localScale, Vector3.one * (_pressed ? .94f : 1f), Time.unscaledDeltaTime * 24);
    }

    void OnDisable()
    {
        _pressed = false;
        if (cap != null && _capCached)
            cap.anchoredPosition = new Vector2(cap.anchoredPosition.x, _capRestY);
    }
}
