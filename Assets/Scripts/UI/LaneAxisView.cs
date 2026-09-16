using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Lane resonance and adjacent banners. Damage forecasts live on the cabinet lights and boss HUD.</summary>
public class LaneAxisView : MonoBehaviour
{
    [Header("Riferimenti")]
    public RectTransform laneReferenceRoot;   // di norma playerBoardRoot

    [Header("Geometria")]
    public float columnWidth = 320f;

    class Column
    {
        public RectTransform root;
        public Image resonance;  // scudo spezzato: in questa corsia nessuno para
    }

    /// <summary>Un'insegna accesa nel varco: da quale corsia, verso quale, con che simbolo.</summary>
    struct Banner
    {
        public Sprite glyph;
        public Color color;
        public string label;
    }

    class Connector
    {
        public RectTransform root;
        public readonly List<RectTransform> chips = new List<RectTransform>();
        public readonly List<Image> plates = new List<Image>();
        public readonly List<Image> glyphs = new List<Image>();
        public readonly List<TextMeshProUGUI> labels = new List<TextMeshProUGUI>();
    }

    readonly List<Column> _columns = new List<Column>();
    readonly List<Connector> _connectors = new List<Connector>();
    readonly List<Banner> _banners = new List<Banner>(4);

    RectTransform _rt;

    void Awake()
    {
        _rt = (RectTransform)transform;
    }

