using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ispettore della colonna destra: mostra la scheda completa di quello che sta
/// sotto il puntatore. Risolve il problema piu' grosso del layout precedente —
/// sedici abilita' e tutte le passive di fazione non avevano nessuna superficie
/// di visualizzazione, e una cella da 220x330 non puo' ospitarle.
/// </summary>
public class InspectorPanel : MonoBehaviour
{
    public static InspectorPanel Instance { get; private set; }

    [Header("Testate")]
    public TMP_Text titleText;
    public TMP_Text subtitleText;
    public Image sideStrip;
    public TMP_Text sideText;

    [Header("Corpo")]
    public TMP_Text bodyText;
    public TMP_Text hintText;

    readonly StringBuilder _sb = new StringBuilder(768);

    // Le ragioni dei bonus, prese da SynergyResolver: liste riusate per non
    // allocare a ogni refresh dell'ispettore (gira in polling, 8 volte al secondo).
    readonly List<SynergyResolver.Contribution> _atkReasons = new List<SynergyResolver.Contribution>(2);
    readonly List<SynergyResolver.Contribution> _blockReasons = new List<SynergyResolver.Contribution>(2);
    readonly List<SynergyResolver.Contribution> _bannerTargets = new List<SynergyResolver.Contribution>(2);

    object _source;

    /// <summary>
    /// Scheda agganciata con un clic. Serve ai nemici: non si girano e non si
    /// spostano, quindi l'unica interazione che ha senso su una casella del rullo
    /// e' tenerne aperta la scheda mentre si guarda il resto del tavolo. Finche'
    /// qualcosa e' agganciato, l'hover non cambia piu' quello che si legge.
    /// </summary>
    object _pinned;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        Clear();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary>
    /// L'hover puo' scrivere solo se non c'e' niente di agganciato. Un oggetto
    /// agganciato che viene distrutto — una casella che muore o che il rullo
    /// sostituisce — libera il pannello da solo: senza questo controllo
    /// l'ispettore resterebbe bloccato sulla scheda di un morto.
    /// </summary>
    bool Locked(object source)
    {
        DropDeadPin();
        return _pinned != null && !ReferenceEquals(_pinned, source);
    }

    /// <summary>Una casella agganciata che muore, o che il rullo sostituisce, libera il pannello.</summary>
    void DropDeadPin()
    {
        if (_pinned is Object unityObject && unityObject == null) _pinned = null;
    }

    // ── Carta ─────────────────────────────────────────────────────────────────

    float _refreshAt;
    void LateUpdate()
    {
        if (Time.unscaledTime < _refreshAt) return;
        _refreshAt = Time.unscaledTime + 0.12f;
        DropDeadPin();
        if (_source is Object obj && obj == null) { Clear(); return; }
        if (_source is CardView card) ShowCard(card);
        else if (_source is SlotView slot) ShowSlot(slot);
    }

    public void ShowCard(CardView view)
    {
        if (view == null || view.instance == null || Locked(view)) return;
        _source = view;

        var inst = view.instance;
        var def = inst.def;
        bool front = inst.side == Side.Fronte;

        SetHeader(def.cardName, $"{def.cardClass}  ·  Fazione {def.faction}", inst.side);

        var gm = GameManager.Instance;
        int lane = gm != null ? gm.GetLaneIndexFor(inst) : -1;
        bool resonant = gm != null && SynergyResolver.Resonates(gm, lane);

        _sb.Clear();

        // Le tre righe che descrivono la carta in se'. Tutto il resto — attacco,
        // guardia, chi le da' cosa — dipende da dove sta, e si legge nel conto
        // della corsia qui sotto.
        Stat("HP", $"{inst.health} / {def.maxHealth}");
        Stat("Cariche", inst.flipCharge > 0
            ? $"{inst.flipCharge} / {CardInstance.MaxFlipCharge}  <color=#ff2fd0>+{inst.flipCharge} al colpo, gia' nel totale</color>"
            : $"0 / {CardInstance.MaxFlipCharge}  <color=#8888aa>(una per turno stando coperta)</color>");
        Stat("Instabilita'", FlipRisk(def));

        if (inst.incomingDamageOverride.HasValue)
            Stat("Parata", $"danno in arrivo forzato a {inst.incomingDamageOverride.Value}");

        AppendLaneAccount(gm, inst, lane, front, resonant);
        AppendActiveBonuses(inst.AtkBonuses, inst.BlockBonuses);
        AppendBanner(def);
        AppendBannerTargets(gm, inst, lane);
        AppendAbilities(view.GetComponentInParent<CardDefinition>()?.gameObject);

        bodyText.text = _sb.ToString();
        SetHint($"Doppio clic: flip {gm?.flipCardCost ?? 1} AP / trascina: scambio {gm?.swapCardCost ?? 1} AP");
    }

