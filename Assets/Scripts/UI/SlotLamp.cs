using UnityEngine;
using UnityEngine.UI;

/// <summary>Small inset signal lamp: a dark socket, coloured lens and lit highlight.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class SlotLamp : MaskableGraphic
{
    public enum Lens { Round, Spear, Shield }
    public Lens shape;
    public bool Lit => _lit;
    bool _lit;
    Color _accent = Color.white;
    Sprite Artwork => UiSkin.Sprite("lamp_" + shape.ToString().ToLowerInvariant() + (_lit ? "_on" : "_off"));
    public override Texture mainTexture => Artwork != null ? Artwork.texture : base.mainTexture;
    public void SetState(bool lit, Color accent)
    {
        if (_lit == lit && _accent == accent) return;
        _lit = lit; _accent = accent; SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var rect = rectTransform.rect;
        var sprite = Artwork;
        if (sprite != null)
        {
            var uv = UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);
            vh.AddVert(new Vector3(rect.xMin,rect.yMin),Color.white,new Vector2(uv.x,uv.y));
            vh.AddVert(new Vector3(rect.xMin,rect.yMax),Color.white,new Vector2(uv.x,uv.w));
            vh.AddVert(new Vector3(rect.xMax,rect.yMax),Color.white,new Vector2(uv.z,uv.w));
            vh.AddVert(new Vector3(rect.xMax,rect.yMin),Color.white,new Vector2(uv.z,uv.y));
            vh.AddTriangle(0,1,2); vh.AddTriangle(0,2,3);
            return;
        }
        float radius = Mathf.Min(rect.width, rect.height) * .5f;
        var center = rect.center;
        LensMesh(vh, center, radius, new Color(.54f, .43f, .26f));
        LensMesh(vh, center, radius * .87f, GamePalette.Ink);
        LensMesh(vh, center, radius * .65f, Color.Lerp(GamePalette.Ink, _accent, _lit ? 1f : .16f));
        if (_lit) Disc(vh, center + new Vector2(-radius*.2f, radius*.2f),
            radius*.22f, GamePalette.Paper);
    }
    void LensMesh(VertexHelper vh, Vector2 center, float radius, Color tint)
    {
        if (shape == Lens.Round) { Disc(vh, center, radius, tint); return; }
        Vector2[] points = shape == Lens.Spear
            ? new[] { new Vector2(0,1), new Vector2(.8f,-.35f), new Vector2(.3f,-1), new Vector2(-.3f,-1), new Vector2(-.8f,-.35f) }
            : new[] { new Vector2(-.85f,1), new Vector2(.85f,1), new Vector2(.72f,-.25f), new Vector2(0,-1), new Vector2(-.72f,-.25f) };
        int start = vh.currentVertCount;
        vh.AddVert(center,tint,Vector2.zero);
        foreach (var p in points) vh.AddVert(center+p*radius,tint,Vector2.zero);
        for(int i=0;i<points.Length;i++) vh.AddTriangle(start,start+1+i,start+1+(i+1)%points.Length);
    }
    static void Disc(VertexHelper vh, Vector2 center, float radius, Color tint)
    {
        const int segments = 12;
        int start = vh.currentVertCount;
        vh.AddVert(center, tint, Vector2.zero);
        for (int i=0; i<=segments; i++)
        {
            float a=i*Mathf.PI*2f/segments;
            vh.AddVert(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,tint,Vector2.zero);
            if (i>0) vh.AddTriangle(start,start+i,start+i+1);
        }
    }
}
