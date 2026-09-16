using UnityEngine;

/// <summary>
/// Lo srotolamento della pergamena: il rotolo parte da dove sta sul tavolo,
/// arriva al centro e si apre. E' solo il rettangolo che cresce — il contenuto
/// e' gia' tutto montato dentro e la maschera del viewport lo scopre man mano,
/// che e' esattamente come si comporta un rotolo.
///
/// I due rulli restano ai bordi del pannello e la superficie ruota con il
/// movimento della carta (ParchmentRollerMotion), fuori dalla maschera del testo.
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
