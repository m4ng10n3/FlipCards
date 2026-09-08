using UnityEngine;
using UnityEngine.UI;

/// <summary>Continuous ornamental line: a cut-out lozenge per point, a fine rule when absent.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class MarginTally : MaskableGraphic
{
    public bool compact;
    int _value=-1, _capacity=10;
    public int Value => _value;
    public int Capacity => _capacity;
    public override Texture mainTexture => UiSkin.Sprite("engraved_point_on")?.texture ?? base.mainTexture;
    public void SetValue(int value,int capacity,Color ink)
    {
        value=Mathf.Max(0,value);
        capacity=compact?Mathf.Max(3,Mathf.Max(value,capacity)):Mathf.Max(10,Mathf.CeilToInt(Mathf.Max(value,capacity)/10f)*10);
        if(_value==value&&_capacity==capacity&&color==ink)return;
        _value=value;_capacity=capacity;color=ink;SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();var r=rectTransform.rect;
        float step=(compact?r.width:r.height)/_capacity;
        for(int i=0;i<_capacity;i++)
        {
            bool active=i<_value;
            var sprite=UiSkin.Sprite(active?"engraved_point_on":"engraved_point_off");
            var cell=compact?new Rect(r.xMin+i*step,r.yMin,step+.05f,r.height)
                :new Rect(r.xMin,r.yMax-(i+1)*step,r.width,step+.05f);
            if (!active)
            {
                // Preserve the fine rule at card scale and under tilt: at least one screen pixel.
                if (compact) { cell.yMin-=r.height*.35f; cell.yMax+=r.height*.35f; }
                else { cell.xMin-=r.width*.35f; cell.xMax+=r.width*.35f; }
            }
            var tint=compact?(active?color:GamePalette.Ink):Color.white;
            EngravedOrnament.Append(vh,sprite,cell,tint,compact);
        }
    }
}
