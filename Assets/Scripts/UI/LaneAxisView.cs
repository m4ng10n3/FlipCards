using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Asse delle corsie: fra il fronte nemico e quello del giocatore, per ogni corsia
/// mostra l'esito previsto con i lati attuali, e nei gap fra le colonne accende i
/// connettori delle combo di adiacenza.
///
/// E' la traduzione a schermo di LaneResolver e SynergyResolver: senza, il
/// giocatore deve rifare a mente l'aritmetica del danno a ogni flip.
/// Le colonne si allineano leggendo la posizione reale delle corsie, quindi
/// seguono lo swap e funzionano con qualunque numero di corsie.
/// </summary>
public class LaneAxisView : MonoBehaviour
{
    [Header("Riferimenti")]
    public RectTransform laneReferenceRoot;   // di norma playerBoardRoot

    [Header("Geometria")]
    public float columnWidth = 240f;

    class Column
    {
        public RectTransform root;
        public TextMeshProUGUI main;
        public TextMeshProUGUI counter;
        public Image rule;
    }

    class Connector
    {
        public RectTransform root;
        public readonly List<Image> chips = new List<Image>();
        public readonly List<TextMeshProUGUI> labels = new List<TextMeshProUGUI>();
    }

    readonly List<Column> _columns = new List<Column>();
    readonly List<Connector> _connectors = new List<Connector>();
    readonly List<string> _combos = new List<string>(3);
    readonly StringBuilder _sb = new StringBuilder(48);

    RectTransform _rt;

    void Awake() => _rt = (RectTransform)transform;

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

        string main, counter = string.Empty;
        Color color;

        if (card != null && slot != null)
        {
            if (card.side == Side.Fronte)
            {
                int atk = card.ComputeAttackDamage();
                int def = slot.ComputeSelfBlock();
                int net = Mathf.Max(0, atk - def);
                main = Compose(true, atk, def, net);
                color = net > 0 ? GamePalette.Good : GamePalette.Neutral;

                // La carta colpisce per prima; se lo slot sopravvive in Fronte, risponde.
                if (slot.side == Side.Fronte && net < slot.health)
                    counter = $"risposta {Mathf.Max(0, slot.def.atkDamage + slot.tempAtkBonus - CardBlock(card))}";
            }
            else if (slot.side == Side.Fronte)
            {
                int atk = slot.def.atkDamage + slot.tempAtkBonus;
                int block = CardBlock(card);
                int net = Mathf.Max(0, atk - block);
                main = Compose(false, atk, block, net);
                color = net > 0 ? GamePalette.Danger : GamePalette.Neutral;
            }
            else
            {
                main = "—";
                color = GamePalette.Neutral;
                counter = "stallo";
            }
        }
        else if (card != null)
        {
            if (card.side == Side.Fronte)
            {
                main = $"{UiBuild.Arrow(true)} {card.ComputeAttackDamage()} → BOSS";
                color = GamePalette.Good;
            }
            else { main = "—"; color = GamePalette.Neutral; counter = "carica"; }
        }
        else if (slot != null)
        {
            if (slot.side == Side.Fronte)
            {
                // Corsia vuota: il danno salta la board e arriva agli HP. E' una falla.
                main = $"{UiBuild.Arrow(false)} {slot.def.atkDamage + slot.tempAtkBonus} → HP";
                color = GamePalette.Danger;
                counter = "corsia scoperta";
            }
            else { main = "—"; color = GamePalette.Neutral; }
        }
        else
        {
            main = "—";
            color = GamePalette.Neutral;
        }

        if (col.main.text != main) col.main.text = main;
        col.main.color = color;
        if (col.counter.text != counter) col.counter.text = counter;
        col.rule.color = GamePalette.WithAlpha(color, 0.45f);
    }

    string Compose(bool up, int power, int mitigation, int net)
    {
        _sb.Clear();
        _sb.Append(UiBuild.Arrow(up)).Append(' ').Append(power)
           .Append(" − ").Append(mitigation).Append(" = ").Append(net);
        return _sb.ToString();
    }

    static int CardBlock(CardInstance card)
        => (card.side == Side.Fronte ? card.def.frontBlockValue : card.def.backBlockValue) + card.tempBlockBonus;

    // ── Connettori di combo ───────────────────────────────────────────────────

    void RefreshConnector(GameManager gm, Connector con, int lane)
    {
        _combos.Clear();

        var left = gm.GetPlayerCardAtLane(lane);
        var right = gm.GetPlayerCardAtLane(lane + 1);

        if (left != null && right != null)
        {
            if (left.side == Side.Fronte && right.side == Side.Fronte &&
                left.def.cardClass == CardClass.Assalto && right.def.cardClass == CardClass.Assalto)
                _combos.Add("BLADE +1 ATK");

            bool hasGuard = left.def.cardClass == CardClass.Guardia || right.def.cardClass == CardClass.Guardia;
            bool hasRetro = left.side == Side.Retro || right.side == Side.Retro;
            if (hasGuard && hasRetro)
                _combos.Add("GUARD +1 BLK");

            var mystic = left.def.cardClass == CardClass.Mistico ? left :
                         right.def.cardClass == CardClass.Mistico ? right : null;
            var retro = left.side == Side.Retro ? left : right.side == Side.Retro ? right : null;
            if (mystic != null && retro != null && mystic != retro)
                _combos.Add("PULSE +1 CHG");
        }

        for (int i = 0; i < con.chips.Count; i++)
        {
            bool on = i < _combos.Count;
            con.chips[i].gameObject.SetActive(on);
            if (!on) continue;
            con.labels[i].text = _combos[i];
        }
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

        var ruleRt = UiBuild.Rect("Rule", col.root);
        UiBuild.Centered(ruleRt, columnWidth - 40f, 2f, 0f, 26f);
        col.rule = UiBuild.Fill(ruleRt, GamePalette.Neutral);

        col.main = UiBuild.Text("Main", col.root, "—", 26f, GamePalette.Neutral,
                                TextAlignmentOptions.Center, FontStyles.Bold);
        UiBuild.Centered(col.main.rectTransform, columnWidth, 32f, 0f, 4f);

        col.counter = UiBuild.Text("Counter", col.root, string.Empty, 15f, GamePalette.TextMuted,
                                   TextAlignmentOptions.Center);
        UiBuild.Centered(col.counter.rectTransform, columnWidth, 18f, 0f, -18f);

        return col;
    }

    Connector CreateConnector(int index)
    {
        var con = new Connector();
        con.root = UiBuild.Rect($"Combo{index + 1}", transform);
        con.root.anchorMin = con.root.anchorMax = new Vector2(0f, 0.5f);
        con.root.pivot = new Vector2(0.5f, 0.5f);
        con.root.sizeDelta = new Vector2(120f, _rt.rect.height);

        const float h = 18f, gap = 3f;
        for (int i = 0; i < 3; i++)
        {
            var chipRt = UiBuild.Rect($"Chip{i}", con.root);
            UiBuild.Centered(chipRt, 116f, h, 0f, 24f - i * (h + gap));
            var chip = UiBuild.Fill(chipRt, GamePalette.WithAlpha(GamePalette.Charge, 0.22f));

            var label = UiBuild.Text("Label", chipRt, string.Empty, 12f, GamePalette.Charge,
                                     TextAlignmentOptions.Center, FontStyles.Bold);
            UiBuild.Stretch(label.rectTransform);

            chipRt.gameObject.SetActive(false);
            con.chips.Add(chip);
            con.labels.Add(label);
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
