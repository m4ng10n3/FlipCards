using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Chrome aggiunto alla cella carta: fascia di lato, traccia delle cariche, badge
/// di classe e fazione, icone delle abilita'.
///
/// Si costruisce da codice sopra l'artwork esistente invece di essere disegnato
/// nel prefab: cosi le dieci carte restano allineate fra loro e la classe — che
/// e' la chiave delle combo di adiacenza — smette di essere invisibile.
///
/// Nessun elemento e' Raycast Target: FindBoardCardUnderPointer prende il primo
/// hit e ci cerca dentro un CardView, quindi un figlio che intercetta il click
/// romperebbe lo swap per trascinamento.
/// </summary>
[DisallowMultipleComponent]
public class CardOverlay : MonoBehaviour
{
    // Bande della cella carta (LAYOUT_SPEC §6.5), coordinate dall'alto.
    const float NameY = 2f, NameH = 30f;
    const float BadgeY = 40f, BadgeH = 22f;
    const float ChipY = 198f, ChipH = 44f, ChipW = 68f;
    const float ChargeY = 248f, ChargeH = 16f;
    const float AbilityY = 270f, AbilitySize = 24f;
    const float SideBandY = 302f, SideBandH = 28f;

    CardView _view;
    RectTransform _rt;

    Image _sideBand;
    TextMeshProUGUI _sideLabel;
    readonly List<Image> _chargeNotches = new List<Image>(CardInstance.MaxFlipCharge);
    Image _classChip;
    TextMeshProUGUI _classLabel;
    Image _factionChip;

    Side _lastSide = (Side)(-1);
    int _lastCharge = -1;
    bool _built;

    void Awake()
    {
        _view = GetComponent<CardView>();
        _rt = (RectTransform)transform;
    }

    void LateUpdate()
    {
        if (_view == null || _view.instance == null) return;
        if (!_built) Build();

        var inst = _view.instance;

        if (inst.side != _lastSide)
        {
            _lastSide = inst.side;
            var color = GamePalette.SideColor(inst.side);
            _sideBand.color = color;
            _sideLabel.text = inst.side == Side.Fronte ? "FRONTE · ATTACCA" : "RETRO · CARICA";
            _sideLabel.color = new Color(0.05f, 0.06f, 0.09f, 1f);
        }

        if (inst.flipCharge != _lastCharge)
        {
            _lastCharge = inst.flipCharge;
            for (int i = 0; i < _chargeNotches.Count; i++)
                _chargeNotches[i].color = i < inst.flipCharge
                    ? GamePalette.Charge
                    : GamePalette.WithAlpha(GamePalette.Charge, 0.30f);
        }
    }

