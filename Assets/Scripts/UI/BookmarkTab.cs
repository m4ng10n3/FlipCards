using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Una linguetta d'indice. Esce un poco al passaggio del puntatore, resta piu'
/// fuori quando e' la sezione aperta, e trema appena se la si clicca da aperta.
/// La scritta si raddrizza quando la linguetta sta sul lato sinistro, dove il
/// foglio la mostra rovesciata.
/// </summary>
public sealed class BookmarkTab : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public RectTransform body;
    public TMP_Text label;
    public float tuck = 70f;
    public float hoverOut = 12f;
    public float activeOut = 20f;

    public bool Active { get; set; }
    bool _hover;
    float _out, _nudge;

    public void OnPointerEnter(PointerEventData e) => _hover = true;
    public void OnPointerExit(PointerEventData e) => _hover = false;

    public void Nudge() => _nudge = 1f;

    public void SetMirrored(bool mirrored)
    {
        if (label == null) return;
        var s = label.rectTransform.localScale;
        s.x = mirrored ? -1f : 1f;
        label.rectTransform.localScale = s;
    }

    void OnDisable() { _hover = false; }

    void Update()
    {
        float target = (Active ? activeOut : 0f) + (_hover ? hoverOut : 0f);
        _out = Mathf.Lerp(_out, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f));
        _nudge = Mathf.MoveTowards(_nudge, 0f, Time.unscaledDeltaTime * 3.5f);
        float wobble = Mathf.Sin(_nudge * 18f) * 6f * _nudge;
        if (body != null) body.anchoredPosition = new Vector2(-tuck + _out + wobble, 0f);
    }
}
