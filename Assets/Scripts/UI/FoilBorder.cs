using UnityEngine;
using UnityEngine.UI;

/// <summary>Four narrow foil strips sharing the portrait's tilt material; printed indices stay above.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class FoilBorder : MaskableGraphic
{
    public Image source;
    public Image driver;
    public override Texture mainTexture => source != null && source.sprite != null
        ? source.sprite.texture : Texture2D.whiteTexture;
    void LateUpdate()
    {
        if (driver != null && material != driver.material) material = driver.material;
        if (source != null) SetMaterialDirty();
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = rectTransform.rect;
        const float inset = 3f, width = 5f;
        Strip(vh, r, r.xMin + inset, r.yMin + inset, r.width - inset * 2f, width);
        Strip(vh, r, r.xMin + inset, r.yMax - inset - width, r.width - inset * 2f, width);
        Strip(vh, r, r.xMin + inset, r.yMin + inset + width, width, r.height - (inset + width) * 2f);
        Strip(vh, r, r.xMax - inset - width, r.yMin + inset + width, width, r.height - (inset + width) * 2f);
    }
    void Strip(VertexHelper vh, Rect r, float x, float y, float w, float h)
    {
        int n = vh.currentVertCount;
        Vertex(vh, r, x, y); Vertex(vh, r, x, y + h);
        Vertex(vh, r, x + w, y + h); Vertex(vh, r, x + w, y);
        vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
    }
    void Vertex(VertexHelper vh, Rect r, float x, float y)
    {
        var uv = new Vector2((x - r.xMin) / r.width, (y - r.yMin) / r.height);
        if (source != null && source.sprite != null)
        {
            var rect = source.sprite.textureRect;
            var texture = source.sprite.texture;
            uv = new Vector2((rect.x + uv.x * rect.width) / texture.width,
                (rect.y + uv.y * rect.height) / texture.height);
        }
        vh.AddVert(new Vector3(x, y), color, uv);
    }
}
