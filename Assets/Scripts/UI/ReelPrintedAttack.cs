using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tacche d'attacco stampate sulla faccia del rullo, in colonna a destra del
/// simbolo come nel riferimento. La vita non sta qui: e' delle lampade della cassa.
/// </summary>
public sealed class ReelPrintedAttack : MonoBehaviour
{
    SlotView _view;
    readonly Image[] _marks = new Image[SlotOverlay.NotchSeats];
    TMP_Text _overflow;
    int _last = -1;

    void Start()
    {
        _view = GetComponent<SlotView>();
        for (int i = 0; i < _marks.Length; i++)
        {
            var rt = UiBuild.Rect("PrintedAttack" + i, transform);
            UiBuild.Band(rt, SlotOverlay.NotchX, SlotOverlay.NotchY(i), SlotOverlay.NotchSize, SlotOverlay.NotchSize);
            _marks[i] = UiBuild.Fill(rt, Color.white);
            _marks[i].preserveAspect = true;
        }
        _overflow = UiBuild.Text("PrintedAttackTotal", transform, "", 15, GamePalette.Ink, TextAlignmentOptions.Center);
        UiBuild.Band(_overflow.rectTransform, SlotOverlay.NotchX, SlotOverlay.NotchY(_marks.Length), SlotOverlay.NotchSize, 22);
    }

    void LateUpdate()
    {
        if (_view == null || _view.instance == null) return;
        int value = Mathf.Max(0, _view.instance.def.atkDamage + _view.instance.tempAtkBonus);
        if (value == _last) return;
        _last = value;
        // Si riempiono dal basso, come le lance sulle carte.
        for (int i = 0; i < _marks.Length; i++)
            _marks[i].sprite = UiSkin.Sprite("final_front_attack_" + (_marks.Length - 1 - i < value ? "full" : "empty"));
        _overflow.text = value > _marks.Length ? value.ToString() : "";
    }
}