    /// <summary>
    /// Il conto di questa corsia, riga per riga, con la causa di ogni modifica.
    ///
    /// E' la parte piu' importante del pannello. Le statistiche di una carta non
    /// vogliono dire niente da sole: l'attacco dipende da chi le sta accanto,
    /// la guardia dipende da chi ha davanti, e tutte due cambiano a ogni giro
    /// perche' il rullo cambia le caselle e il caos rimescola la fila. Un
    /// "3 <color=#5ad98c>+1</color>" dice al giocatore che qualcosa gli sta
    /// dando un bonus, ma non che cosa, quindi non gli dice come averne due —
    /// ed e' esattamente la mossa che deve imparare a fare.
    ///
    /// Quindi ogni riga ha un numero e la sua ragione, in ordine di
    /// applicazione, e finisce con la conseguenza: chi sfonda, chi passa, chi
    /// regge. I numeri vengono dagli stessi metodi che risolvono il colpo
    /// (<see cref="SynergyResolver"/>), non da un conto rifatto qui.
    /// </summary>
    void AppendLaneAccount(GameManager gm, CardInstance card, int lane, bool front, bool resonant)
    {
        if (gm == null || lane < 0)
        {
            Section("Fuori dal campo");
            Line("<color=#66667a>Il conto si legge quando la carta e' in una corsia.</color>");
            return;
        }

        var slot = gm.GetEnemySlotAtLane(lane);
        Section($"Il conto della corsia {lane + 1}");

        if (resonant)
            Line($"<color=#ff2b3c><b>RISONANZA</b></color> stessa fazione {card.def.faction} in corsia: " +
                 "<b>nessuno dei due para</b>.");

        // ── Quello che fai tu ────────────────────────────────────────────────
        if (!front)
        {
            Line("<b>Coperta</b>: questo giro non attacchi, pari e accumuli una carica.");
        }
        else if (slot == null)
        {
            int insegnaOpen = SynergyResolver.AttackBonus(gm, lane, _atkReasons);
            Plus(card.def.frontDamage, "attacco base", GreyHex);
            if (card.flipCharge > 0) Plus(card.flipCharge, "cariche accumulate", ChargeHex);
            foreach (var r in _atkReasons) Plus(r.amount, r.ToString(), FactionHex(card.def.faction));
            Total(card.def.frontDamage + card.flipCharge + insegnaOpen, "al boss: la corazza qui e' scoperta", GoodHex);
        }
        else
        {
            int insegna = SynergyResolver.AttackBonus(gm, lane, _atkReasons);
            int guard = SynergyResolver.EffectiveSlotBlock(gm, lane);
            int atk = card.def.frontDamage + card.flipCharge + insegna;
            int net = Mathf.Max(0, atk - guard);
            string slotName = SlotName(slot);

            Line($"<color=#8b93a3>Colpisci {slotName}</color>");
            Plus(card.def.frontDamage, "attacco base", GreyHex);
            if (card.flipCharge > 0) Plus(card.flipCharge, "cariche accumulate", ChargeHex);
            foreach (var r in _atkReasons) Plus(r.amount, r.ToString(), FactionHex(card.def.faction));
            Minus(guard, resonant
                ? $"guardia di {slotName}: <b>azzerata dalla risonanza</b>"
                : $"guardia di {slotName}", resonant ? DangerHex : RetroHex);
            Total(net, "colpo netto", net > 0 ? GoodHex : GreyHex);

            if (net > slot.health)
                Line($"   <color=#3dff7a><b>SFONDA</b></color>: le restano {slot.health}, " +
                     $"i {net - slot.health} in eccesso <b>li paga il boss</b>.");
            else if (net == slot.health)
                Line($"   La rompe esatta: fuori dal rullo, ma <b>il boss non paga niente</b>. " +
                     $"Un punto in piu' e ci arrivi.");
            else if (net > 0)
                Line($"   Non la rompe: le resterebbero {slot.health - net} di {slot.def.maxHealth}. " +
                     $"Per sfondarla adesso servirebbero <b>{slot.health + guard + 1}</b> di attacco, " +
                     $"cioe' <b>{slot.health + guard + 1 - atk}</b> in piu' di quelli che hai.");
            else
                Line("   <color=#ff2b3c>Non passa la guardia</color>: nessun danno.");
        }

        // ── Quello che ti arriva ─────────────────────────────────────────────
        if (slot == null) return;

        if (slot.side != Side.Fronte)
        {
            Line($"<color=#8b93a3>{SlotName(slot)} e' trattenuta: questo giro non colpisce.</color>");
            return;
        }

        int guardBase = front ? card.def.frontBlockValue : card.def.backBlockValue;
        int shields = SynergyResolver.BlockBonus(gm, lane, _blockReasons);
        int mine = SynergyResolver.EffectiveCardBlock(gm, lane);
        int incoming = slot.def.atkDamage + slot.tempAtkBonus;
        int arrives = Mathf.Max(0, incoming - mine);

        Line($"<color=#8b93a3>Ti risponde {SlotName(slot)}</color>");
        Plus(incoming, $"attacco di {SlotName(slot)}", DangerHex);

        // In risonanza la guardia non si sottrae affatto: mostrarne i pezzi e poi
        // rimetterli indietro renderebbe il conto piu' difficile, non piu' chiaro.
        if (resonant)
        {
            Minus(0, $"la tua guardia ({guardBase + shields}): " +
                     "<b>azzerata dalla risonanza</b>", DangerHex);
        }
        else
        {
            Minus(guardBase, front ? "la tua guardia in Fronte" : "la tua guardia da coperta", RetroHex);
            foreach (var r in _blockReasons) Minus(r.amount, r.ToString(), FactionHex(card.def.faction));
        }
        Total(arrives, "in arrivo", arrives > 0 ? DangerHex : GreyHex);

        if (arrives > card.health)
            Line($"   <color=#ff2b3c><b>PASSA</b></color>: hai {card.health} HP, " +
                 $"i {arrives - card.health} in eccesso <b>li paghi tu</b>.");
        else if (arrives == card.health)
            Line($"   Hai {card.health} HP: <color=#ff2b3c>la carta cade</color>, ma non passa niente.");
        else if (arrives > 0)
            Line($"   Hai {card.health} HP: regge, te ne restano {card.health - arrives}.");
        else
            Line("   <color=#3dff7a>Parato del tutto.</color>");
    }

