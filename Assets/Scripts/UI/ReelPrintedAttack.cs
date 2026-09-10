using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Printed attack tally on a reel face; health belongs only to cabinet lamps.</summary>
public sealed class ReelPrintedAttack : MonoBehaviour
{
    SlotView _view;
    readonly Image[] _marks = new Image[7];
    TMP_Text _overflow;
    int _last = -1;
    void Start()
    {
        _view = GetComponent<SlotView>();
        for (int i=0;i<_marks.Length;i++)
        {
            var rt=UiBuild.Rect("PrintedAttack"+i,transform);
            UiBuild.Band(rt,315,59+i*25,24,24);
            _marks[i]=UiBuild.Fill(rt,Color.white);
        }
        _overflow=UiBuild.Text("PrintedAttackTotal",transform,"",15,GamePalette.Ink,TextAlignmentOptions.Center);
        UiBuild.Band(_overflow.rectTransform,309,244,36,23);
    }
    void LateUpdate()
    {
        if(_view==null || _view.instance==null)return;
        int value=Mathf.Max(0,_view.instance.def.atkDamage+_view.instance.tempAtkBonus);
        if(value==_last)return;_last=value;
        for(int i=0;i<_marks.Length;i++)_marks[i].sprite=UiSkin.Sprite("final_front_attack_"+(6-i<value?"full":"empty"));
        _overflow.text=value>7?value.ToString():"";
    }
}
