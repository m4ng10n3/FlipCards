using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Un foglio del libretto che gira attorno alla piega, con la curvatura della
/// carta. La mesh e' una striscia di colonne: ogni colonna ha la sua direzione,
/// e il foglio e' l'integrale di quelle direzioni lungo la sua larghezza, cosi'
/// la lunghezza resta quella del foglio mentre si incurva.
///
/// Il perno e' lo zero locale del rect (sulla piega): a <see cref="angle"/> 0 il
/// foglio sta disteso a destra, a PI disteso a sinistra. Il canvas e' in
/// Screen Space - Camera con camera prospettica, quindi il sollevamento lungo z
/// si vede davvero di scorcio.
///
/// Fronte e retro sono due texture: il fronte e' la <c>mainTexture</c>, il retro
/// va nel materiale (<c>_BackTex</c>) e lo shader sceglie la faccia con VFACE.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class BookLeaf : MaskableGraphic
{
    public Texture front;
    public Texture back;
    [Tooltip("Larghezza e altezza del foglio, in unita' del rect.")]
    public Vector2 size = new Vector2(600, 740);
    [Tooltip("Scostamento verticale del centro del foglio dal perno.")]
    public float centerY;
    [Range(0, Mathf.PI)] public float angle;
    [Tooltip("Quanto il bordo esterno resta indietro rispetto alla piega mentre gira (radianti al massimo).")]
    public float curl;
    [Range(4, 64)] public int segments = 28;
    [Tooltip("Ombra del foglio in verticale: 0 nessuna, 1 nero.")]
    [Range(0, 1)] public float edgeShade = .28f;

    Material _instance;

    public override Texture mainTexture => front != null ? front : s_WhiteTexture;

    public override Material materialForRendering
    {
        get
        {
            var baseMaterial = base.materialForRendering;
            if (back == null || baseMaterial == null) return baseMaterial;
            if (_instance == null || _instance.shader != baseMaterial.shader)
            {
                if (_instance != null) DestroyImmediate(_instance);
                _instance = new Material(baseMaterial) { hideFlags = HideFlags.HideAndDontSave };
            }
            _instance.CopyPropertiesFromMaterial(baseMaterial);
            _instance.SetTexture("_BackTex", back);
            return _instance;
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (_instance != null) DestroyImmediate(_instance);
    }

    public void SetAngle(float radians, float curlAmount)
    {
        angle = Mathf.Clamp(radians, 0f, Mathf.PI);
        curl = curlAmount;
        SetVerticesDirty();
    }

    /// <summary>
    /// Posizione e direzione del bordo esterno, nello spazio del perno. Servono al
    /// segnalibro che viaggia attaccato al foglio.
    /// </summary>
    public static Vector3 Edge(float width, float angle, float curl, int segments, out float edgeAngle)
    {
        Vector3 p = Vector3.zero;
        float step = width / segments;
        edgeAngle = angle;
        for (int i = 0; i < segments; i++)
        {
            float s = (i + .5f) / segments;
            float phi = Direction(angle, curl, s);
            p += new Vector3(Mathf.Cos(phi), 0f, -Mathf.Sin(phi)) * step;
            edgeAngle = Direction(angle, curl, (i + 1f) / segments);
        }
        return p;
    }

    /// <summary>Il bordo esterno resta indietro: la carta si piega, non ruota rigida.</summary>
    static float Direction(float angle, float curl, float s)
        => angle - curl * Mathf.Sin(angle) * s * s;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        float h = size.y * .5f;
        float step = size.x / segments;
        Vector3 p = Vector3.zero;
        var baseColor = (Color)color;
        for (int i = 0; i <= segments; i++)
        {
            float s = i / (float)segments;
            float phi = Direction(angle, curl, s);
            // La carta in piedi prende meno luce di quella distesa.
            float light = 1f - edgeShade * Mathf.Abs(Mathf.Sin(phi));
            var c = new Color(baseColor.r * light, baseColor.g * light, baseColor.b * light, baseColor.a);
            vh.AddVert(new Vector3(p.x, centerY - h, p.z), c, new Vector2(s, 0f));
            vh.AddVert(new Vector3(p.x, centerY + h, p.z), c, new Vector2(s, 1f));
            if (i < segments)
            {
                float mid = Direction(angle, curl, (i + .5f) / segments);
                p += new Vector3(Mathf.Cos(mid), 0f, -Mathf.Sin(mid)) * step;
            }
        }
        for (int i = 0; i < segments; i++)
        {
            int a = i * 2;
            vh.AddTriangle(a, a + 1, a + 3);
            vh.AddTriangle(a, a + 3, a + 2);
        }
    }
}
