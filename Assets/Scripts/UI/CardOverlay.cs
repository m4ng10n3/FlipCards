using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Chrome della cella carta: barra nome, badge di classe e fazione, traccia delle
/// cariche, sottolineature delle statistiche e fascia di lato.
///
/// Approccio simbolico: sulla cella non compaiono etichette testuali. Il ruolo di
/// ogni numero e' dato dal colore (rosso attacco, verde vita, ciano blocco) e dalla
/// sottolineatura dello stesso colore; classe e fazione sono pastiglie colorate.
/// Tutto il dettaglio — passive, abilita', valori per lato — sta nell'ispettore.
///
/// **Il chrome e' traslucido di proposito.** Il Template della carta monta
/// CardShaderGraph, che l'edizione POLYCHROME lega alla rotazione della carta
/// (ShaderCode scrive <c>_Rotation</c> da <c>transform.parent.localRotation</c>):
/// se i fondi fossero opachi, il riflesso che scorre al tilt resterebbe sotto e
/// non lo vedrebbe nessuno. Nessun fondo di questa classe supera alpha ~0.55.
///
/// Funziona anche sulle carte in mano, che non hanno ancora una CardInstance:
/// in quel caso legge la Spec dal CardDefinition.
///
/// Nessun elemento e' Raycast Target: FindBoardCardUnderPointer prende il primo
/// hit e ci cerca dentro un CardView, quindi un figlio che intercetta il click
/// romperebbe lo swap per trascinamento.
/// </summary>
[DisallowMultipleComponent]
public class CardOverlay : MonoBehaviour
{
    // ── Anatomia della cella carta ────────────────────────────────────────────
    //
    // Coordinate banda (origine in alto a sinistra), gia' in scala 2x: sono i
    // numeri di `layouts.card` in flipcards_ui_manifest.json moltiplicati per 2,
    // cioe' la cella 112x168 del kit portata a 224x336 sul canvas 1920x1080.
    // Le legge anche il builder, che ci posiziona i Text del prefab: un solo
    // posto per questi numeri.

    public const float CardW = 224f, CardH = 336f;

    public const float NameX = 12f, NameY = 12f, NameW = 200f, NameH = 34f;
    public const float ArtX = 32f, ArtY = 52f, ArtW = 160f, ArtH = 160f;

    public const float StatY = 220f, StatH = 36f, StatW = 64f;
    public const float RuleY = 256f, RuleH = 3f;
    public const float ChargeY = 264f, ChargeH = 22f;
    public const float StripY = 290f, StripH = 34f;

    public const float BadgeSize = 26f;
    public const float FactionBadgeX = NameX + NameW - BadgeSize - 2f;   // in coda alla barra nome
    public const float FactionBadgeY = NameY + 4f;

    /// <summary>Colonna dell'i-esimo numero: tre chip da 64 con 4 di gap dentro i 200 utili.</summary>
    public static float StatX(int index) => NameX + index * (StatW + 4f);

    // Alpha dei fondi. Tenerli bassi non e' un vezzo: sotto c'e' lo shader.
    const float PlateAlpha = 0.46f;
    const float StripAlpha = 0.55f;

    CardView _view;
    CardDefinition _definition;
    RectTransform _rt;

    Image _sideRule;
    TextMeshProUGUI _sideLabel;
    readonly List<Image> _chargeNotches = new List<Image>(CardInstance.MaxFlipCharge);

    Side _lastSide = (Side)(-1);
    int _lastCharge = -1;
    bool _built;

    void Awake()
    {
        _view = GetComponent<CardView>();
        _definition = GetComponentInParent<CardDefinition>();
        _rt = (RectTransform)transform;
    }

    void LateUpdate()
    {
        if (_view == null || _definition == null) return;
        if (!_built) Build();

        var inst = _view.instance;

        // Carta in mano: nessuna istanza, quindi nessun lato e nessuna carica.
        // Le statistiche le scrive gia' CardView.Awake dalla Spec.
        if (inst == null)
        {
            if (_lastSide != (Side)(-2))
            {
                _lastSide = (Side)(-2);
                _sideRule.color = GamePalette.Neutral;
                _sideLabel.color = GamePalette.TextMuted;
                _sideLabel.text = "IN MANO";
            }
            return;
        }

        if (inst.side != _lastSide)
        {
            _lastSide = inst.side;
            var color = GamePalette.SideColor(inst.side);
            _sideRule.color = color;
            _sideLabel.color = color;
            _sideLabel.text = inst.side == Side.Fronte ? "FRONTE" : "RETRO";
        }

        if (inst.flipCharge != _lastCharge)
        {
            _lastCharge = inst.flipCharge;
            for (int i = 0; i < _chargeNotches.Count; i++)
                _chargeNotches[i].color = i < inst.flipCharge
                    ? GamePalette.Charge
                    : GamePalette.WithAlpha(GamePalette.Charge, 0.22f);
        }
    }

