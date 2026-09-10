using UnityEngine;

/// <summary>
/// Lo srotolamento della pergamena: il rotolo parte da dove sta sul tavolo,
/// arriva al centro e si apre. E' solo il rettangolo che cresce — il contenuto
/// e' gia' tutto montato dentro e la maschera del viewport lo scopre man mano,
/// che e' esattamente come si comporta un rotolo.
///
/// I due rulli d'ottone stanno DENTRO il contenuto scorrevole, non ai bordi
/// della finestra: percio' quando il testo supera l'altezza se ne vede uno per
/// volta, e quando ci sta si vedono tutti e due.
/// </summary>
public sealed class MedallionScroll : MonoBehaviour
{
    public Vector2 closedPosition, closedSize, openPosition, openSize;
    [Tooltip("Durata dello srotolamento.")]
    public float seconds = 0.42f;

    RectTransform _rt;
    float _t;

    RectTransform Rect => _rt != null ? _rt : _rt = (RectTransform)transform;

    void OnEnable() { _t = 0f; Apply(0f); }

    void Update()
    {
        if (_t >= 1f) return;
        _t = Mathf.Min(1f, _t + Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds));
        float inv = 1f - _t;
        Apply(1f - inv * inv * inv);
    }

    void Apply(float k)
    {
        Rect.anchoredPosition = Vector2.Lerp(closedPosition, openPosition, k);
        Rect.sizeDelta = Vector2.Lerp(closedSize, openSize, k);
    }
}
