using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Non-interactive destination brackets; shares the drop handler's resolved target.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class DropPreview : MaskableGraphic
{
    public static DropPreview Show(Transform target, bool swap)
    {
        var rt=UiBuild.Rect("DropDestination",target);
        rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,.5f);
        rt.anchoredPosition=Vector2.zero;
        rt.sizeDelta=new Vector2(CardOverlay.CardW+28f,CardOverlay.CardH+28f);
        var graphic=rt.gameObject.AddComponent<DropPreview>(); graphic.raycastTarget=false;
        var label=UiBuild.Text("Action",rt,swap?"RILASCIA\nSCAMBIA":"RILASCIA\nQUI",15f,GamePalette.Good,
            TextAlignmentOptions.Center,FontStyles.Bold);
        UiBuild.Band(label.rectTransform,rt.sizeDelta.x+6f,145f,100f,48f);
        return graphic;
    }
    void Update() => SetVerticesDirty();
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); var r=rectTransform.rect;
        var ink=GamePalette.WithAlpha(GamePalette.Good,.7f+.3f*Mathf.Sin(Time.unscaledTime*5f));
        Quad(vh,r.xMin,r.yMin,r.width,r.height,GamePalette.WithAlpha(GamePalette.Good,.055f));
        foreach(float x in new[]{r.xMin,r.xMax-30f})
        foreach(float y in new[]{r.yMin,r.yMax-3f}) Quad(vh,x,y,30f,3f,ink);
        foreach(float x in new[]{r.xMin,r.xMax-3f})
        foreach(float y in new[]{r.yMin,r.yMax-30f}) Quad(vh,x,y,3f,30f,ink);
    }
    static void Quad(VertexHelper vh,float x,float y,float w,float h,Color tint)
    {
        int i=vh.currentVertCount;
        vh.AddVert(new Vector3(x,y),tint,Vector2.zero); vh.AddVert(new Vector3(x,y+h),tint,Vector2.zero);
        vh.AddVert(new Vector3(x+w,y+h),tint,Vector2.zero); vh.AddVert(new Vector3(x+w,y),tint,Vector2.zero);
        vh.AddTriangle(i,i+1,i+2); vh.AddTriangle(i,i+2,i+3);
    }
}
