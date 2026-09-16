using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Display a matrice di LED indirizzabili uno per uno, come un monitor a pixel.
///
/// Lo stato e' una texture da un texel per LED (colonne x righe, filtro Point):
/// <see cref="SetPixel"/> scrive, <see cref="Apply"/> la manda alla GPU. La
/// texture e' la <c>mainTexture</c> della grafica, quindi non serve un
/// materiale per istanza; lo shader <c>FlipCards/LED Matrix</c> ricava la
/// griglia dalla sua dimensione.
///
/// <b>La mesh segue i fori dell'arte.</b> La griglia di fori e' dipinta nello
/// sprite che sta sopra (12_TableProps/Tools/build_led_displays.py) e i suoi
/// numeri sono in <c>led_displays.json</c>: qui si ricostruisce la stessa
/// parametrizzazione, in pixel della tela sorgente, cosi' ogni cella cade sotto
/// il suo foro. Due forme: un quadrilatero (la guancia della cassa, che e'
/// leggermente in prospettiva) e un arco d'ellisse su un cono (la gonna del
/// basamento del fungo).
///
/// Uno strato di bagliore e' un secondo LedMatrix con <see cref="source"/>
/// assegnato: legge la stessa texture e disegna sopra l'arte in additivo.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class LedMatrix : MaskableGraphic
{
    public enum Shape { Quad, ConeArc }

    [Header("Griglia")]
    [Min(1)] public int columns = 3;
    [Min(1)] public int rows = 62;
    public Shape shape = Shape.Quad;

    [Header("Tela sorgente")]
    [Tooltip("Pixel della tela che finisce nell'angolo in alto a sinistra del rect.")]
    public Vector2 sourceOrigin;
    [Tooltip("Unita' del rect per pixel della tela.")]
    public Vector2 sourceScale = Vector2.one;

    [Header("Quadrilatero (pixel tela, y verso il basso)")]
    public Vector2 topLeft, topRight, bottomLeft, bottomRight;

    [Header("Arco su cono (pixel tela, y verso il basso)")]
    public Vector2 arcCenter;
    public Vector2 arcRadii;
    public float cone;
    public Vector2 thetaRange;
    [Tooltip("Quote dall'ellisse di base: x = bordo alto della griglia, y = bordo basso.")]
    public Vector2 dyRange;

    [Header("Bagliore")]
    [Tooltip("Se assegnato, questa grafica e' il bagliore di un altro display: ne legge lo stato.")]
    public LedMatrix source;
    [Tooltip("Celle di margine attorno alla griglia per lasciar uscire il bagliore.")]
    public float glowMargin = 1.5f;

    Texture2D _state;
    Color32[] _pixels;
    bool _dirty;

    public override Texture mainTexture => source != null ? source.State : State;

    public Texture2D State
    {
        get { Ensure(); return _state; }
    }

    public int Columns => columns;
    public int Rows => rows;

    void Ensure()
    {
        if (_state != null && _state.width == columns && _state.height == rows) return;
        if (_state != null) DestroyTexture(_state);
        _state = new Texture2D(columns, rows, TextureFormat.RGBA32, false, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
            name = name + "_LedState",
        };
        _pixels = new Color32[columns * rows];
        _state.SetPixels32(_pixels);
        _state.Apply(false);
    }

    static void DestroyTexture(Object texture)
    {
        if (Application.isPlaying) Destroy(texture); else DestroyImmediate(texture);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (_state != null) DestroyTexture(_state);
    }

    /// <summary>
    /// Scrive un LED. <paramref name="x"/> da sinistra, <paramref name="y"/> dal
    /// basso. Il canale alpha e' quanto quel LED alimenta il bagliore: un LED
    /// spento puo' avere un colore (la brace del filamento) senza irradiare.
    /// </summary>
    public void SetPixel(int x, int y, Color color, float glow)
    {
        Ensure();
        if (x < 0 || y < 0 || x >= columns || y >= rows) return;
        var c = (Color32)new Color(color.r, color.g, color.b, Mathf.Clamp01(glow));
        _pixels[y * columns + x] = c;
        _dirty = true;
    }

    public void Apply()
    {
        if (!_dirty || _state == null) return;
        _dirty = false;
        _state.SetPixels32(_pixels);
        _state.Apply(false);
    }

    // ── Mesh ──────────────────────────────────────────────────────────────────

    Vector3 ToLocal(Vector2 sourcePixel)
    {
        var r = rectTransform.rect;
        return new Vector3(r.xMin + (sourcePixel.x - sourceOrigin.x) * sourceScale.x,
                           r.yMax - (sourcePixel.y - sourceOrigin.y) * sourceScale.y, 0f);
    }

    /// <summary>Punto della tela per (u da sinistra, v dal basso) della griglia.</summary>
    Vector2 SourcePoint(float u, float v)
    {
        if (shape == Shape.Quad)
        {
            var bottom = Vector2.LerpUnclamped(bottomLeft, bottomRight, u);
            var top = Vector2.LerpUnclamped(topLeft, topRight, u);
            return Vector2.LerpUnclamped(bottom, top, v);
        }
        float theta = Mathf.LerpUnclamped(thetaRange.x, thetaRange.y, u);
        float dy = Mathf.LerpUnclamped(dyRange.y, dyRange.x, v);
        float s = 1f + cone * dy / arcRadii.y;
        return new Vector2(arcCenter.x + arcRadii.x * s * Mathf.Sin(theta),
                           arcCenter.y + dy + arcRadii.y * s * Mathf.Cos(theta));
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        float mu = source != null ? glowMargin / columns : 0f;
        float mv = source != null ? glowMargin / rows : 0f;
        // Suddivisione fitta: la mappa bilineare (o l'arco) non e' affine, e due
        // triangoli soli sposterebbero le celle dai fori di qualche pixel.
        int segU = shape == Shape.ConeArc ? columns * 3 : Mathf.Max(2, columns);
        int segV = shape == Shape.Quad ? Mathf.Max(8, rows / 2) : Mathf.Max(2, rows * 2);
        var tint = (Color32)color;
        for (int j = 0; j <= segV; j++)
        for (int i = 0; i <= segU; i++)
        {
            float u = Mathf.Lerp(-mu, 1f + mu, i / (float)segU);
            float v = Mathf.Lerp(-mv, 1f + mv, j / (float)segV);
            vh.AddVert(ToLocal(SourcePoint(u, v)), tint, new Vector4(u, v, 0f, 0f));
        }
        int stride = segU + 1;
        for (int j = 0; j < segV; j++)
        for (int i = 0; i < segU; i++)
        {
            int a = j * stride + i;
            vh.AddTriangle(a, a + stride, a + stride + 1);
            vh.AddTriangle(a, a + stride + 1, a + 1);
        }
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        SetVerticesDirty();
    }
}