    /// <summary>
    /// A chi sta servendo l'insegna di questa carta, adesso. Sul retro il
    /// simbolo dice quanto da'; qui si legge <b>a chi</b>, che e' l'informazione
    /// che decide se lasciarla dov'e'. Se non serve a nessuno, dirlo e' il modo
    /// piu' diretto di suggerire lo spostamento.
    /// </summary>
    void AppendBannerTargets(GameManager gm, CardInstance card, int lane)
    {
        if (gm == null || lane < 0 || card.side != Side.Retro) return;
        if (card.def.backDamageBonusSameFaction <= 0 && card.def.backBlockBonusSameFaction <= 0) return;

        SynergyResolver.CollectBannerTargets(gm, lane, _bannerTargets);

        Section("A chi serve adesso");
        if (_bannerTargets.Count == 0)
        {
            Line($"<color=#ff2b3c>A nessuno</color>: nelle corsie accanto non c'e' " +
                 $"nessuna carta {card.def.faction} che possa usarla.");
            Line("<color=#8b93a3>Spostarla accanto a una della sua fazione la accende.</color>");
            return;
        }

        foreach (var t in _bannerTargets)
            Line($"<color=#5ad98c>+{t.amount}</color> a <b>{t.who}</b>, corsia {t.lane}");
    }

