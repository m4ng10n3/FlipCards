using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// La cassa del rullo nemico: fondo, caselle parziali sopra e sotto, alone della
/// colonna che sta per colpire, striature mentre gira, e davanti a tutto cornice,
/// payline e vetro.
///
/// Serve a dire una regola che il gioco non spiega mai a parole: **il fronte
/// nemico non e' una fila di carte, e' un rullo**. Non si tocca, gira da solo a
/// fine turno, e l'unica cosa che il giocatore puo' fare e' leggerlo in tempo.
/// La forma della cassa lo dice prima di qualunque numero; l'alone di colonna
/// dice quale corsia colpira' in questo giro, che e' l'unica decisione che il
/// tavolo gli chiede.
///
/// Sta in due strati perche' le caselle stanno in mezzo:
/// <c>underLayer</c> (fondo, sliver, blur, alone) e' fratello **prima** di
/// AIBoardRoot, <c>overLayer</c> (cornice, payline, vetro) **dopo**. Nessuno dei
/// due e' Raycast Target: un vetro che intercetta il puntatore spegnerebbe hover
/// e ispettore su tutte le caselle.
///
/// Senza kit non disegna niente e il tabellone resta quello di prima: la cassa e'
/// pelle, non layout.
/// </summary>
[DisallowMultipleComponent]
public class ReelChrome : MonoBehaviour
{
    [Header("Strati")]
    [Tooltip("Sotto le caselle: fondo cassa, sliver, blur di rotazione, alone di colonna.")]
    public RectTransform underLayer;
    [Tooltip("Sopra le caselle: cornice metallica, payline, vetro.")]
    public RectTransform overLayer;

    [Header("Riferimenti")]
    [Tooltip("AIBoardRoot: da qui si leggono i centri reali delle colonne.")]
    public RectTransform laneReferenceRoot;

    [Header("Geometria della cassa")]
    [Tooltip("Distanza fra il bordo alto della cassa e quello della casella.")]
    public float cellTop = 56f;
    public float cellWidth = SlotOverlay.CellW;
    public float cellHeight = SlotOverlay.CellH;
    [Tooltip("Altezza delle caselle parziali sopra e sotto la payline.")]
    public float sliverHeight = 40f;
    [Tooltip("Bleed dell'alone di colonna oltre la casella, per lato.")]
    public float highlightBleed = 12f;

    class Column
    {
        public RectTransform root;
        public Image blur;
        public Image highlight;
        public Image payout;
    }

    readonly List<Column> _columns = new List<Column>();
    SlotBatchManager _batch;
    bool _built;
    int _lastArmedMask = -1;
    bool _lastRolling;
    int _lastPayoutSerial = -1;

    void LateUpdate()
    {
        var gm = GameManager.Instance;
        if (gm == null || underLayer == null) return;

        var reference = laneReferenceRoot != null ? laneReferenceRoot : gm.aiBoardRoot as RectTransform;
        if (reference == null) return;

        if (!_built) Build();

        int lanes = reference.childCount;
        Rebuild(lanes);

        if (_batch == null) _batch = Object.FindAnyObjectByType<SlotBatchManager>();
        bool rolling = _batch != null && _batch.IsRolling;

        int armedMask = 0;
        for (int i = 0; i < lanes && i < _columns.Count; i++)
        {
            Place(_columns[i], LocalCenterX(reference, i));

            var slot = gm.GetEnemySlotAtLane(i);
            // Mentre il rullo gira non c'e' niente da preannunciare: le caselle
            // che si vedono passare non sono ancora quelle del prossimo turno.
            bool armed = !rolling && slot != null && slot.alive && slot.side == Side.Fronte;
            if (armed) armedMask |= 1 << i;
        }

        // La vincita si annuncia prima dell'uscita anticipata: e' un evento, non
        // uno stato, e senza questo controllo passerebbe inosservata nei giri in
        // cui nient'altro cambia.
        if (gm.RollPayoutSerial != _lastPayoutSerial)
        {
            _lastPayoutSerial = gm.RollPayoutSerial;
            if (_lastPayoutSerial > 0) FlashPayout(gm.RollPayoutLanes, gm.RollPayoutJackpot);
        }

        if (armedMask == _lastArmedMask && rolling == _lastRolling) return;
        _lastArmedMask = armedMask;
        _lastRolling = rolling;

        for (int i = 0; i < _columns.Count; i++)
        {
            if (_columns[i].highlight != null)
                _columns[i].highlight.enabled = (armedMask & (1 << i)) != 0;
            if (_columns[i].blur != null)
                _columns[i].blur.enabled = rolling;
        }
    }

    // ── Costruzione ───────────────────────────────────────────────────────────

    void Build()
    {
        _built = true;

        Layer(underLayer, "Backing", UiSkin.ReelBacking, 0f, 0f, underLayer.rect.width, underLayer.rect.height, 1f);

        if (overLayer == null) return;

        Layer(overLayer, "Frame", UiSkin.ReelFrame, 0f, 0f, overLayer.rect.width, overLayer.rect.height, 1f);

        // La payline attraversa le caselle: e' la riga su cui il rullo "si ferma".
        // Nel gioco non decide nulla, ma e' cio' che rende la fila un rullo.
        var payline = UiSkin.Sprite(UiSkin.ReelPayline);
        if (payline != null)
        {
            float h = payline.rect.height;
            Layer(overLayer, "Payline", UiSkin.ReelPayline,
                  0f, cellTop + cellHeight * 0.5f - h * 0.5f, overLayer.rect.width, h, 1f);
        }

        // Il vetro va sopra a tutto: riflesso in alto, ombra interna in basso.
        // Tenuto basso di alpha, o spegnerebbe i simboli sotto.
        Layer(overLayer, "Glass", UiSkin.ReelGlass, 0f, 0f, overLayer.rect.width, overLayer.rect.height, 0.55f);
    }

