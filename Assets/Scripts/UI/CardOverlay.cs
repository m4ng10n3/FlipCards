using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Chrome della cella carta, montato sull'anatomia del kit *Arcade Horror CRT*
/// (`layouts.card` del manifest, ×2): tag di fazione, badge delle statistiche,
/// traccia delle cariche, striscia dell'abilita' e sigillo della faccia Retro.
///
/// **Sulla carta non e' scritto quale faccia sia.** Il lato lo dice il template —
/// <c>card_front_{fazione}</c> ha la finestra del ritratto aperta,
/// <c>card_back_{fazione}</c> e' cieca e ci si stampa sopra il sigillo — e lo
/// confermano i numeri, che cambiano con la faccia. Una fascia con scritto
/// FRONTE o RETRO ripeterebbe a parole quello che l'immagine gia' dice, e
/// costerebbe l'unica banda libera della cella: quella bassa, che qui e'
/// l'abilita'.
///
/// **Le informazioni sono divise fra le due facce**, non ripetute su entrambe:
/// due sole caselle statistica, e la prima cambia significato.
///
/// | | in mano | Fronte | Retro |
/// |---|---|---|---|
/// | finestra | ritratto | ritratto | sigillo |
/// | casella 1 | ATK | ATK (+ cariche) | BLOCCO |
/// | casella 2 | HP massimi | HP | HP |
/// | cariche | nascoste | visibili | visibili |
///
/// Il resto — blocco dell'altra faccia, passive, instabilita', testo completo
/// dell'abilita' — sta nell'ispettore, che e' la superficie fatta per il
/// dettaglio. Una cella da 224x336 non puo' ospitarlo e provarci significa
/// scriverlo cosi' piccolo che non lo legge nessuno.
///
/// **Il chrome resta traslucido.** Il Template monta CardShaderGraph, che
/// l'edizione POLYCHROME lega alla rotazione della carta: fondi opachi
/// spegnerebbero il riflesso che scorre al tilt. Gli sprite del kit hanno gia'
/// la loro trasparenza; i ripieghi a tinta piatta non superano alpha ~0.55.
///
/// Funziona anche sulle carte in mano, che non hanno ancora una CardInstance:
/// in quel caso legge la Spec dal CardDefinition e mostra la faccia di fronte.
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

    // Due caselle, non tre: la prima e' ATK in Fronte e BLOCCO in Retro.
    public const float StatY = 220f, StatH = 36f, StatW = 98f, StatGap = 4f;

    // Il badge del kit ha l'icona a sinistra: il numero parte dopo di lei.
    public const float StatTextInset = 26f;
    public const float StatTextW = StatW - StatTextInset - 6f;

    public const float ChargeY = 262f, ChargeH = 22f, ChargeW = 64f, ChargeGap = 4f;
    public const float StripY = 290f, StripH = 34f;
    public const float StripIconX = 16f, StripIconY = 294f, StripIconSize = 26f;

    public const float TagSize = 26f;
    public const float TagX = NameX + NameW - TagSize - 2f;   // in coda alla barra nome
    public const float TagY = NameY + 4f;

    public static float StatX(int index) => NameX + index * (StatW + StatGap);
    public static float StatTextX(int index) => StatX(index) + StatTextInset;
    public static float ChargeX(int index) => NameX + index * (ChargeW + ChargeGap);

    // Alpha dei ripieghi senza skin. Tenerli bassi non e' un vezzo: sotto c'e'
    // lo shader.
    const float PlateAlpha = 0.46f;

    CardView _view;
    CardDefinition _definition;
    RectTransform _rt;

    Image _statBadge;        // badge della prima casella: ATK o BLOCCO
    Image _statRule;         // sottolineatura della prima casella (ripiego senza skin)
    Image _sigil;            // sigillo: si accende solo sulla faccia Retro
    RectTransform _chargeTrack;
    readonly List<Image> _chargeCells = new List<Image>(CardInstance.MaxFlipCharge);

    bool _skinned;
    int _lastFace = int.MinValue;
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

        // In mano la carta non ha ancora un lato — lo tira CardInstance quando
        // entra in campo — quindi si presenta di fronte, senza cariche.
        bool onBoard = inst != null;
        var face = onBoard ? inst.side : Side.Fronte;

        int faceKey = (onBoard ? 2 : 0) + (int)face;
        if (faceKey != _lastFace)
        {
            _lastFace = faceKey;
            ApplyFace(face, onBoard);
        }

        if (!onBoard) return;

        if (inst.flipCharge != _lastCharge)
        {
            _lastCharge = inst.flipCharge;
            ApplyCharge(inst.flipCharge);
        }
    }

    // ── Faccia ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Quello che cambia girando la carta: il badge della prima casella, il
    /// sigillo e la presenza della traccia cariche. Il template e il ritratto li
    /// cambia <see cref="CardView.ApplySideVisuals"/>, che possiede l'Image.
    /// </summary>
    void ApplyFace(Side face, bool onBoard)
    {
        bool front = face == Side.Fronte;

        if (_statBadge != null)
        {
            var sprite = UiSkin.Sprite(front ? UiSkin.BadgeAtk : UiSkin.BadgeDef);
            if (sprite != null) _statBadge.sprite = sprite;
        }

        if (_statRule != null)
            _statRule.color = front ? GamePalette.Danger : GamePalette.Retro;

        if (_sigil != null) _sigil.enabled = onBoard && !front;
        if (_chargeTrack != null) _chargeTrack.gameObject.SetActive(onBoard);
    }

    /// <summary>
    /// Tre tacche: quante cariche ha accumulato la carta stando in Retro, cioe'
    /// quanto danno in piu' fara' il suo prossimo attacco in Fronte.
    /// </summary>
    void ApplyCharge(int charge)
    {
        var full = UiSkin.Sprite(UiSkin.FlipCellCurrent);
        var empty = UiSkin.Sprite(UiSkin.FlipCellUnknown);

        for (int i = 0; i < _chargeCells.Count; i++)
        {
            bool on = i < charge;
            if (full != null && empty != null)
            {
                _chargeCells[i].sprite = on ? full : empty;
                _chargeCells[i].color = on ? GamePalette.Charge : GamePalette.WithAlpha(Color.white, 0.5f);
            }
            else
            {
                _chargeCells[i].color = on
                    ? GamePalette.Charge
                    : GamePalette.WithAlpha(GamePalette.Charge, 0.22f);
            }
        }
    }

    // ── Costruzione ───────────────────────────────────────────────────────────

    void Build()
    {
        _built = true;
        _skinned = UiSkin.Sprite(UiSkin.BadgeHp) != null;

        var def = _definition.BuildSpec();

        // I fondi vanno creati PRIMA di rialzare i Text del prefab, altrimenti li
        // coprirebbero: i figli aggiunti dopo disegnano sopra.
        // Con la skin i pozzetti li disegna gia' il template del kit: aggiungere
        // una seconda targhetta sopra raddoppierebbe la cornice.
        if (!_skinned)
        {
            Plate("NameBar", NameX, NameY, NameW, NameH);
            Plate("StatRow", StatX(0), StatY, StatW * 2f + StatGap, StatH);
            Plate("AbilityStrip", NameX, StripY, NameW, StripH, 0.55f);
        }

        BuildSigil();
        BuildStatBadges();
        BuildChargeTrack();
        BuildFactionTag(def);
        BuildAbilityStrip(def);

        RaisePrefabTexts();
    }

    /// <summary>
    /// Il sigillo della faccia Retro, nella stessa finestra del ritratto: e' cio'
    /// che rende la faccia riconoscibile da lontano senza scriverla. Nasce spento
    /// e lo accende <see cref="ApplyFace"/>.
    /// </summary>
    void BuildSigil()
    {
        var rt = UiBuild.Rect("Sigil", _rt);
        UiBuild.Band(rt, ArtX, ArtY, ArtW, ArtH);

        var sprite = UiSkin.Sprite(UiSkin.Sigil(_definition.faction));
        if (sprite != null)
        {
            _sigil = UiBuild.Fill(rt, GamePalette.WithAlpha(GamePalette.FactionColor(_definition.faction), 0.85f));
            _sigil.sprite = sprite;
            _sigil.type = Image.Type.Simple;
            _sigil.preserveAspect = true;
        }
        else
        {
            // Senza kit resta un velo del colore di fazione: la faccia Retro deve
            // restare distinguibile anche a skin assente.
            _sigil = UiBuild.Fill(rt, GamePalette.WithAlpha(GamePalette.FactionColor(_definition.faction), 0.16f));
        }

        _sigil.enabled = false;

        // Sotto il Template, come il ritratto: la finestra della cornice e'
        // trasparente ed e' lei a dare il bordo alla finestra.
        rt.SetSiblingIndex(0);
    }

    /// <summary>
    /// I due badge delle statistiche. Il primo cambia sprite con la faccia
    /// (spada in Fronte, scudo in Retro), il secondo e' sempre la vita.
    /// </summary>
    void BuildStatBadges()
    {
        _statBadge = StatBadge("StatBadge0", 0, UiSkin.BadgeAtk, GamePalette.Danger, out _statRule);
        StatBadge("StatBadge1", 1, UiSkin.BadgeHp, GamePalette.PlayerHp, out _);
    }

    Image StatBadge(string name, int index, string key, Color accent, out Image rule)
    {
        var rt = UiBuild.Rect(name, _rt);
        UiBuild.Band(rt, StatX(index), StatY, StatW, StatH);

        var sprite = UiSkin.Sprite(key);
        rule = null;

        if (sprite != null)
        {
            var img = UiBuild.Fill(rt, Color.white);
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            return img;
        }

        // Ripiego: il ruolo del numero lo dichiara una riga colorata sotto, come
        // prima del kit. Rosso attacco, verde vita, ciano blocco.
        var plate = UiBuild.Fill(rt, GamePalette.WithAlpha(GamePalette.PanelSunken, PlateAlpha));

        var ruleRt = UiBuild.Rect("Rule", rt);
        UiBuild.Band(ruleRt, 0f, StatH - 3f, StatW, 3f);
        rule = UiBuild.Fill(ruleRt, accent);
        return plate;
    }

    void BuildChargeTrack()
    {
        _chargeTrack = UiBuild.Rect("ChargeTrack", _rt);
        UiBuild.Band(_chargeTrack, NameX, ChargeY, NameW, ChargeH);

        for (int i = 0; i < CardInstance.MaxFlipCharge; i++)
        {
            var cellRt = UiBuild.Rect($"Charge{i}", _chargeTrack);
            UiBuild.Band(cellRt, ChargeX(i) - NameX, 0f, ChargeW, ChargeH);

            var sprite = UiSkin.Sprite(UiSkin.FlipCellUnknown);
            var img = UiBuild.Fill(cellRt, sprite != null
                ? GamePalette.WithAlpha(Color.white, 0.5f)
                : GamePalette.WithAlpha(GamePalette.Charge, 0.22f));
            if (sprite != null) { img.sprite = sprite; img.type = Image.Type.Simple; }

            _chargeCells.Add(img);
        }

        _chargeTrack.gameObject.SetActive(false);
    }

    void BuildFactionTag(CardDefinition.Spec def)
    {
        var rt = UiBuild.Rect("FactionTag", _rt);
        UiBuild.Band(rt, TagX, TagY, TagSize, TagSize);

        var sprite = UiSkin.Sprite(UiSkin.FactionTag(def.faction));
        if (sprite != null)
        {
            var img = UiBuild.Fill(rt, Color.white);
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            return;
        }

        UiBuild.Fill(rt, GamePalette.FactionColor(def.faction));
        var label = UiBuild.Text("Label", rt, def.faction.ToString(), 15f, GamePalette.Background,
                                 TextAlignmentOptions.Center, FontStyles.Bold);
        UiBuild.Stretch(label.rectTransform);
    }

    /// <summary>
    /// La fascia bassa, che prima diceva il lato, ora dice cosa fa la carta:
    /// classe e abilita'. E' l'informazione che serve mentre si sceglie dove
    /// giocarla — la classe decide le combo di adiacenza — e non cambia girando
    /// la carta, quindi vale su tutte e due le facce.
    /// </summary>
    void BuildAbilityStrip(CardDefinition.Spec def)
    {
        var abilities = _definition.GetComponents<AbilityBase>();
        var ability = abilities != null && abilities.Length > 0 ? abilities[0] : null;

        var iconRt = UiBuild.Rect("AbilityIcon", _rt);
        UiBuild.Band(iconRt, StripIconX, StripIconY, StripIconSize, StripIconSize);

        var iconSprite = UiSkin.Sprite(ability != null
            ? AbilityCatalog.IconKey(ability)
            : ClassIcon(def.cardClass));

        if (iconSprite != null)
        {
            var img = UiBuild.Fill(iconRt, GamePalette.ClassColor(def.cardClass));
            img.sprite = iconSprite;
            img.type = Image.Type.Simple;
        }
        else
        {
            UiBuild.Fill(iconRt, GamePalette.ClassColor(def.cardClass));
        }

        string label = ability != null
            ? AbilityCatalog.Name(ability).ToUpperInvariant()
            : def.cardClass.ToString().ToUpperInvariant();

        if (abilities != null && abilities.Length > 1) label += $" +{abilities.Length - 1}";

        var text = UiBuild.Text("AbilityLabel", _rt, label, 13f, GamePalette.TextMuted,
                                TextAlignmentOptions.Left, FontStyles.Bold);
        UiBuild.Band(text.rectTransform, StripIconX + StripIconSize + 6f, StripY + 4f,
                     NameW - StripIconSize - 20f, StripH - 8f);
    }

    /// <summary>Icona di ripiego quando la carta non ha abilita': la sua classe.</summary>
    static string ClassIcon(CardClass cardClass) => cardClass switch
    {
        CardClass.Assalto => UiSkin.Icon("sword"),
        CardClass.Guardia => UiSkin.Icon("shield"),
        CardClass.Tecnico => UiSkin.Icon("bolt"),
        _                 => UiSkin.Icon("diamond"),
    };

    /// <summary>Fondo scuro traslucido: sotto passa lo shader della carta.</summary>
    RectTransform Plate(string name, float x, float y, float w, float h, float alpha = PlateAlpha)
    {
        var rt = UiBuild.Rect(name, _rt);
        UiBuild.Band(rt, x, y, w, h);
        UiBuild.Fill(rt, GamePalette.WithAlpha(GamePalette.PanelSunken, alpha));
        return rt;
    }

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
        graphic.transform.SetAsLastSibling();
    }
}