    void Build()
    {
        _built = true;

        float w = _rt.rect.width;
        var def = _view.instance.def;

        // Fondi delle bande: vanno creati PRIMA di rialzare i Text del prefab,
        // altrimenti li coprirebbero (i figli aggiunti dopo disegnano sopra).
        var nameBar = UiBuild.Rect("NameBar", _rt);
        UiBuild.Band(nameBar, 4f, NameY, w - 8f, NameH);
        UiBuild.Fill(nameBar, GamePalette.WithAlpha(Color.black, 0.62f));

        Chip("AtkChipBg", 4f, GamePalette.Danger);
        Chip("HpChipBg", 4f + ChipW + 4f, GamePalette.PlayerHp);
        Chip("BlockChipBg", 4f + (ChipW + 4f) * 2f, GamePalette.Retro);

        // Traccia cariche: tre tacche piene = tre cariche. Era testo "[2/3]" in 26 px.
        var trackRt = UiBuild.Rect("ChargeTrack", _rt);
        UiBuild.Band(trackRt, 0f, ChargeY, w, ChargeH);
        UiBuild.Fill(trackRt, GamePalette.WithAlpha(Color.black, 0.62f));

        const float pad = 8f, gap = 4f;
        float notch = (w - pad * 2f - gap * (CardInstance.MaxFlipCharge - 1)) / CardInstance.MaxFlipCharge;
        for (int i = 0; i < CardInstance.MaxFlipCharge; i++)
        {
            var nRt = UiBuild.Rect($"Notch{i}", trackRt);
            UiBuild.Band(nRt, pad + i * (notch + gap), 3f, notch, ChargeH - 6f);
            _chargeNotches.Add(UiBuild.Fill(nRt, GamePalette.Charge));
        }

        // Fascia di lato: a tutta larghezza sul bordo basso, leggibile a colpo d'occhio.
        var bandRt = UiBuild.Rect("SideBand", _rt);
        UiBuild.Band(bandRt, 0f, SideBandY, w, SideBandH);
        _sideBand = UiBuild.Fill(bandRt, GamePalette.Fronte);

        _sideLabel = UiBuild.Text("Label", bandRt, string.Empty, 13f, Color.black,
                                  TextAlignmentOptions.Center, FontStyles.Bold);
        UiBuild.Stretch(_sideLabel.rectTransform);

        // Badge classe: guida le combo di adiacenza, quindi non puo' mancare.
        var classRt = UiBuild.Rect("ClassBadge", _rt);
        UiBuild.Band(classRt, 4f, BadgeY, 96f, BadgeH);
        _classChip = UiBuild.Fill(classRt, GamePalette.WithAlpha(GamePalette.ClassColor(def.cardClass), 0.95f));
        _classLabel = UiBuild.Text("Label", classRt, def.cardClass.ToString().ToUpperInvariant(), 13f,
                                   new Color(0.05f, 0.06f, 0.09f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);
        UiBuild.Stretch(_classLabel.rectTransform);

        // Badge fazione: stesso colore usato ovunque per quella fazione.
        var facRt = UiBuild.Rect("FactionBadge", _rt);
        UiBuild.Band(facRt, w - 30f, BadgeY, 26f, BadgeH);
        _factionChip = UiBuild.Fill(facRt, GamePalette.FactionColor(def.faction));
        var facLabel = UiBuild.Text("Label", facRt, def.faction.ToString(), 14f,
                                    new Color(0.05f, 0.06f, 0.09f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);
        UiBuild.Stretch(facLabel.rectTransform);

        BuildAbilityIcons();
        RaisePrefabTexts();
    }

    void Chip(string name, float x, Color accent)
    {
        var rt = UiBuild.Rect(name, _rt);
        UiBuild.Band(rt, x, ChipY, ChipW, ChipH);
        UiBuild.Fill(rt, GamePalette.WithAlpha(Color.black, 0.62f));

        var stripe = UiBuild.Rect("Accent", rt);
        UiBuild.Band(stripe, 0f, ChipH - 4f, ChipW, 4f);
        UiBuild.Fill(stripe, accent);
    }

    void BuildAbilityIcons()
    {
        var host = GetComponentInParent<CardDefinition>();
        if (host == null) return;

        var abilities = host.GetComponents<AbilityBase>();
        if (abilities == null || abilities.Length == 0) return;

        const float gap = 4f;
        int count = Mathf.Min(abilities.Length, 3);

        for (int i = 0; i < count; i++)
        {
            var iconRt = UiBuild.Rect($"Ability{i}", _rt);
            UiBuild.Band(iconRt, 4f + i * (AbilitySize + gap), AbilityY, AbilitySize, AbilitySize);
            UiBuild.Fill(iconRt, GamePalette.WithAlpha(new Color(0.85f, 0.70f, 0.35f), 0.95f));

            var label = UiBuild.Text("Glyph", iconRt, AbilityCatalog.Glyph(abilities[i]), 13f,
                                     new Color(0.05f, 0.06f, 0.09f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);
            UiBuild.Stretch(label.rectTransform);
        }

        if (abilities.Length <= count) return;

        var moreRt = UiBuild.Rect("AbilityMore", _rt);
        UiBuild.Band(moreRt, 4f + count * (AbilitySize + gap), AbilityY, AbilitySize, AbilitySize);
        UiBuild.Fill(moreRt, GamePalette.WithAlpha(GamePalette.Neutral, 0.85f));
        var more = UiBuild.Text("Glyph", moreRt, $"+{abilities.Length - count}", 12f,
                                GamePalette.TextPrimary, TextAlignmentOptions.Center, FontStyles.Bold);
        UiBuild.Stretch(more.rectTransform);
    }

    /// <summary>I Text del prefab devono disegnare sopra i fondi appena creati.</summary>
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
        if (graphic != null) graphic.transform.SetAsLastSibling();
    }
}
