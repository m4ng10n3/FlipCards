using UnityEngine;
using UnityEngine.UI;

/// <summary>Displays the exact cut-out ink geometry from the generated card sheet.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class EngravedOrnament : MaskableGraphic
{
    public string spriteKey;
    public bool horizontal;
    public override Texture mainTexture => UiSkin.Sprite(spriteKey)?.texture ?? base.mainTexture;
    public void SetSprite(string key) { if(spriteKey==key)return; spriteKey=key;SetAllDirty(); }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); Append(vh,UiSkin.Sprite(spriteKey),rectTransform.rect,color,horizontal);
    }
    public static void Append(VertexHelper vh, Sprite sprite, Rect rect, Color tint, bool horizontal=false)
    {
        if(sprite==null)return;
        var mesh=EngravedInkLibrary.Get(sprite.name);
        if(mesh==null)return;
        var vertices=mesh.vertices;var uv=mesh.uv;var triangles=mesh.triangles;
        int start=vh.currentVertCount;
        for(int i=0;i<vertices.Length;i++)
        {
            float x=vertices[i].x;
            float y=vertices[i].y;
            var pos=horizontal ? new Vector3(rect.xMin+y*rect.width,rect.yMin+(1f-x)*rect.height)
                : new Vector3(rect.xMin+x*rect.width,rect.yMin+y*rect.height);
            vh.AddVert(pos,tint,uv[i]);
        }
        for(int i=0;i<triangles.Length;i+=3)vh.AddTriangle(start+triangles[i],start+triangles[i+1],start+triangles[i+2]);
    }
}
