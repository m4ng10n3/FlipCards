using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// I segni momentanei di una carta o di una casella — lo scudo spezzato della
/// risonanza, le insegne che riceve dalle vicine — stanno accanto al suo nome,
/// sulla stessa targhetta. Prima galleggiavano sul panno fra la cassa e le carte,
/// o in un angolo della carta lontano da qualunque cosa li spiegasse.
///
/// Ogni segno e' il glifo inciso nel colore della fazione, alto quanto la
/// targhetta, con un filetto d'inchiostro che lo stacca sia dal fronte chiaro
/// sia dal dorso rosso. Un tondino d'avorio dietro lo rimpiccioliva fino a
/// renderlo illeggibile. I segni partono subito dopo la fine del testo del nome; se la
/// targhetta non basta, il nome scivola a sinistra quanto serve. Senza nome
/// visibile (il dorso) si allineano alla fine della targhetta.
///
/// Coordinate "banda" come UiBuild: origine in alto a sinistra del parent.
/// </summary>
public sealed class NameMarks : MonoBehaviour
{
    [Tooltip("Il Text del nome; puo' mancare o essere spento, e allora i segni stanno in fondo alla targhetta.")]
    public Text nameText;
    [Tooltip("La targhetta del nome in coordinate banda del parent: x, y, larghezza, altezza.")]
    public Rect plate;
    public float markSize = 23f;
    public float gap = 2f;

    sealed class Mark
    {
        public RectTransform root;
        public Image glyph;
        public bool on;
    }

    readonly List<Mark> _marks = new List<Mark>();
    Vector2 _nameBase;
    bool _nameBaseRead;
    RectTransform _rt;

    RectTransform Rt => _rt != null ? _rt : _rt = (RectTransform)transform;

    /// <summary>Accende o spegne il segno <paramref name="slot"/> (0, 1, 2...) con glifo e colore.</summary>
    public void Set(int slot, bool on, Sprite glyph = null, Color color = default)
    {
        while (_marks.Count <= slot) _marks.Add(Build(_marks.Count));
        var mark = _marks[slot];
        mark.on = on;
        mark.root.gameObject.SetActive(on);
        if (on)
        {
            if (glyph != null) mark.glyph.sprite = glyph;
            mark.glyph.color = Color.Lerp(color, GamePalette.Ink, .15f);
        }
    }

    Mark Build(int index)
    {
        var root = UiBuild.Rect("Mark" + index, Rt);
        var glyph = UiBuild.Fill(root, Color.white);
        glyph.preserveAspect = true;
        glyph.raycastTarget = false;
        var outline = root.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(GamePalette.Ink.r, GamePalette.Ink.g, GamePalette.Ink.b, .9f);
        outline.effectDistance = new Vector2(.9f, -.9f);
        root.gameObject.SetActive(false);
        return new Mark { root = root, glyph = glyph };
    }

    void LateUpdate()
    {
        int count = 0;
        foreach (var m in _marks) if (m.on) count++;

        bool nameVisible = nameText != null && nameText.isActiveAndEnabled && !string.IsNullOrEmpty(nameText.text);
        if (nameText != null && !_nameBaseRead) { _nameBase = nameText.rectTransform.anchoredPosition; _nameBaseRead = true; }

        float total = count * markSize + Mathf.Max(0, count - 1) * gap;
        float plateRight = plate.x + plate.width;
        float start, shift = 0f;
        if (count > 0 && nameVisible)
        {
            float textW = Mathf.Min(nameText.preferredWidth, plate.width);
            var anchor = nameText.alignment;
            bool centered = anchor == TextAnchor.UpperCenter || anchor == TextAnchor.MiddleCenter || anchor == TextAnchor.LowerCenter;
            float textLeft = centered ? plate.x + (plate.width - textW) * .5f : plate.x;
            start = textLeft + textW + gap * 2f;
            float overflow = start + total - plateRight;
            if (overflow > 0f)
            {
                shift = Mathf.Min(overflow, textLeft - plate.x);
                start -= shift;
            }
        }
        else start = plateRight - total;

        if (nameText != null && _nameBaseRead)
            nameText.rectTransform.anchoredPosition = _nameBase - new Vector2(count > 0 ? shift : 0f, 0f);

        float y = plate.y + (plate.height - markSize) * .5f;
        int i = 0;
        foreach (var m in _marks)
        {
            if (!m.on) continue;
            UiBuild.Band(m.root, start + i * (markSize + gap), y, markSize, markSize);
            i++;
        }
    }
}