    /// <summary>
    /// I bonus attivi adesso, con la loro causa, per una carta o per una
    /// casella.
    ///
    /// E' la risposta alla domanda piu' immediata del tabellone: **sulla cella
    /// c'e' un "+2", da dove viene?** Le righe le fornisce il registro dei bonus
    /// (<see cref="BonusLedger"/>), che ogni abilita' riempie nel momento in cui
    /// somma; qui non si ricostruisce niente, si legge. Cosi una regola nuova
    /// compare nell'ispettore da sola, e una spiegazione non puo' restare
    /// indietro rispetto all'effetto.
    ///
    /// Se non c'e' nessun bonus la sezione non compare: un elenco vuoto e' una
    /// riga in piu' da scartare con l'occhio.
    /// </summary>
    void AppendActiveBonuses(BonusLedger attack, BonusLedger block)
    {
        if (attack == null || block == null) return;
        if (!attack.Any && !block.Any) return;

        Section("Da cosa vengono i bonus");

        foreach (var e in attack.Entries)
            Plus(e.amount, $"<color=#ff8a8a>attacco</color> · {e.reason}", DangerHex);

        foreach (var e in block.Entries)
            Plus(e.amount, $"<color=#38e8ff>guardia</color> · {e.reason}", RetroHex);
    }

    static string SlotName(SlotInstance slot)
        => (slot.PoolNumber > 0 ? $"#{slot.PoolNumber} " : string.Empty) + slot.def.SlotName;

    // Colori dei numeri del conto: gli stessi significati della palette.
    const string GreyHex = "8b93a3";
    const string RetroHex = "38e8ff";
    const string DangerHex = "ff2b3c";
    const string GoodHex = "3dff7a";
    const string ChargeHex = "ff2fd0";

    static string FactionHex(Faction faction)
        => ColorUtility.ToHtmlStringRGB(GamePalette.FactionColor(faction));

    /// <summary>
    /// Una riga del conto: il verso e il numero in colonna, poi la ragione.
    /// La colonna monospaziata serve a sommare con l'occhio senza leggere.
    ///
    /// Il verso lo dichiara il chiamante e non si ricava dal numero: una
    /// guardia che vale zero e' comunque una sottrazione, e stamparla "+ 0"
    /// faceva sembrare che aggiungesse qualcosa. Capita sempre in risonanza,
    /// cioe' proprio quando il conto ha piu' bisogno di essere chiaro.
    /// </summary>
    void Row(string sign, int amount, string reason, string hex)
    {
        _sb.Append("  <mspace=0.62em><color=#").Append(hex).Append('>')
           .Append(sign).Append(Mathf.Abs(amount).ToString().PadLeft(2))
           .Append("</color></mspace>  ").Append(reason).Append('\n');
    }

    void Plus(int amount, string reason, string hex) => Row("+", amount, reason, hex);
    void Minus(int amount, string reason, string hex) => Row("−", amount, reason, hex);

    void Total(int amount, string label, string hex)
    {
        // "=" e non "= ": la cifra del totale deve cadere nella stessa colonna
        // di quelle delle righe, o la somma non si controlla con l'occhio.
        _sb.Append("  <mspace=0.62em><color=#").Append(hex).Append("><b>=")
           .Append(amount.ToString().PadLeft(2))
           .Append("</b></color></mspace>  <b>").Append(label).Append("</b>\n");
    }

