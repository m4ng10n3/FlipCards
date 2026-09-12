using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Programma stampato a destra del rullo, dall'alto in basso: spada ambra
/// (carica), scudo ciano (trattenuta). Il cursore indica il passo corrente.
/// Il valore di attacco appartiene alle lampade della cassa. Il nome della
/// classe conserva i collegamenti dei prefab esistenti.
/// </summary>
public sealed class ReelPrintedAttack : MonoBehaviour
{
    SlotView _view;
    RectTransform _root;
    readonly List<Image> _symbols = new();
    readonly List<Image> _cursors = new();

    void LateUpdate()
    {
        if (_view == null) _view = GetComponent<SlotView>();
        var inst = _view != null ? _view.instance : null;
        if (inst == null) return;
        int count = Mathf.Max(1, inst.PatternLength);
        if (_root == null)
        {
            _root = UiBuild.Rect("PrintedProgram", transform);
            UiBuild.Stretch(_root);
        }
        if (_symbols.Count != count) Build(count);
        for (int i = 0; i < count; i++)
        {
            var side = inst.PatternSideAt(i);
            _symbols[i].sprite = side == Side.Fronte ? GlyphSprites.Sword : GlyphSprites.Shield;
            _symbols[i].color = GamePalette.SideColor(side);
            _cursors[i].enabled = i == inst.PatternStep;
        }
    }

    void Build(int count)
    {
        UiBuild.ClearChildren(_root);
        _symbols.Clear();
        _cursors.Clear();
        // Fit the full program in the old column; no repeated or truncated states.
        float height = SlotOverlay.NotchPitch * SlotOverlay.NotchSeats;
        float pitch = Mathf.Min(SlotOverlay.NotchPitch, height / count);
        float size = Mathf.Max(1f, Mathf.Min(SlotOverlay.NotchSize, pitch - 2f));
        float top = SlotOverlay.ArtY + SlotOverlay.ArtSize * .5f - pitch * count * .5f;
        for (int i = 0; i < count; i++)
        {
            var rt = UiBuild.Rect("State" + i, _root);
            UiBuild.Band(rt, SlotOverlay.NotchX + (SlotOverlay.NotchSize - size) * .5f,
                top + i * pitch, size, size);
            var symbol = UiBuild.Fill(rt, Color.white);
            symbol.preserveAspect = true;
            symbol.raycastTarget = false;
            _symbols.Add(symbol);
            var cursor = UiBuild.Rect("Current" + i, _root);
            UiBuild.Band(cursor, SlotOverlay.NotchX - 5f, top + i * pitch + size * .25f, 3f, size * .5f);
            var mark = UiBuild.Fill(cursor, GamePalette.Ink);
            mark.raycastTarget = false;
            _cursors.Add(mark);
        }
    }
}