    void Rebuild(int lanes)
    {
        if (_columns.Count == lanes) return;

        for (int i = _columns.Count - 1; i >= 0; i--)
        {
            if (_columns[i].root != null) Destroy(_columns[i].root.gameObject);
        }
        _columns.Clear();
        _lastArmedMask = -1;

        for (int i = 0; i < lanes; i++) _columns.Add(CreateColumn(i));
    }

    Column CreateColumn(int index)
    {
        var col = new Column();
        col.root = UiBuild.Rect($"Column{index + 1}", underLayer);
        col.root.anchorMin = col.root.anchorMax = new Vector2(0f, 1f);
        col.root.pivot = new Vector2(0.5f, 1f);
        col.root.sizeDelta = new Vector2(cellWidth, underLayer.rect.height);
        col.root.anchoredPosition = Vector2.zero;

        // Caselle parziali: quello che si intravede della casella precedente e
        // della successiva, come nella finestra di una slot machine.
        Layer(col.root, "SliverTop", UiSkin.ReelSliverTop,
              0f, cellTop - sliverHeight, cellWidth, sliverHeight, 1f);
        Layer(col.root, "SliverBottom", UiSkin.ReelSliverBottom,
              0f, cellTop + cellHeight, cellWidth, sliverHeight, 1f);

        float colTop = cellTop - sliverHeight;
        float colHeight = cellHeight + sliverHeight * 2f;

        col.blur = Layer(col.root, "Blur", UiSkin.ReelColBlur, 0f, colTop, cellWidth, colHeight, 0.9f);
        if (col.blur != null) col.blur.enabled = false;

        col.highlight = Layer(col.root, "Highlight", UiSkin.ReelColHighlight,
                              -highlightBleed, colTop - highlightBleed,
                              cellWidth + highlightBleed * 2f, colHeight + highlightBleed * 2f, 1f);
        if (col.highlight == null)
        {
            // Ripiego senza kit: una cornice piena tinta d'ambra dice comunque
            // "questa colonna colpisce".
            var rt = UiBuild.Rect("Highlight", col.root);
            UiBuild.Band(rt, -highlightBleed, colTop - highlightBleed,
                         cellWidth + highlightBleed * 2f, colHeight + highlightBleed * 2f);
            col.highlight = UiBuild.Fill(rt, GamePalette.WithAlpha(GamePalette.Fronte, 0.14f));
        }
        col.highlight.enabled = false;

        // Strato separato dall'alone di attacco: i due significati possono
        // capitare insieme (una colonna che paga E che colpisce) e devono
        // restare leggibili come due cose diverse.
        var payoutRt = UiBuild.Rect("Payout", col.root);
        UiBuild.Band(payoutRt, -highlightBleed, colTop - highlightBleed,
                     cellWidth + highlightBleed * 2f, colHeight + highlightBleed * 2f);
        col.payout = UiBuild.Fill(payoutRt, GamePalette.WithAlpha(GamePalette.Good, 0f));
        var payoutSprite = UiSkin.Sprite(UiSkin.ReelColHighlight);
        if (payoutSprite != null) col.payout.sprite = payoutSprite;
        col.payout.enabled = false;

        return col;
    }

    /// <summary>
    /// Il lampo di vincita sulle colonne che hanno fatto combinazione. E' il
    /// momento di pagamento della macchina: senza, la coppia esiste solo come
    /// riga di testo nella HUD e il giro non ha un esito che si guarda.
    /// </summary>
    void FlashPayout(int laneMask, bool jackpot)
    {
        int pulses = jackpot ? 5 : 3;
        float duration = jackpot ? 1.1f : 0.7f;
        float peak = jackpot ? 0.55f : 0.35f;

        for (int i = 0; i < _columns.Count; i++)
        {
            var img = _columns[i].payout;
            if (img == null) continue;

            img.DOKill();
            if ((laneMask & (1 << i)) == 0) { img.enabled = false; continue; }

            img.enabled = true;
            img.color = GamePalette.WithAlpha(jackpot ? GamePalette.Fronte : GamePalette.Good, 0f);
            var target = img;
            DOTween.Sequence()
                   .SetUpdate(true)
                   .SetLink(img.gameObject)
                   .Append(img.DOFade(peak, duration / pulses).SetLoops(pulses * 2, LoopType.Yoyo))
                   .OnComplete(() => { if (target != null) target.enabled = false; });
        }
    }

    static Image Layer(RectTransform parent, string name, string key,
                       float x, float y, float w, float h, float alpha)
    {
        if (parent == null) return null;

        var sprite = UiSkin.Sprite(key);
        if (sprite == null) return null;

        var rt = UiBuild.Rect(name, parent);
        UiBuild.Band(rt, x, y, w, h);

        var img = UiBuild.Fill(rt, new Color(1f, 1f, 1f, alpha));
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.preserveAspect = false;
        return img;
    }

    /// <summary>Centro della colonna proiettato nello spazio della cassa: segue le corsie reali.</summary>
    float LocalCenterX(RectTransform reference, int index)
    {
        if (index < 0 || index >= reference.childCount) return 0f;
        var lane = reference.GetChild(index) as RectTransform;
        if (lane == null) return 0f;

        Vector3 local = underLayer.InverseTransformPoint(lane.TransformPoint(lane.rect.center));
        return local.x - underLayer.rect.xMin;
    }

    static void Place(Column col, float x)
    {
        var p = col.root.anchoredPosition;
        if (!Mathf.Approximately(p.x, x)) col.root.anchoredPosition = new Vector2(x, 0f);
    }
}