    /// <summary>
    /// Carta in mano: non ha ancora una CardInstance — lato e cariche nascono
    /// quando viene giocata — quindi la scheda si legge dalla Spec del prefab.
    /// </summary>
    public void ShowCardPreview(CardDefinition definition)
    {
        if (definition == null || Locked(definition)) return;
        _source = definition;

        var def = definition.BuildSpec();

        if (titleText != null) titleText.text = def.cardName;
        if (subtitleText != null) subtitleText.text = $"{def.cardClass}  ·  Fazione {def.faction}";
        if (sideStrip != null) sideStrip.color = GamePalette.WithAlpha(GamePalette.Neutral, 0.7f);
        if (sideText != null)
        {
            sideText.text = "IN MANO / ENTRA IN FRONTE";
            sideText.color = GamePalette.TextMuted;
        }

        _sb.Clear();
        Stat("HP", $"{def.maxHealth}");
        Stat("ATK Fronte", $"{def.frontDamage}");
        Stat("BLOCCO Fronte", $"{def.frontBlockValue}");
        Stat("BLOCCO Retro", $"{def.backBlockValue}");
        Stat("Instabilita'", FlipRisk(def));

        AppendBanner(def);
        AppendAbilities(definition.gameObject);

        bodyText.text = _sb.ToString();
        SetHint("Seleziona una casella libera, poi la carta · 1 AP     Oppure trascinala sulla casella.");
    }

    // ── Slot ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Scheda di una casella del rullo. Il vocabolario e' quello del rullo, non
    /// quello delle carte: un nemico non ha un fronte e un retro da girare, ha un
    /// giro **carico** (colpisce) o **trattenuto** (para e basta) e un programma
    /// che avanza da solo. Chiamarlo Fronte/Retro come le carte faceva credere
    /// che si potesse girare.
    /// </summary>
    public void ShowSlot(SlotView view)
    {
        if (view == null || view.instance == null || Locked(view)) return;
        _source = view;

        var inst = view.instance;
        var def = inst.def;
        bool armed = inst.side == Side.Fronte;

        SetHeader(def.SlotName, $"Casella del rullo  ·  Fazione {def.faction}", inst.side);
        if (sideText != null) sideText.text = armed ? "CARICA — COLPISCE" : "TRATTENUTA — NON COLPISCE";

        _sb.Clear();
        Stat("HP", $"{inst.health} / {def.maxHealth}");
        Stat("ATK", Delta(def.atkDamage, inst.tempAtkBonus));
        Stat("Guardia adesso", Delta(armed ? def.blockFront : def.blockRetro, inst.tempBlockBonus));
        Stat("Guardia", $"{def.blockFront} da carica  ·  {def.blockRetro} trattenuta");

        // Ogni "+n" stampato sulla casella ha la sua riga qui sotto. Prima il
        // numero c'era e la causa no, e su un nemico che non si puo' girare ne'
        // spostare quella era l'unica informazione che il giocatore poteva
        // usare per decidere se colpirlo adesso o al giro dopo.
        AppendActiveBonuses(inst.AtkBonuses, inst.BlockBonuses);

        // Le due regole del pool, dette dove servono: la vita resta sulla
        // casella fra un giro e l'altro, e il traboccamento e' l'unico modo di
        // toccare il boss finche' la corazza tiene.
        var gmRef = GameManager.Instance;
        int wounds = Mathf.Max(0, def.maxHealth - inst.health);
        int laneIndex = gmRef != null ? gmRef.GetLaneIndexFor(inst) : -1;
        bool resonantLane = gmRef != null && SynergyResolver.Resonates(gmRef, laneIndex);

        if (resonantLane)
        {
            Section("Risonanza");
            Line("Stessa fazione della tua carta in questa corsia: <b>nessuno dei due para</b>.");
            Line("Il tuo colpo passa la sua guardia, <b>e il suo passa la tua</b>.");
            Line("<color=#8b93a3>E' il modo piu' economico di sfondare una lastra, " +
                 "e il modo piu' rapido di perdere la carta che la copre. " +
                 "Lo scudo spezzato sta su tutte due le celle finche' dura.</color>");
        }

        Section("Cosa paga colpirla");
        if (inst.PoolNumber > 0 && gmRef != null)
            Line($"Casella <b>#{inst.PoolNumber}</b> della corazza - {gmRef.Pool.Summary()}.");
        Line(wounds > 0
            ? $"Ferite gia' incassate: <b>{wounds}</b>. Restano sulla casella: se il rullo la ripesca, torna ferita."
            : "Le ferite restano sulla casella: se il rullo la ripesca, torna come l'hai lasciata.");
        Line($"Per finirla serve <b>{inst.health + (resonantLane ? 0 : inst.ComputeSelfBlock())}</b> di attacco. " +
             "Tutto quello che eccede <b>lo paga il boss</b>.");
        Line("<color=#8b93a3>Ucciderla la toglie dal rullo per il resto della partita.</color>");

        Section("Posizioni possibili del rullo");
        if (inst.PatternLength == 0)
        {
            Line("<color=#66667a>fisso: colpisce a ogni giro</color>");
        }
        else
        {
            var line = new StringBuilder();
            for (int i = 0; i < inst.PatternLength; i++)
            {
                var side = inst.PatternSideAt(i);
                string label = side == Side.Fronte ? "COLPISCE" : "TRATTIENE";
                string hex = ColorUtility.ToHtmlStringRGB(GamePalette.SideColor(side));
                line.Append(i == inst.PatternStep
                    ? $"<b><color=#{hex}>[{label}]</color></b>  "
                    : $"<color=#{hex}>{label}</color>  ");
            }
            Line(line.ToString());
            int armedCount = 0;
            for (int i = 0; i < inst.PatternLength; i++) if (inst.PatternSideAt(i) == Side.Fronte) armedCount++;
            Line($"Attacco: {armedCount}/{inst.PatternLength} posizioni. Tra parentesi: esito attuale.");
            Line("Il prossimo giro estrae una nuova casella e una nuova posizione.");
        }

        var berserker = view.GetComponent<SlotBerserker>();
        if (berserker != null)
        {
            Section("Furia");
            Line(berserker.BurstReady
                ? "<b>BURST PRONTO</b> — il prossimo attacco vale doppio"
                : $"{berserker.FuryStacks} / {berserker.furyThreshold} simboli della stessa fazione");
        }

        AppendAbilities(view.gameObject);

        bodyText.text = _sb.ToString();
        SetHint(ReferenceEquals(_pinned, view)
            ? "Scheda agganciata · clic sulla casella per sganciarla"
            : "Non si gira e non si sposta: clic per agganciare la scheda.\n" +
              "Il rullo gira a fine turno e sostituisce tutto il fronte.");
    }

