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
/// | template | fronte di fazione | fronte di fazione | **copertina piana + bordo di fazione** |
/// | plancia alta | nome | nome | **insegna: spada/scudo col numero** |
/// | finestra | ritratto | ritratto | **sigillo grande, come nel mazzo** |
/// | pozzetti bassi | ATK e HP | ATK e HP | **spenti** |
/// | numeri | — | nei pozzetti | **due indici negli angoli bassi** |
/// | cariche | nascoste | tre pozzetti sotto le statistiche | **gli stessi tre, nello stesso posto** |
/// | fascia bassa | icona + nome abilita' | icona + nome | **niente** |
///
/// **Il retro e' un dorso, non il fronte con altri numeri.** Deve leggersi come
/// le carte impilate nel mazzo: <c>card_back_plain</c> del kit, il sigillo
/// grande al centro, le squadre agli angoli, e nessun pozzetto. Con i numeri
/// nelle stesse due caselle del fronte una carta coperta sembrava una carta
/// scoperta a cui mancava qualcosa, e il conto delle facce sul tavolo — quante
/// attaccano, quante parano — richiedeva di leggere invece di guardare.
///
/// **Girata, la carta non dice piu' chi e'.** Il nome sparisce, l'attacco
/// sparisce, il nome dell'abilita' sparisce: resta l'icona, il colore della
/// fazione e cosa fa da coperta. Non e' una semplificazione grafica, e' la
/// regola del gioco — con sei carte a terra devi ricordarti tu che cosa hai
/// coperto, e sbagliarsi e' una mossa persa. Quello che il retro mostra e'
/// soltanto cio' che serve a decidere <em>adesso</em>: quanto para, quanta vita
/// ha, e che numero passa alle vicine della sua fazione.
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

    public const float StripY = 290f, StripH = 34f;
    public const float StripIconX = 16f, StripIconY = 294f, StripIconSize = 26f;

    // ── Anatomia del RETRO ────────────────────────────────────────────────────
    //
    // Non condivide niente col fronte tranne la plancia alta, dove il nome
    // lascia il posto all'insegna. I due numeri che servono da coperta stanno
    // negli angoli bassi come gli indici di una carta da gioco — guardia a
    // sinistra, vita a destra — e le cariche diventano una colonnina sul bordo,
    // cosi il centro della cella resta il sigillo e nient'altro.
    // Le legge anche il builder, che ci posiziona i Text del gruppo BackFace.

    public const float BackIndexY = 286f, BackIndexH = 34f, BackIndexW = 90f;
    public const float BackIndexInset = 12f;
    public const float BackIndexGlyph = 22f;
    public const float BackIndexGlyphGap = 26f;
    public const float BackIndexTextW = BackIndexW - BackIndexGlyphGap;

    public static float BackIndexX(int index)
        => index == 0 ? BackIndexInset : CardW - BackIndexInset - BackIndexW;
    public static float BackIndexTextX(int index) => BackIndexX(index) + BackIndexGlyphGap;

    // Le tre tacche delle cariche, nei pozzetti che il kit disegna: sono i
    // `flip_cells` di `layouts.card` (x 6/40/74, y 131, 32x11) portati a 2x.
    //
    // **Non si spostano girando la carta**, ed e' il punto: prima il fronte le
    // mostrava qui e il dorso come una colonnina sul bordo, quindi la stessa
    // informazione cambiava posto e forma a ogni flip e andava ritrovata ogni
    // volta. Ora sono un elemento solo, condiviso: la carica appartiene alla
    // carta, non a una delle sue facce. Sul fronte cadono dentro i pozzetti
    // stampati nel template, sul dorso stanno alla stessa quota sulla
    // copertina.
    public const float ChargeY = 262f, ChargeH = 22f, ChargeW = 64f, ChargeGap = 4f;

    // L'insegna divide la plancia alta con il tag di fazione, che resta al suo
    // posto anche da coperta: la fazione e' la chiave della regola.
    public const float BannerRowW = TagX - NameX - 4f;
    public const float BannerChipGap = 4f;
    public const float BannerChipW = (BannerRowW - BannerChipGap) * 0.5f;
    public const float BannerGlyph = 22f;

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
    RectTransform _chargeColumn;
    Image _resonanceMark;    // scudo spezzato: questa corsia risuona
    readonly List<Image> _chargeCells = new List<Image>(CardInstance.MaxFlipCharge);

    // L'insegna: le chip che compaiono sulla plancia alta quando la carta e'
    // coperta, al posto del nome.
    RectTransform _bannerRow;
    TextMeshProUGUI _abilityLabel;

    // Roba del solo FRONTE: si spegne girando la carta.
    readonly List<GameObject> _frontOnly = new List<GameObject>(6);

    // Roba del solo RETRO.
    readonly List<GameObject> _backOnly = new List<GameObject>(4);

    // Tutto il chrome sta in due contenitori, uno dietro il Template e uno
    // davanti. Vedi Build: e' cio' che rende la costruzione ripetibile.
    RectTransform _under;
    RectTransform _over;

    const string UnderName = "_ChromeUnder";
    const string OverName = "_ChromeOver";

    bool _skinned;
    int _lastFace = int.MinValue;
    int _lastCharge = -1;
    int _lastResonance = -1;
    int _lastBanner = -1;
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

        // Ne' la risonanza ne' l'insegna sono proprieta' della carta: nascono
        // da chi le sta davanti e accanto, e quelle cambiano per il rullo, per
        // il caos di fine turno e per ogni spostamento. Vanno quindi rilette,
        // non attese: nessun evento della carta scatta quando cambia la vicina.
        var gm = GameManager.Instance;
        int lane = gm != null ? gm.GetLaneIndexFor(inst) : -1;

        int banner = gm != null && lane >= 0 ? SynergyResolver.AttackBonus(gm, lane) : 0;
        if (banner != _lastBanner)
        {
            _lastBanner = banner;
            _view.RefreshStatTexts();
        }
        int resonance = gm != null && SynergyResolver.Resonates(gm, lane) ? 1 : 0;
        if (resonance != _lastResonance)
        {
            _lastResonance = resonance;
            if (_resonanceMark != null)
            {
                _resonanceMark.enabled = resonance == 1;
                _resonanceMark.color = GamePalette.FactionColor(inst.def.faction);
            }
        }
    }

    // ── Faccia ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Quello che cambia girando la carta: il badge della prima casella, il
    /// sigillo e la presenza della traccia cariche. Il template e il ritratto li
    /// cambia <see cref="CardView.ApplySideVisuals"/>, che possiede l'Image.
    /// </summary>
    /// <summary>
    /// Girando la carta non cambia il contenuto di un badge: cambia **quale
    /// insieme di elementi esiste**. Il fronte ha plance, pozzetti, fila delle
    /// cariche e striscia dell'abilita'; il retro ha l'insegna, gli indici agli
    /// angoli, la colonnina delle cariche e il bordo di fazione. Non c'e'
    /// nessun elemento che vive su tutte due le facce cambiando significato —
    /// era quello a far sembrare il retro un fronte incompleto.
    ///
    /// In mano la carta si presenta col fronte: il lato lo tira CardInstance
    /// quando entra in campo.
    /// </summary>
    void ApplyFace(Side face, bool onBoard)
    {
        bool front = face == Side.Fronte;
        bool showFront = front || !onBoard;

        foreach (var go in _frontOnly) if (go != null) go.SetActive(showFront);
        foreach (var go in _backOnly) if (go != null) go.SetActive(!showFront);

        if (_sigil != null) _sigil.enabled = !showFront;

        // Le cariche si vedono in campo su tutte due le facce: sono la stessa
        // informazione, e cambiare posto girando la carta la rendeva difficile
        // da seguire. In mano non ci sono, perche' la carta non ne ha ancora.
        if (_chargeColumn != null) _chargeColumn.gameObject.SetActive(onBoard);

        // Da coperta anche il nome dell'abilita' la tradirebbe, e la fascia
        // bassa del dorso deve restare vuota: la striscia intera si spegne.
        if (_abilityLabel != null) _abilityLabel.enabled = showFront;
    }

    /// <summary>
    /// Tre tacche: quante cariche ha accumulato la carta stando in Retro, cioe'
    /// quanto danno in piu' fara' il suo prossimo attacco in Fronte.
    /// </summary>
    /// <summary>
    /// Le tacche accese sono le cariche accumulate, cioe' il danno in piu' che
    /// fara' il prossimo attacco in Fronte. Una sola traccia per tutte due le
    /// facce: la carica appartiene alla carta e non al lato che sta mostrando.
    /// </summary>
    void ApplyCharge(int charge)
    {
        var full = UiSkin.Sprite(UiSkin.FlipCellCurrent);
        var empty = UiSkin.Sprite(UiSkin.FlipCellUnknown);

        for (int i = 0; i < _chargeCells.Count; i++)
        {
            bool on = i < charge;
            if (full != null && empty != null) _chargeCells[i].sprite = on ? full : empty;
            _chargeCells[i].color = on
                ? GamePalette.Charge
                : GamePalette.WithAlpha(GamePalette.Charge, 0.22f);
        }
    }

    // ── Costruzione ───────────────────────────────────────────────────────────

    /// <summary>
    /// Monta il chrome. Va chiamata una volta per cella, ma **deve poter girare
    /// su una cella che ne ha gia' uno**: giocare una carta clona l'oggetto in
    /// mano (<c>GameManager.PlayCardFromHand</c>), non il prefab su disco, e
    /// quell'oggetto ha il chrome della mano gia' montato. Il clone se lo porta
    /// dietro, poi la sua CardOverlay ne costruisce un secondo: due copie
    /// sovrapposte, e quella vecchia resta ferma sul lato di prima — e' cosi'
    /// che il nome dell'abilita' ricompariva sul retro.
    ///
    /// Per questo tutto il chrome sta in due contenitori con un nome noto: se ci
    /// sono si buttano, e non serve tenere aggiornato un elenco di figli.
    /// <c>_ChromeUnder</c> sta dietro il Template (il sigillo di ripiego, che la
    /// cornice del kit copre) e <c>_ChromeOver</c> davanti a tutto.
    /// </summary>
    void Build()
    {
        _built = true;
        _skinned = UiSkin.Sprite(UiSkin.BadgeHp) != null;

        DiscardChrome(UnderName);
        DiscardChrome(OverName);

        _under = UiBuild.Rect(UnderName, _rt);
        UiBuild.Stretch(_under);
        _under.SetSiblingIndex(0);

        _over = UiBuild.Rect(OverName, _rt);
        UiBuild.Stretch(_over);
        _over.SetAsLastSibling();

        var def = _definition.BuildSpec();

        // I fondi vanno creati PRIMA di rialzare i Text del prefab, altrimenti li
        // coprirebbero: i figli aggiunti dopo disegnano sopra.
        // Con la skin i pozzetti li disegna gia' il template del kit: aggiungere
        // una seconda targhetta sopra raddoppierebbe la cornice.
        if (!_skinned)
        {
            _frontOnly.Add(Plate("NameBar", NameX, NameY, NameW, NameH).gameObject);
            _frontOnly.Add(Plate("StatRow", StatX(0), StatY, StatW * 2f + StatGap, StatH).gameObject);
            _frontOnly.Add(Plate("AbilityStrip", NameX, StripY, NameW, StripH, 0.55f).gameObject);
        }

        BuildSigil();
        BuildStatBadges();
        BuildChargeColumn();
        BuildFactionTag(def);
        BuildAbilityStrip(def);
        BuildBannerRow(def);
        BuildBackChrome(def);
        BuildResonanceMark();

        RaisePrefabTexts();
    }

    /// <summary>
    /// Sgancia e distrugge un contenitore di chrome ereditato dal clone.
    ///
    /// Prima si sfila dalla gerarchia e poi si distrugge, come fa
    /// <c>DetachAndDestroy</c> per le caselle vuote: <c>Destroy</c> e' differito
    /// a fine frame, quindi senza lo sgancio il vecchio chrome resterebbe a
    /// disegnare sopra quello nuovo per un frame.
    /// </summary>
    void DiscardChrome(string name)
    {
        var stale = _rt.Find(name);
        if (stale == null) return;
        stale.SetParent(null, false);
        Destroy(stale.gameObject);
    }

    /// <summary>
    /// Il sigillo della faccia Retro, nella stessa finestra del ritratto: e' cio'
    /// che rende la faccia riconoscibile da lontano senza scriverla. Nasce spento
    /// e lo accende <see cref="ApplyFace"/>.
    /// </summary>
    void BuildSigil()
    {
        var rt = UiBuild.Rect("Sigil", _under);
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
    }

    /// <summary>
    /// I due badge delle statistiche. Il primo cambia sprite con la faccia
    /// (spada in Fronte, scudo in Retro), il secondo e' sempre la vita.
    /// </summary>
    /// <summary>
    /// I pozzetti del fronte: attacco e vita. Non cambiano piu' significato col
    /// lato — sul dorso non ci sono affatto.
    /// </summary>
    void BuildStatBadges()
    {
        _statBadge = StatBadge("StatBadge0", 0, UiSkin.BadgeAtk, GamePalette.Danger, out _statRule);
        StatBadge("StatBadge1", 1, UiSkin.BadgeHp, GamePalette.PlayerHp, out _);
    }

    Image StatBadge(string name, int index, string key, Color accent, out Image rule)
    {
        var rt = UiBuild.Rect(name, _over);
        UiBuild.Band(rt, StatX(index), StatY, StatW, StatH);

        var sprite = UiSkin.Sprite(key);
        rule = null;

        if (sprite != null)
        {
            var img = UiBuild.Fill(rt, Color.white);
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            _frontOnly.Add(rt.gameObject);
            return img;
        }

        // Ripiego: il ruolo del numero lo dichiara una riga colorata sotto, come
        // prima del kit. Rosso attacco, verde vita, ciano blocco.
        _frontOnly.Add(rt.gameObject);
        var plate = UiBuild.Fill(rt, GamePalette.WithAlpha(GamePalette.PanelSunken, PlateAlpha));

        var ruleRt = UiBuild.Rect("Rule", rt);
        UiBuild.Band(ruleRt, 0f, StatH - 3f, StatW, 3f);
        rule = UiBuild.Fill(ruleRt, accent);
        return plate;
    }

    /// <summary>
    /// Le tacche delle cariche: tre, nei pozzetti del kit, identiche sulle due
    /// facce. Il numero non c'e' apposta — sono tre, si contano a occhio, e un
    /// numero in piu' sulla cella e' un numero in piu' da leggere. Quanto
    /// valgono lo dice l'ispettore, nella riga "cariche accumulate" del conto
    /// della corsia, e sono gia' dentro il totale stampato nel pozzetto ATK.
    /// </summary>
    void BuildChargeColumn()
    {
        _chargeColumn = UiBuild.Rect("ChargeTrack", _over);
        UiBuild.Band(_chargeColumn, NameX, ChargeY, NameW, ChargeH);

        var sprite = UiSkin.Sprite(UiSkin.FlipCellUnknown);

        for (int i = 0; i < CardInstance.MaxFlipCharge; i++)
        {
            var cellRt = UiBuild.Rect($"Charge{i}", _chargeColumn);
            UiBuild.Band(cellRt, ChargeX(i) - NameX, 0f, ChargeW, ChargeH);

            var img = UiBuild.Fill(cellRt, GamePalette.WithAlpha(GamePalette.Charge, 0.22f));
            if (sprite != null) { img.sprite = sprite; img.type = Image.Type.Simple; }
            _chargeCells.Add(img);
        }

        _chargeColumn.gameObject.SetActive(false);
    }

    /// <summary>
    /// Lo scudo spezzato della risonanza, sulla carta che la subisce.
    ///
    /// La risonanza e' l'unica regola che lega una carta alla casella che ha
    /// davanti, e finora si vedeva solo sull'asse delle corsie — cioe' in un
    /// terzo posto, lontano dalle due cose che la causano. Il simbolo va invece
    /// **su tutte due**: la stessa icona sulla carta e sulla casella dice
    /// "questi due, insieme" senza una parola di spiegazione, e sparisce appena
    /// una delle due cambia.
    ///
    /// Sta sotto il tag di fazione perche' la fazione e' la sua causa: sono
    /// della stessa, e per questo non si parano.
    /// </summary>
    void BuildResonanceMark()
    {
        var rt = UiBuild.Rect("ResonanceMark", _over);
        UiBuild.Band(rt, TagX, TagY + TagSize + 2f, TagSize, TagSize);

        _resonanceMark = UiBuild.Fill(rt, GamePalette.Danger);
        _resonanceMark.sprite = GlyphSprites.BrokenShield;
        _resonanceMark.type = Image.Type.Simple;
        _resonanceMark.preserveAspect = true;
        _resonanceMark.enabled = false;
    }

    /// <summary>
    /// L'insegna, sulla plancia che da scoperta porta il nome.
    ///
    /// Una spada col numero e uno scudo col numero, tinti del colore della
    /// fazione: <em>+n di attacco</em> e <em>+n di guardia</em> alle carte della
    /// stessa fazione nelle corsie accanto. E' meta' della sinergia del gioco e
    /// deve leggersi senza contare niente — colore per sapere a chi, simbolo per
    /// sapere cosa, numero per sapere quanto.
    ///
    /// I simboli non vengono dal font ma da <see cref="GlyphSprites"/>: la spada
    /// e lo scudo unicode stanno fuori dal set base e sul font legacy escono come
    /// rettangoli vuoti (e' una trappola gia' pagata, vedi AGENTS.md).
    ///
    /// Una carta senza insegna lascia la plancia vuota, e va bene cosi': da
    /// coperta e' soltanto un muro, e la casella del BLOCCO lo dice gia'.
    /// </summary>
    void BuildBannerRow(CardDefinition.Spec def)
    {
        _bannerRow = UiBuild.Rect("BannerRow", _over);
        UiBuild.Band(_bannerRow, NameX, NameY, BannerRowW, NameH);

        var color = GamePalette.FactionColor(def.faction);
        int shown = 0;

        if (def.backDamageBonusSameFaction > 0)
            BannerChip(shown++, GlyphSprites.Sword, color, def.backDamageBonusSameFaction);

        if (def.backBlockBonusSameFaction > 0)
            BannerChip(shown++, GlyphSprites.Shield, color, def.backBlockBonusSameFaction);

        _bannerRow.gameObject.SetActive(false);
        _backOnly.Add(_bannerRow.gameObject);
    }

    void BannerChip(int index, Sprite glyph, Color color, int value)
    {
        var chip = UiBuild.Rect($"Banner{index}", _bannerRow);
        UiBuild.Band(chip, index * (BannerChipW + BannerChipGap), 0f, BannerChipW, NameH);

        // Fondo appena accennato: sotto la plancia c'e' il template del kit, e
        // un ripieno pieno spegnerebbe lo shader della carta.
        UiBuild.Fill(chip, GamePalette.WithAlpha(color, 0.14f));

        var glyphRt = UiBuild.Rect("Glyph", chip);
        UiBuild.Band(glyphRt, 4f, (NameH - BannerGlyph) * 0.5f, BannerGlyph, BannerGlyph);
        var img = UiBuild.Fill(glyphRt, color);
        img.sprite = glyph;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;

        var label = UiBuild.Text("Value", chip, $"+{value}", 20f, color,
                                 TextAlignmentOptions.Center, FontStyles.Bold);
        UiBuild.Band(label.rectTransform, BannerGlyph + 6f, 0f,
                     BannerChipW - BannerGlyph - 10f, NameH);
    }

    /// <summary>
    /// Il chrome del dorso: bordo di fazione, simbolo della guardia accanto
    /// all'indice, colonnina delle cariche.
    ///
    /// **Il bordo e' l'unica cosa che dice la fazione**, perche' il template del
    /// dorso e' la copertina piana del kit e non ha colore proprio. Il colore
    /// non si mette tingendo il Template: <c>CardView.FlashTemplate</c> legge il
    /// colore base **una volta sola** per non accumulare i flash, quindi un
    /// Template tinto solo su una faccia resterebbe tinto anche sull'altra dopo
    /// la prima reazione di combattimento. Il bordo e' quindi un'Image a parte
    /// (<c>card_rim_{fazione}</c> del kit, o quattro strisce se il kit non c'e').
    ///
    /// Serve: da coperta, la fazione e' l'informazione che decide tutto — a chi
    /// vale l'insegna e in quale corsia si risuona — e il nome non c'e' piu'.
    /// </summary>
    void BuildBackChrome(CardDefinition.Spec def)
    {
        var color = GamePalette.FactionColor(def.faction);

        // Quattro strisce e non `card_rim_{fazione}` del kit: quello sprite
        // disegna il perimetro **e la cornice della finestra del ritratto**, e
        // sul dorso il ritratto non c'e' — restava un quadrato vuoto intorno al
        // sigillo, che sembrava il riquadro dell'immagine mancante. Il
        // perimetro lo si disegna, la finestra no.
        var rimRt = UiBuild.Rect("BackRim", _over);
        UiBuild.Stretch(rimRt);
        Edge(rimRt, 0f, 0f, CardW, 3f, color);
        Edge(rimRt, 0f, CardH - 3f, CardW, 3f, color);
        Edge(rimRt, 0f, 0f, 3f, CardH, color);
        Edge(rimRt, CardW - 3f, 0f, 3f, CardH, color);
        _backOnly.Add(rimRt.gameObject);

        // Lo scudo accanto all'indice di sinistra: dice che quel numero e' la
        // guardia, con lo stesso simbolo che l'insegna usa per la guardia.
        var glyphRt = UiBuild.Rect("BackBlockGlyph", _over);
        UiBuild.Band(glyphRt, BackIndexX(0), BackIndexY + (BackIndexH - BackIndexGlyph) * 0.5f,
                     BackIndexGlyph, BackIndexGlyph);
        var glyph = UiBuild.Fill(glyphRt, GamePalette.Retro);
        glyph.sprite = GlyphSprites.Shield;
        glyph.type = Image.Type.Simple;
        glyph.preserveAspect = true;
        _backOnly.Add(glyphRt.gameObject);
    }

    void Edge(RectTransform parent, float x, float y, float w, float h, Color color)
    {
        var rt = UiBuild.Rect("Edge", parent);
        UiBuild.Band(rt, x, y, w, h);
        UiBuild.Fill(rt, GamePalette.WithAlpha(color, 0.9f));
    }

    void BuildFactionTag(CardDefinition.Spec def)
    {
        var rt = UiBuild.Rect("FactionTag", _over);
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

        var iconRt = UiBuild.Rect("AbilityIcon", _over);
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

        _frontOnly.Add(iconRt.gameObject);

        string label = ability != null
            ? AbilityCatalog.Name(ability).ToUpperInvariant()
            : def.cardClass.ToString().ToUpperInvariant();

        if (abilities != null && abilities.Length > 1) label += $" +{abilities.Length - 1}";

        _abilityLabel = UiBuild.Text("AbilityLabel", _over, label, 13f, GamePalette.TextMuted,
                                     TextAlignmentOptions.Left, FontStyles.Bold);
        UiBuild.Band(_abilityLabel.rectTransform, StripIconX + StripIconSize + 6f, StripY + 4f,
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
        var rt = UiBuild.Rect(name, _over);
        UiBuild.Band(rt, x, y, w, h);
        UiBuild.Fill(rt, GamePalette.WithAlpha(GamePalette.PanelSunken, alpha));
        return rt;
    }

    /// <summary>
    /// I Text del prefab devono disegnare sopra il chrome appena creato, e devono
    /// essere accesi: le carte in mano non passano mai da ApplySideVisuals, ed e'
    /// il motivo per cui la vita non si vedeva. Vanno rialzati **dopo**
    /// <c>_ChromeOver</c>, che a sua volta e' l'ultimo dei figli generati.
    /// </summary>
    void RaisePrefabTexts()
    {
        Raise(_view.nameText);
        Raise(_view.AttackPwrText);
        Raise(_view.hpText);

        // Il gruppo del dorso sta nel prefab e contiene i suoi due indici: va
        // rialzato tutto insieme, non un Text per volta, o gli indici finirebbero
        // sotto il bordo di fazione.
        if (_view.BackFace != null) _view.BackFace.transform.SetAsLastSibling();

        var hint = _rt.Find("HintText");
        if (hint != null) hint.SetAsLastSibling();
    }

    static void Raise(Graphic graphic)
    {
        if (graphic == null) return;
        graphic.transform.SetAsLastSibling();
    }
}