    void Build()
    {
        _built = true;

        var def = _definition.BuildSpec();

        // I fondi vanno creati PRIMA di rialzare i Text del prefab, altrimenti li
        // coprirebbero: i figli aggiunti dopo disegnano sopra.
        Plate("NameBar", NameX, NameY, NameW, NameH);

        // Traccia cariche: tre tacche piene = tre cariche. Era testo "[2/3]" in 26 px.
        var trackRt = Plate("ChargeTrack", NameX, ChargeY, NameW, ChargeH);

        const float gap = 4f;
        float notch = (NameW - gap * (CardInstance.MaxFlipCharge - 1)) / CardInstance.MaxFlipCharge;
        for (int i = 0; i < CardInstance.MaxFlipCharge; i++)
        {
            var nRt = UiBuild.Rect($"Notch{i}", trackRt);
            UiBuild.Band(nRt, i * (notch + gap), 3f, notch, ChargeH - 6f);
            _chargeNotches.Add(UiBuild.Fill(nRt, GamePalette.Charge));
        }

        // Fondo dietro i numeri, e la riga colorata che ne dichiara il ruolo.
        Plate("StatRow", NameX, StatY, NameW, StatH);

        Rule(0, GamePalette.Danger);
        Rule(1, GamePalette.PlayerHp);
        Rule(2, GamePalette.Retro);

        // Fascia bassa: il lato, cioe' il dato piu' letto della cella. Fondo scuro
        // e testo colorato invece di una fascia piena — una fascia opaca su tutta
        // la larghezza spegnerebbe il riflesso dello shader proprio dove si vede
        // meglio, sul bordo basso.
        var strip = Plate("SideStrip", NameX, StripY, NameW, StripH, StripAlpha);

        var ruleRt = UiBuild.Rect("Rule", strip);
        UiBuild.Band(ruleRt, 0f, 0f, NameW, 3f);
        _sideRule = UiBuild.Fill(ruleRt, GamePalette.Neutral);

        _sideLabel = UiBuild.Text("Label", strip, string.Empty, 15f, GamePalette.TextMuted,
                                  TextAlignmentOptions.Center, FontStyles.Bold);
        UiBuild.Stretch(_sideLabel.rectTransform, 30f, 3f, 4f, 0f);

        // Classe e fazione: la classe in testa alla fascia bassa, la fazione in
        // coda alla barra nome — come il tag di fazione del kit.
        Badge("ClassBadge", strip, 4f, 4f, GamePalette.ClassColor(def.cardClass), Initial(def.cardClass.ToString()));
        Badge("FactionBadge", _rt, FactionBadgeX, FactionBadgeY, GamePalette.FactionColor(def.faction), def.faction.ToString());

        RaisePrefabTexts();
    }

    /// <summary>Fondo scuro traslucido: sotto passa lo shader della carta.</summary>
    RectTransform Plate(string name, float x, float y, float w, float h, float alpha = PlateAlpha)
    {
        var rt = UiBuild.Rect(name, _rt);
        UiBuild.Band(rt, x, y, w, h);
        UiBuild.Fill(rt, GamePalette.WithAlpha(GamePalette.PanelSunken, alpha));
        return rt;
    }

    void Rule(int index, Color color)
    {
        var rt = UiBuild.Rect($"Rule{index}", _rt);
        UiBuild.Band(rt, StatX(index), RuleY, StatW, RuleH);
        UiBuild.Fill(rt, color);
    }

    void Badge(string name, RectTransform parent, float x, float y, Color color, string label)
    {
        var rt = UiBuild.Rect(name, parent);
        UiBuild.Band(rt, x, y, BadgeSize, BadgeSize);
        UiBuild.Fill(rt, color);

        var text = UiBuild.Text("Label", rt, label, 15f, GamePalette.Background,
                                TextAlignmentOptions.Center, FontStyles.Bold);
        UiBuild.Stretch(text.rectTransform);
    }

    static string Initial(string value) => string.IsNullOrEmpty(value) ? "?" : value.Substring(0, 1);

    /// <summary>
    /// I Text del prefab devono disegnare sopra i fondi appena creati, e devono
    /// essere accesi: le carte in mano non passano mai da ApplySideVisuals, ed e'
    /// il motivo per cui la vita non si vedeva.
    /// </summary>
    void RaisePrefabTexts()
    {
        Raise(_view.nameText);
        Raise(_view.AttackPwrText);
        Raise(_view.hpText);
        Raise(_view.BlockPwrText);

        var hint = _rt.Find("HintText");
        if (hint != null) hint.SetAsLastSibling();
    }

    static void Raise(Graphic graphic)
    {
        if (graphic == null) return;
        graphic.enabled = true;
        graphic.transform.SetAsLastSibling();
    }
}
