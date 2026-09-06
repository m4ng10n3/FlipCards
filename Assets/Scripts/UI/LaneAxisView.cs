using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Asse delle corsie: fra il fronte nemico e quello del giocatore dice, corsia
/// per corsia, cosa succede se si attacca adesso, e nei varchi fra una corsia e
/// l'altra disegna le insegne, cioe' i bonus che una carta coperta passa alla
/// vicina della sua fazione.
///
/// E' la traduzione a schermo di LaneResolver e SynergyResolver, e usa gli
/// stessi metodi che poi risolvono il colpo: quello che si legge qui e' quello
/// che succede. In particolare la guardia mostrata e' quella **efficace**
/// (<see cref="SynergyResolver.EffectiveSlotBlock"/>), quindi in una corsia in
/// risonanza si legge zero — che e' il punto della risonanza.
///
/// Le tre cose che il giocatore decide guardando questa banda:
///  - dove puo' <b>sfondare</b>, cioe' fare piu' danno della vita che resta alla
///    casella, perche' l'eccedenza la paga il boss;
///  - dove sta per <b>passare</b> un colpo, cioe' dove la sua carta non ha
///    abbastanza vita e il resto arriva ai suoi HP;
///  - dove conviene spostare un'insegna, perche' i varchi dicono a chi sta
///    dando il suo numero e a chi lo darebbe.
///
/// Le colonne si allineano leggendo la posizione reale delle corsie, quindi
/// seguono lo swap e funzionano con qualunque numero di corsie.
/// </summary>
public class LaneAxisView : MonoBehaviour
{
    [Header("Riferimenti")]
    public RectTransform laneReferenceRoot;   // di norma playerBoardRoot

    [Header("Geometria")]
    public float columnWidth = 320f;

    class Column
    {
        public RectTransform root;
        public TextMeshProUGUI main;
        public TextMeshProUGUI counter;
        public Image rule;
        public Image readout;    // freccia del kit: chi colpisce, o parata
        public Image resonance;  // scudo spezzato: in questa corsia nessuno para
    }

    /// <summary>Esito della corsia, nei tre indicatori che il kit disegna.</summary>
    enum Readout { None, Up, Down, Block }

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
    readonly StringBuilder _sb = new StringBuilder(48);

    RectTransform _rt;
    bool _skinnedReadout;