    /// <summary>
    /// Aggancia o sgancia la scheda di una casella. E' l'unica azione che il
    /// giocatore ha su un nemico, ed e' apposta: sul rullo non si interviene, lo
    /// si legge.
    /// </summary>
    public void TogglePinSlot(SlotView view)
    {
        if (view == null || view.instance == null) return;

        bool wasPinned = ReferenceEquals(_pinned, view);

        // Si sgancia sempre prima di ridisegnare: ShowSlot rifiuta di scrivere
        // se qualcosa e' agganciato, compresa la casella su cui si e' cliccato.
        _pinned = null;
        ShowSlot(view);

        if (wasPinned) return;

        _pinned = view;
        SetHint("Scheda agganciata · clic sulla casella per sganciarla");
    }

    // ── Chiusura ──────────────────────────────────────────────────────────────

    public void HideFor(object source)
    {
        // Una scheda agganciata non la chiude l'uscita del puntatore: e' il
        // motivo per cui e' agganciata.
        DropDeadPin();
        if (_pinned != null) return;
        if (source != null && !ReferenceEquals(source, _source)) return;
        Clear();
    }

    public void Clear()
    {
        _source = null;
        _pinned = null;
        if (titleText != null) titleText.text = "ISPETTORE";
        if (subtitleText != null) subtitleText.text = "passa il puntatore su una carta o su uno slot";
        if (sideStrip != null) sideStrip.color = GamePalette.WithAlpha(GamePalette.Neutral, 0.35f);
        if (sideText != null) sideText.text = string.Empty;
        if (bodyText != null) bodyText.text = string.Empty;
        if (hintText != null) hintText.text = string.Empty;
    }

    // ── Helper di composizione ────────────────────────────────────────────────

