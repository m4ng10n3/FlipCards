using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Helper per costruire UI da codice. Usato sia dal builder di scena (editor) sia
/// dagli overlay che carte e slot si creano addosso a runtime.
///
/// Tutte le coordinate passano per <see cref="Band"/>: origine in alto a sinistra
/// del parent, y che cresce verso il basso. E' il sistema in cui e' scritta la
/// specifica di layout, cosi i numeri del documento finiscono nel codice invariati.
/// </summary>
public static class UiBuild
{
    static TMP_FontAsset _font;

    public static TMP_FontAsset Font
    {
        get
        {
            if (_font != null) return _font;
            _font = TMP_Settings.defaultFontAsset;
            if (_font == null) _font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            return _font;
        }
    }

    // ── Rect ──────────────────────────────────────────────────────────────────

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        return rt;
    }

    /// <summary>Posiziona in coordinate banda: (x, y) dall'angolo alto-sinistra del parent.</summary>
    public static RectTransform Band(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, -y);
        return rt;
    }

    public static RectTransform Stretch(RectTransform rt, float l = 0f, float t = 0f, float r = 0f, float b = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(l, b);
        rt.offsetMax = new Vector2(-r, -t);
        return rt;
    }

    /// <summary>Rect centrato sul parent, dimensione fissa.</summary>
    public static RectTransform Centered(RectTransform rt, float w, float h, float dx = 0f, float dy = 0f)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(dx, dy);
        return rt;
    }

    // ── Grafica ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Rettangolo colorato. raycast resta SPENTO di default: qualunque Image con
    /// Raycast Target acceso sopra l'area di gioco rompe drag-and-drop e swap
    /// (FindEmptySpotUnderPointer risale i parent del primo hit).
    /// </summary>
    public static Image Fill(RectTransform rt, Color color, bool raycast = false)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    public static RectTransform PanelBox(string name, Transform parent, Color color, bool border = true)
    {
        var rt = Rect(name, parent);
        Fill(rt, color);
        if (border)
        {
            var o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = GamePalette.Border;
            o.effectDistance = new Vector2(1.5f, -1.5f);
            o.useGraphicAlpha = false;
        }
        return rt;
    }

    public static TextMeshProUGUI Text(string name, Transform parent, string content, float size,
                                       Color color, TextAlignmentOptions align = TextAlignmentOptions.Left,
                                       FontStyles style = FontStyles.Normal)
    {
        var rt = Rect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (Font != null) t.font = Font;
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = style;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    /// <summary>Barra valore/massimo: sfondo incassato + riempimento ad ancore.</summary>
    public static UiBar Bar(string name, Transform parent, Color fillColor, out RectTransform root,
                            bool vertical = false)
    {
        root = Rect(name, parent);
        Fill(root, GamePalette.PanelSunken);

        var fillRt = Rect("Fill", root);
        Stretch(fillRt, 2f, 2f, 2f, 2f);

        Fill(fillRt, fillColor);

        var bar = root.gameObject.AddComponent<UiBar>();
        bar.fill = fillRt;
        bar.vertical = vertical;
        return bar;
    }

    /// <summary>
    /// Bottone del pannello comandi. Il tint disabilitato di default e' quasi
    /// invisibile su Image chiare: qui il contrasto e' esplicito, cosi le fasi in
    /// cui un comando non e' disponibile si leggono davvero.
    /// </summary>
    public static Button Command(string name, Transform parent, string label, string cost,
                                 Color accent, out TextMeshProUGUI labelText)
    {
        var rt = Rect(name, parent);
        var img = Fill(rt, GamePalette.Panel, raycast: true);

        var outline = rt.gameObject.AddComponent<Outline>();
        outline.effectColor = accent;
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = false;

        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.ColorTint;
        var colors = btn.colors;
        colors.normalColor      = Color.white;
        colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
        colors.pressedColor     = new Color(0.70f, 0.70f, 0.70f, 1f);
        colors.selectedColor    = Color.white;
        colors.disabledColor    = new Color(0.32f, 0.32f, 0.36f, 1f);
        colors.colorMultiplier  = 1f;
        colors.fadeDuration     = 0.08f;
        btn.colors = colors;

        labelText = Text("Label", rt, label, 24f, GamePalette.TextPrimary, TextAlignmentOptions.Left, FontStyles.Bold);
        Band(labelText.rectTransform, 20f, 0f, 260f, 56f);
        labelText.alignment = TextAlignmentOptions.Left;

        var costText = Text("Cost", rt, cost, 17f, GamePalette.TextMuted, TextAlignmentOptions.Right);
        Band(costText.rectTransform, 100f, 0f, 280f, 56f);
        costText.alignment = TextAlignmentOptions.Right;

        return btn;
    }

    // ── Utility ───────────────────────────────────────────────────────────────

    public static void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            var child = t.GetChild(i);
            child.SetParent(null, false);
            if (Application.isPlaying) Object.Destroy(child.gameObject);
            else Object.DestroyImmediate(child.gameObject);
        }
    }

    /// <summary>Freccia/segno del pronostico di corsia, senza dipendere dai glifi del font.</summary>
    public static string Arrow(bool up) => up ? "▲" : "▼";
}