    void Awake()
    {
        _rt = (RectTransform)transform;
        _skinnedReadout = UiSkin.Sprite(UiSkin.ReadoutUp) != null;
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
            PlaceColumn(_columns[i], x);
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
        var slot = gm.GetEnemySlotAtLane(lane);
        bool resonant = SynergyResolver.Resonates(gm, lane);

        string main, counter = string.Empty;
        Color color;
        Readout readout;

        if (card != null && slot != null)
        {
            if (card.side == Side.Fronte)
            {
                int atk = card.ComputeAttackDamage() + (gm.CanAct ? SynergyResolver.AttackBonus(gm, lane) : 0);
                int guard = SynergyResolver.EffectiveSlotBlock(gm, lane);
                int net = Mathf.Max(0, atk - guard);
                main = Compose(true, atk, guard, net);
                color = net > 0 ? GamePalette.Good : GamePalette.Neutral;
                readout = net > 0 ? Readout.Up : Readout.Block;

                // Il traboccamento e' l'unica via al boss finche' la corazza
                // tiene: senza questa riga il giocatore vede "3" e non sa se
                // sta facendo qualcosa o riempiendo un secchio bucato.
                if (net > slot.health) counter = $"SFONDA · boss −{net - slot.health}";
                else if (net == slot.health) counter = "rompe la casella";
                else if (net > 0) counter = $"le restano {slot.health - net}";

                // La carta colpisce per prima; se la casella sopravvive ed e'
                // carica, risponde. Ed e' li' che si vede se la corsia regge.
                if (slot.side == Side.Fronte && net < slot.health)
                {
                    int back = Mathf.Max(0, slot.def.atkDamage + slot.tempAtkBonus
                                          - SynergyResolver.EffectiveCardBlock(gm, lane));
                    string risposta = back > card.health
                        ? $"risposta {back} · PASSA −{back - card.health}"
                        : $"risposta {back}";
                    counter = string.IsNullOrEmpty(counter) ? risposta : counter + " / " + risposta;
                }
            }
            else if (slot.side == Side.Fronte)
            {
                int atk = slot.def.atkDamage + slot.tempAtkBonus;
                int guard = SynergyResolver.EffectiveCardBlock(gm, lane);
                int net = Mathf.Max(0, atk - guard);
                main = Compose(false, atk, guard, net);
                color = net > 0 ? GamePalette.Danger : GamePalette.Neutral;
                readout = net > 0 ? Readout.Down : Readout.Block;

                // La copertura non e' infinita: quando la carta cede, il resto
                // del colpo arriva addosso al giocatore.
                if (net > card.health) counter = $"PASSA · tu −{net - card.health}";
                else if (net >= card.health && net > 0) counter = "la carta cade";
                else if (net > 0) counter = $"le restano {card.health - net}";
            }
            else
            {
                main = "—";
                color = GamePalette.Neutral;
                counter = "stallo · lei carica, tu carichi";
                readout = Readout.Block;
            }
        }
        else if (card != null)
        {
            if (card.side == Side.Fronte)
            {
                // Corsia senza casella: la corazza non copre e il colpo va tutto al boss.
                int atk = card.ComputeAttackDamage() + (gm.CanAct ? SynergyResolver.AttackBonus(gm, lane) : 0);
                main = $"{Arrow(true)}{atk} → BOSS";
                color = GamePalette.Good;
                counter = "corazza scoperta";
                readout = Readout.Up;
            }
            else { main = "—"; color = GamePalette.Neutral; counter = "carica"; readout = Readout.None; }
        }
        else if (slot != null)
        {
            if (slot.side == Side.Fronte)
            {
                // Corsia vuota: il danno salta la board e arriva agli HP. E' una falla.
                main = $"{Arrow(false)}{slot.def.atkDamage + slot.tempAtkBonus} → HP";
                color = GamePalette.Danger;
                counter = "corsia scoperta";
                readout = Readout.Down;
            }
            else { main = "—"; color = GamePalette.Neutral; readout = Readout.None; }
        }
        else
        {
            main = "—";
            color = GamePalette.Neutral;
            readout = Readout.None;
        }

        if (col.main.text != main) col.main.text = main;
        col.main.color = color;
        if (col.counter.text != counter) col.counter.text = counter;
        col.rule.color = GamePalette.WithAlpha(color, 0.45f);
        ApplyReadout(col, readout, color);
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

    /// <summary>
    /// L'indicatore del kit al posto del glifo: sale se colpisci tu, scende se
    /// colpiscono te, diventa lo scudo quando il colpo viene assorbito.
    /// </summary>
    void ApplyReadout(Column col, Readout readout, Color color)
    {
        if (col.readout == null) return;

        if (readout == Readout.None)
        {
            col.readout.enabled = false;
            return;
        }

        var sprite = UiSkin.Sprite(readout switch
        {
            Readout.Up => UiSkin.ReadoutUp,
            Readout.Down => UiSkin.ReadoutDown,
            _ => UiSkin.ReadoutBlock,
        });

        if (sprite == null) { col.readout.enabled = false; return; }

        col.readout.enabled = true;
        col.readout.sprite = sprite;
        col.readout.color = color;
    }

    /// <summary>Freccia testuale: serve solo dove non c'e' lo sprite del kit.</summary>
    string Arrow(bool up) => _skinnedReadout ? string.Empty : UiBuild.Arrow(up) + " ";

    string Compose(bool up, int power, int mitigation, int net)
    {
        _sb.Clear();
        _sb.Append(Arrow(up)).Append(power)
           .Append(" − ").Append(mitigation).Append(" = ").Append(net);
        return _sb.ToString();
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

        // Le quote seguono l'altezza reale della banda: l'asse si e' gia'
        // accorciato una volta col layout e le costanti erano tarate su 64.
        float half = _rt.rect.height * 0.5f;

        var ruleRt = UiBuild.Rect("Rule", col.root);
        UiBuild.Centered(ruleRt, columnWidth - 60f, 2f, 0f, half - 6f);
        col.rule = UiBuild.Fill(ruleRt, GamePalette.Neutral);

        col.main = UiBuild.Text("Main", col.root, "—", 24f, GamePalette.Neutral,
                                TextAlignmentOptions.Center, FontStyles.Bold);
        UiBuild.Centered(col.main.rectTransform, columnWidth, 28f, _skinnedReadout ? 16f : 0f, 2f);

        if (_skinnedReadout)
        {
            var iconRt = UiBuild.Rect("Readout", col.root);
            UiBuild.Centered(iconRt, 28f, 28f, -columnWidth * 0.5f + 40f, 2f);
            col.readout = UiBuild.Fill(iconRt, GamePalette.Neutral);
            col.readout.sprite = UiSkin.Sprite(UiSkin.ReadoutUp);
            col.readout.type = Image.Type.Simple;
            col.readout.enabled = false;
        }

        // Lo scudo spezzato in coda alla riga, dalla parte opposta alla freccia:
        // e' un secondo indicatore e non deve leggersi come parte del numero.
        var resRt = UiBuild.Rect("Resonance", col.root);
        UiBuild.Centered(resRt, 26f, 26f, columnWidth * 0.5f - 34f, 2f);
        col.resonance = UiBuild.Fill(resRt, GamePalette.Danger);
        col.resonance.sprite = GlyphSprites.BrokenShield;
        col.resonance.type = Image.Type.Simple;
        col.resonance.preserveAspect = true;
        col.resonance.enabled = false;

        col.counter = UiBuild.Text("Counter", col.root, string.Empty, 14f, GamePalette.TextMuted,
                                   TextAlignmentOptions.Center);
        UiBuild.Centered(col.counter.rectTransform, columnWidth, 16f, 0f, -half + 8f);

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

    void PlaceColumn(Column col, float x)
    {
        var p = col.root.anchoredPosition;
        if (!Mathf.Approximately(p.x, x)) col.root.anchoredPosition = new Vector2(x, 0f);
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