    void LateUpdate()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.playerBoardRoot == null) return;

        var reference = laneReferenceRoot != null ? laneReferenceRoot : gm.playerBoardRoot as RectTransform;
        if (reference == null) return;

        int lanes = reference.childCount;
        Rebuild(lanes);

        for (int i = 0; i < lanes; i++)
        {
            float x = LocalCenterX(reference, i);
            _columns[i].root.anchoredPosition = new Vector2(x, 0f);
            RefreshColumn(gm, _columns[i], i);

            if (i >= lanes - 1) continue;
            float xNext = LocalCenterX(reference, i + 1);
            PlaceConnector(_connectors[i], (x + xNext) * 0.5f);
            RefreshConnector(gm, _connectors[i], i);
        }
    }

    // ── Pronostico di corsia ──────────────────────────────────────────────────

    void RefreshColumn(GameManager gm, Column col, int lane)
    {
        var card = gm.GetPlayerCardAtLane(lane);
        bool resonant = SynergyResolver.Resonates(gm, lane);

        ApplyResonance(col, resonant, card);
    }

    /// <summary>
    /// Lo scudo spezzato del colore della fazione: in questa corsia carta e
    /// casella sono della stessa fazione e nessuno dei due para. Sta sull'asse e
    /// non sulla carta perche' e' una proprieta' della corsia — nasce
    /// dall'incontro fra le due, e sparisce appena una delle due cambia.
    /// </summary>
    void ApplyResonance(Column col, bool resonant, CardInstance card)
    {
        if (col.resonance == null) return;

        col.resonance.enabled = resonant && card != null;
        if (!col.resonance.enabled) return;

        col.resonance.color = GamePalette.FactionColor(card.def.faction);
    }

    // ── Insegne nei varchi ────────────────────────────────────────────────────

    /// <summary>
    /// Il varco fra due corsie mostra cosa si stanno passando le due carte. Una
    /// carta coperta e' un'insegna: da' il suo numero — spada in attacco, scudo
    /// in guardia — alla vicina della sua stessa fazione, e la freccia dice in
    /// che direzione, perche' il bonus non e' reciproco.
    ///
    /// E' il posto giusto per dirlo: il bonus nasce dall'adiacenza, quindi vive
    /// nello spazio fra le due carte, non sopra una delle due. Ed e' quello che
    /// rende leggibile lo spostamento: si vede il varco spegnersi quando il
    /// caos di fine turno separa la coppia.
    /// </summary>
    void RefreshConnector(GameManager gm, Connector con, int lane)
    {
        _banners.Clear();

        var left = gm.GetPlayerCardAtLane(lane);
        var right = gm.GetPlayerCardAtLane(lane + 1);

        Collect(left, right, "→");
        Collect(right, left, "←");

        for (int i = 0; i < con.chips.Count; i++)
        {
            bool on = i < _banners.Count;
            con.chips[i].gameObject.SetActive(on);
            if (!on) continue;

            var banner = _banners[i];
            con.glyphs[i].sprite = banner.glyph;
            con.glyphs[i].color = banner.color;
            con.labels[i].text = banner.label;
            con.labels[i].color = banner.color;
            con.plates[i].color = GamePalette.WithAlpha(banner.color, 0.16f);
        }
    }

    /// <summary>Quello che <paramref name="source"/> passa a <paramref name="target"/>, se e' un'insegna.</summary>
    void Collect(CardInstance source, CardInstance target, string arrow)
    {
        if (source == null || target == null) return;
        if (!source.alive || !target.alive) return;
        if (source.side != Side.Retro) return;
        if (source.def.faction != target.def.faction) return;

        var color = GamePalette.FactionColor(source.def.faction);

        // La spada vale solo se chi la riceve e' scoperto: da coperto non attacca.
        if (source.def.backDamageBonusSameFaction > 0 && target.side == Side.Fronte)
            _banners.Add(new Banner
            {
                glyph = GlyphSprites.Sword,
                color = color,
                label = $"+{source.def.backDamageBonusSameFaction} {arrow}",
            });

        if (source.def.backBlockBonusSameFaction > 0)
            _banners.Add(new Banner
            {
                glyph = GlyphSprites.Shield,
                color = color,
                label = $"+{source.def.backBlockBonusSameFaction} {arrow}",
            });
    }

    // ── Costruzione e geometria ───────────────────────────────────────────────

    void Rebuild(int lanes)
    {
        if (_columns.Count == lanes) return;

        UiBuild.ClearChildren(transform);
        _columns.Clear();
        _connectors.Clear();

        for (int i = 0; i < lanes; i++) _columns.Add(CreateColumn(i));
        for (int i = 0; i < Mathf.Max(0, lanes - 1); i++) _connectors.Add(CreateConnector(i));
    }

    Column CreateColumn(int index)
    {
        var col = new Column();
        col.root = UiBuild.Rect($"Lane{index + 1}", transform);
        col.root.anchorMin = col.root.anchorMax = new Vector2(0f, 0.5f);
        col.root.pivot = new Vector2(0.5f, 0.5f);
        col.root.sizeDelta = new Vector2(columnWidth, _rt.rect.height);

        // Lo scudo spezzato in coda alla riga: indica risonanza in questa corsia.
        var resRt = UiBuild.Rect("Resonance", col.root);
        UiBuild.Centered(resRt, 22f, 22f, 0f, 0f);
        col.resonance = UiBuild.Fill(resRt, GamePalette.Danger);
        col.resonance.sprite = GlyphSprites.BrokenShield;
        col.resonance.type = Image.Type.Simple;
        col.resonance.preserveAspect = true;
        col.resonance.enabled = false;

        return col;
    }

    Connector CreateConnector(int index)
    {
        var con = new Connector();
        con.root = UiBuild.Rect($"Insegne{index + 1}", transform);
        con.root.anchorMin = con.root.anchorMax = new Vector2(0f, 0.5f);
        con.root.pivot = new Vector2(0.5f, 0.5f);
        con.root.sizeDelta = new Vector2(96f, _rt.rect.height);

        const float w = 84f, h = 20f, gap = 3f;
        float top = _rt.rect.height * 0.5f - h * 0.5f - 2f;

        // Quattro: due insegne per carta (spada e scudo) in ognuna delle due
        // direzioni non capitano mai tutte insieme, ma tre si'.
        for (int i = 0; i < 4; i++)
        {
            var chipRt = UiBuild.Rect($"Chip{i}", con.root);
            UiBuild.Centered(chipRt, w, h, 0f, top - i * (h + gap));
            con.plates.Add(UiBuild.Fill(chipRt, GamePalette.WithAlpha(GamePalette.Neutral, 0.16f)));

            var glyphRt = UiBuild.Rect("Glyph", chipRt);
            UiBuild.Centered(glyphRt, 16f, 16f, -w * 0.5f + 12f, 0f);
            var glyph = UiBuild.Fill(glyphRt, GamePalette.Neutral);
            glyph.sprite = GlyphSprites.Sword;
            glyph.type = Image.Type.Simple;
            glyph.preserveAspect = true;
            con.glyphs.Add(glyph);

            var label = UiBuild.Text("Label", chipRt, string.Empty, 13f, GamePalette.Neutral,
                                     TextAlignmentOptions.Center, FontStyles.Bold);
            UiBuild.Centered(label.rectTransform, w - 26f, h, 10f, 0f);
            con.labels.Add(label);

            chipRt.gameObject.SetActive(false);
            con.chips.Add(chipRt);
        }

        return con;
    }

    void PlaceConnector(Connector con, float x)
    {
        var p = con.root.anchoredPosition;
        if (!Mathf.Approximately(p.x, x)) con.root.anchoredPosition = new Vector2(x, 0f);
    }

    /// <summary>Centro della corsia proiettato nello spazio dell'asse: segue swap e riordini.</summary>
    float LocalCenterX(RectTransform reference, int index)
    {
        if (index < 0 || index >= reference.childCount) return 0f;
        var lane = reference.GetChild(index) as RectTransform;
        if (lane == null) return 0f;

        Vector3 local = _rt.InverseTransformPoint(lane.TransformPoint(lane.rect.center));
        return local.x - _rt.rect.xMin;
    }
}