    void SetHeader(string title, string subtitle, Side side)
    {
        if (titleText != null) titleText.text = title;
        if (subtitleText != null) subtitleText.text = subtitle;
        if (sideStrip != null) sideStrip.color = GamePalette.SideColor(side);
        if (sideText != null)
        {
            sideText.text = side.ToString().ToUpperInvariant();
            sideText.color = GamePalette.SideColor(side);
        }
    }

    void SetHint(string text) { if (hintText != null) hintText.text = text; }

    void Stat(string label, string value)
        => _sb.Append("<color=#8b93a3>").Append(label).Append("</color>  <b>").Append(value).Append("</b>\n");

    void Section(string label)
        => _sb.Append('\n').Append("<color=#5c6478>── ").Append(label).Append(" ──</color>\n");

    void Line(string text) => _sb.Append(text).Append('\n');

    static string Delta(int baseValue, int bonus)
        => bonus > 0 ? $"{baseValue} <color=#5ad98c>+{bonus}</color>" : baseValue.ToString();

    /// <summary>
    /// Quanto e' probabile che il fine turno la giri da sola. Non e' una
    /// percentuale esatta perche' non lo e' nemmeno la regola: il caos sceglie
    /// fra le candidate e ne gira al piu' <c>ChaosFlips</c>. Quello che serve al
    /// giocatore e' il confronto - questa carta sta piu' ferma di quell'altra -
    /// e quante ne cadranno stanotte.
    /// </summary>
    static string FlipRisk(CardDefinition.Spec def)
    {
        int max = GameManager.Instance != null ? GameManager.Instance.ChaosFlips : 0;
        float chance = Mathf.Clamp01(def.endTurnFlipChance);
        string grade = chance >= 0.5f  ? "<color=#ff2b3c>alta</color>"
                     : chance >= 0.35f ? "<color=#ffb000>media</color>"
                     :                   "<color=#3dff7a>bassa</color>";
        return $"{grade} ({Mathf.RoundToInt(chance * 100f)}%) · il fine turno gira fino a {max} carte";
    }

    /// <summary>
    /// L'insegna della carta per esteso. E' meta' della sinergia del gioco: sul
    /// retro della cella e' una spada e uno scudo col numero, qui e' la stessa
    /// cosa detta a parole, per chi vuole controllare di aver letto bene.
    /// </summary>
    void AppendBanner(CardDefinition.Spec def)
    {
        Section("Insegna - vale solo da coperta");
        bool any = false;
        string hex = ColorUtility.ToHtmlStringRGB(GamePalette.FactionColor(def.faction));

        if (def.backDamageBonusSameFaction > 0)
        {
            Line($"<color=#{hex}><b>SPADA +{def.backDamageBonusSameFaction}</b></color>  attacco alle carte <b>{def.faction}</b> nelle corsie accanto");
            any = true;
        }
        if (def.backBlockBonusSameFaction > 0)
        {
            Line($"<color=#{hex}><b>SCUDO +{def.backBlockBonusSameFaction}</b></color>  guardia alle carte <b>{def.faction}</b> nelle corsie accanto");
            any = true;
        }
        if (def.backBonusPAIfTwoRetroSameFaction > 0)
        {
            Line($"+{def.backBonusPAIfTwoRetroSameFaction} AP con due {def.faction} coperte, una volta per turno");
            any = true;
        }
        if (!any) Line("<color=#66667a>nessuna: da coperta e' soltanto un muro</color>");
    }

    /// <summary>Le abilita' sono componenti sul prefab: il nome del tipo e' l'unica etichetta che hanno.</summary>
    void AppendAbilities(GameObject host)
    {
        if (host == null) return;
        var abilities = host.GetComponents<AbilityBase>();
        if (abilities == null || abilities.Length == 0) return;

        Section("Abilita'");
        foreach (var ability in abilities)
            // U+2666: il rombo U+25C6 non esiste in LiberationSans ne' nei suoi fallback
            Line($"<color=#d9b25a>♦</color> {AbilityCatalog.Describe(ability)}");
    }
}
