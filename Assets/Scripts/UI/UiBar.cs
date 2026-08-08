using UnityEngine;

/// <summary>
/// Barra valore/massimo. Il riempimento e' un rect ancorato, non
/// <c>Image.fillAmount</c>: fillAmount richiede uno sprite, e su un'Image senza
/// sprite viene ignorato del tutto (la barra resta piena a qualunque valore).
/// </summary>
public class UiBar : MonoBehaviour
{
    public RectTransform fill;

    float _value = -1f;

    public float Value
    {
        get => _value;
        set
        {
            value = Mathf.Clamp01(value);
            if (fill == null || Mathf.Approximately(_value, value)) return;
            _value = value;

            var max = fill.anchorMax;
            max.x = value;
            fill.anchorMax = max;
            fill.gameObject.SetActive(value > 0f);
        }
    }

    public void Set(int current, int max) => Value = max <= 0 ? 0f : current / (float)max;
}
