using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Chrome della cella carta: barra nome, badge di classe e fazione, traccia delle
/// cariche, sottolineature delle statistiche e fascia di lato.
///
/// Approccio simbolico: sulla cella non compaiono etichette testuali. Il ruolo di
/// ogni numero e' dato dal colore (rosso attacco, verde vita, blu blocco) e dalla
/// sottolineatura dello stesso colore; classe e fazione sono pastiglie colorate.
/// Tutto il dettaglio — passive, abilita', valori per lato — sta nell'ispettore.
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
    // Bande della cella carta, coordinate dall'alto. Le legge anche il builder,
    // che ci posiziona i Text del prefab: un solo posto per questi numeri.
    public const float NameY = 2f, NameH = 26f;
    public const float BadgeY = 212f, BadgeSize = 24f;
    public const float ChargeY = 248f, ChargeH = 12f;
    public const float StatY = 264f, StatH = 28f, StatW = 66f;
    public const float RuleY = 292f, RuleH = 3f;
    public const float SideBandY = 300f, SideBandH = 30f;

    public static float StatX(int index) => 8f + index * (StatW + 3f);

    CardView _view;
    CardDefinition _definition;
    RectTransform _rt;

    Image _sideBand;
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
                _sideBand.color = GamePalette.Neutral;
                _sideLabel.text = "IN MANO";
            }
            return;
        }

        if (inst.side != _lastSide)
        {
            _lastSide = inst.side;
            _sideBand.color = GamePalette.SideColor(inst.side);
            _sideLabel.text = inst.side == Side.Fronte ? "FRONTE" : "RETRO";
        }

        if (inst.flipCharge != _lastCharge)
        {
            _lastCharge = inst.flipCharge;
            for (int i = 0; i < _chargeNotches.Count; i++)
                _chargeNotches[i].color = i < inst.flipCharge
                    ? GamePalette.Charge
                    : GamePalette.WithAlpha(GamePalette.Charge, 0.28f);
        }
    }

    void Build()
    {
        _built = true;

        float w = _rt.rect.width;
        var def = _definition.BuildSpec();

        // I fondi vanno creati PRIMA di rialzare i Text del prefab, altrimenti li
        // coprirebbero: i figli aggiunti dopo disegnano sopra.
        var nameBar = UiBuild.Rect("NameBar", _rt);
        UiBuild.Band(nameBar, 4f, NameY, w - 8f, NameH);
        UiBuild.Fill(nameBar, GamePalette.WithAlpha(Color.black, 0.66f));

        // Traccia cariche: tre tacche piene = tre cariche. Era testo "[2/3]" in 26 px.
        var trackRt = UiBuild.Rect("ChargeTrack", _rt);
        UiBuild.Band(trackRt, 8f, ChargeY, w - 16f, ChargeH);
        UiBuild.Fill(trackRt, GamePalette.WithAlpha(Color.black, 0.66f));

        const float gap = 3f;
        float notch = (w - 16f - gap * (CardInstance.MaxFlipCharge - 1)) / CardInstance.MaxFlipCharge;
        for (int i = 0; i < CardInstance.MaxFlipCharge; i++)
        {
            var nRt = UiBuild.Rect($"Notch{i}", trackRt);
            UiBuild.Band(nRt, i * (notch + gap), 2f, notch, ChargeH - 4f);
            _chargeNotches.Add(UiBuild.Fill(nRt, GamePalette.Charge));
        }

        // Fondo dietro i numeri, e la riga colorata che ne dichiara il ruolo.
        var statBg = UiBuild.Rect("StatRow", _rt);
        UiBuild.Band(statBg, 8f, StatY, w - 16f, StatH);
        UiBuild.Fill(statBg, GamePalette.WithAlpha(Color.black, 0.66f));

        Rule(0, GamePalette.Danger);
        Rule(1, GamePalette.PlayerHp);
        Rule(2, GamePalette.Retro);

        // Fascia di lato: a tutta larghezza sul bordo basso, il dato piu' letto.
        var bandRt = UiBuild.Rect("SideBand", _rt);
        UiBuild.Band(bandRt, 0f, SideBandY, w, SideBandH);
        _sideBand = UiBuild.Fill(bandRt, GamePalette.Neutral);

        _sideLabel = UiBuild.Text("Label", bandRt, string.Empty, 15f, new Color(0.05f, 0.06f, 0.09f, 1f),
                                  TextAlignmentOptions.Center, FontStyles.Bold);
        UiBuild.Stretch(_sideLabel.rectTransform);

        // Classe e fazione: pastiglie sugli angoli bassi dell'artwork. La classe
        // guida le combo di adiacenza, quindi non puo' essere solo nell'ispettore.
        Badge("ClassBadge", 6f, GamePalette.ClassColor(def.cardClass), Initial(def.cardClass.ToString()));
        Badge("FactionBadge", w - 6f - BadgeSize, GamePalette.FactionColor(def.faction), def.faction.ToString());

        RaisePrefabTexts();
    }

    void Rule(int index, Color color)
    {
        var rt = UiBuild.Rect($"Rule{index}", _rt);
        UiBuild.Band(rt, StatX(index), RuleY, StatW, RuleH);
        UiBuild.Fill(rt, color);
    }

    void Badge(string name, float x, Color color, string label)
    {
        var rt = UiBuild.Rect(name, _rt);
        UiBuild.Band(rt, x, BadgeY, BadgeSize, BadgeSize);
        UiBuild.Fill(rt, color);

        var text = UiBuild.Text("Label", rt, label, 14f, new Color(0.05f, 0.06f, 0.09f, 1f),
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
