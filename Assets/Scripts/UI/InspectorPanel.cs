using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ispettore sulla pagina destra del libretto: la scheda di una carta o di una
/// casella, detta con i simboli del tavolo invece che a parole.
///
/// In testa c'e' l'immagine vera (il ritratto della carta, il simbolo della
/// casella) accanto al nome. Sotto, le statistiche sono le stesse file di gocce,
/// lance e scudi stampate sulla carta, e il conto della corsia e' una riga di
/// simboli: ⟨lancia⟩ attacco + ⟨carica⟩ + ⟨insegna⟩ − ⟨scudo⟩ guardia = colpo,
/// e dove va a finire (⟨display del boss⟩ se sfonda). Le icone sono lo sprite
/// asset TMP ui_icons (12_TableProps/Tools/build_icons.py): nei testi si scrive
/// &lt;sprite name="drop"&gt;.
///
/// I numeri vengono dagli stessi metodi che risolvono il colpo
/// (<see cref="SynergyResolver"/>), non da un conto rifatto qui. Le parole
/// restano solo dove un simbolo non basta: la causa di un bonus e le abilita'.
/// </summary>
public class InspectorPanel : MonoBehaviour
{
    public static InspectorPanel Instance { get; private set; }

    [Header("Testate")]
    public TMP_Text titleText;
    public TMP_Text subtitleText;
    public Image sideStrip;
    public TMP_Text sideText;
    [Tooltip("L'immagine della carta o della casella accanto al nome.")]
    public Image portrait;

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

    // ── Simboli ───────────────────────────────────────────────────────────────

    public static string I(string name) => "<sprite name=\"" + name + "\">";

    public static string FactionIcon(Faction faction) => faction switch
    {
        Faction.A => "sun",
        Faction.B => "moon",
        _ => "saturn",
    };

    /// <summary>
    /// Una fila di simboli come sulla carta: pieni quanto il valore, vuoti fino al
    /// massimo. Oltre dieci diventa simbolo e numero, o la riga andrebbe a capo.
    /// </summary>
    static string Pips(string full, string empty, int value, int max)
    {
        value = Mathf.Max(0, value);
        max = Mathf.Max(value, max);
        if (max > 10) return I(full) + "<b>" + value + "</b>" + (max > value ? "<color=#7A8078>/" + max + "</color>" : "");
        var sb = new StringBuilder();
        for (int i = 0; i < max; i++) sb.Append(I(i < value ? full : empty));
        return sb.ToString();
    }

    const string GreyHex = "5E6A66";
    const string RetroHex = "1E6E7A";
    const string DangerHex = "A31220";
    const string GoodHex = "1F7A3A";
    const string ChargeHex = "8E2A78";

    static string C(string hex, string text) => "<color=#" + hex + ">" + text + "</color>";
    static string Big(string text) => "<size=130%>" + text + "</size>";

    void Line(string text) => _sb.Append(text).Append('\n');
    void Gap() => _sb.Append("<size=40%>\n</size>");
    void Section(string icon, string label)
        => _sb.Append("<size=45%>\n</size>").Append(I(icon)).Append(' ').Append(C("6E5A34", "<b>" + label + "</b>")).Append('\n');

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
        var definition = view.GetComponentInParent<CardDefinition>();

        SetHeader(def.cardName, $"{I(FactionIcon(def.faction))} {GamePalette.FactionName(def.faction)}", inst.side, CardPortrait(definition));

        var gm = GameManager.Instance;
        int lane = gm != null ? gm.GetLaneIndexFor(inst) : -1;
        bool resonant = gm != null && SynergyResolver.Resonates(gm, lane);

        _sb.Clear();
        // Le file della carta, come stampate: vita, attacco o difesa del lato
        // che mostra, cariche.
        Line(Big(Pips("drop", "drop_empty", inst.health, def.maxHealth)));
        Line(front
            ? Big(Pips("atk", "atk_empty", view.ForecastAttack(), def.frontDamage))
            : Big(Pips("def", "def_empty", def.backBlockValue + inst.tempBlockBonus, def.backBlockValue)));
        Line(Pips("charge", "charge_empty", inst.flipCharge, CardInstance.MaxFlipCharge) +
             (inst.flipCharge > 0 ? "  " + C(ChargeHex, $"+{inst.flipCharge} {I("atk")}") : ""));
        Line($"{I("flip")} {FlipRisk(def)}");

        if (inst.incomingDamageOverride.HasValue)
            Line($"{I("def")} {I("arrow")} {inst.incomingDamageOverride.Value}");

        AppendLaneAccount(gm, inst, lane, front, resonant);
        AppendActiveBonuses(inst.AtkBonuses, inst.BlockBonuses);
        AppendBanner(def);
        AppendBannerTargets(gm, inst, lane);
        AppendAbilities(definition != null ? definition.gameObject : null);

        bodyText.text = _sb.ToString();
        SetHint($"{I("flip")} doppio clic · {gm?.flipCardCost ?? 1}{I("star")}     {I("swap")} trascina · {gm?.swapCardCost ?? 1}{I("star")}");
    }

    /// <summary>Explicit selection from the modal, independent of hover or gameplay clicks.</summary>
    public void InspectSelection(Object source)
    {
        _pinned = null;
        if (source is CardView card) ShowCard(card);
        else if (source is CardDefinition definition) ShowCardPreview(definition);
        else if (source is SlotView slot) ShowSlot(slot);
        _pinned = source;
        if (bodyText != null)
        {
            var scroll = bodyText.GetComponentInParent<ScrollRect>();
            if (scroll != null) scroll.verticalNormalizedPosition = 1;
        }
    }

    /// <summary>
    /// Il conto della corsia in due righe di simboli: il colpo che dai e quello
    /// che ricevi, ognuno con il suo esito. Ogni addendo ha il suo simbolo — la
    /// carica, l'insegna di chi te la da', la guardia della casella — perche' un
    /// "+1" senza causa non dice al giocatore come averne due.
    /// </summary>
    void AppendLaneAccount(GameManager gm, CardInstance card, int lane, bool front, bool resonant)
    {
        if (gm == null || lane < 0) return;

        var slot = gm.GetEnemySlotAtLane(lane);
        Section("reel", $"CORSIA {lane + 1}");

        if (resonant)
            Line($"{Big(I("broken"))} {I(FactionIcon(card.def.faction))}={I(FactionIcon(card.def.faction))}  {C(DangerHex, "nessuno para")}");

        // ── Il colpo che dai ─────────────────────────────────────────────────
        if (!front)
        {
            Line($"{I("card_back")} {I("atk_empty")}  {C(GreyHex, "coperta: non colpisci")}  +1{I("charge")}");
        }
        else
        {
            int insegna = SynergyResolver.AttackBonus(gm, lane, _atkReasons);
            int atk = card.def.frontDamage + card.flipCharge + insegna;
            var row = new StringBuilder();
            row.Append(I("atk")).Append("<b>").Append(card.def.frontDamage).Append("</b>");
            if (card.flipCharge > 0) row.Append("  +").Append(card.flipCharge).Append(I("charge"));
            foreach (var r in _atkReasons) row.Append("  +").Append(r.amount).Append(I("spade"));

            if (slot == null)
            {
                row.Append("  = <b>").Append(atk).Append("</b> ").Append(I("arrow")).Append(Big(I("led_boss")));
                Line(row.ToString());
            }
            else
            {
                int guard = SynergyResolver.EffectiveSlotBlock(gm, lane);
                int net = Mathf.Max(0, atk - guard);
                row.Append("  −").Append(resonant ? $"<s>{I("def")}</s>" : $"{guard}{I("def")}");
                row.Append("  = ").Append(C(net > 0 ? GoodHex : GreyHex, "<b>" + net + "</b>"));
                row.Append(' ').Append(I("arrow")).Append(' ').Append(slot.health).Append(I("drop"));
                Line(row.ToString());

                if (net > slot.health)
                    Line($"     {C(GoodHex, "<b>sfonda</b>")}  {I("arrow")} +{net - slot.health}{Big(I("led_boss"))}");
                else if (net == slot.health)
                    Line($"     {C(GoodHex, "rotta")}  {C(GreyHex, $"+1{I("atk")} per il boss")}");
                else if (net > 0)
                    Line($"     {C(GreyHex, "regge")}  {C(GreyHex, $"servono +{slot.health + 1 - net}{I("atk")}")}");
                else
                    Line($"     {C(DangerHex, "parato")}");
            }
        }

        // ── Il colpo che ricevi ──────────────────────────────────────────────
        if (slot == null) return;
        if (slot.side != Side.Fronte)
        {
            Line($"{I("lamp_def")} {C(GreyHex, "trattenuta: non colpisce")}");
            return;
        }

        int guardBase = front ? card.def.frontBlockValue : card.def.backBlockValue;
        SynergyResolver.BlockBonus(gm, lane, _blockReasons);
        int mine = SynergyResolver.EffectiveCardBlock(gm, lane);
        int incoming = slot.def.atkDamage + slot.tempAtkBonus;
        int arrives = Mathf.Max(0, incoming - mine);

        var back = new StringBuilder();
        back.Append(I("lamp_atk")).Append("<b>").Append(incoming).Append("</b>  −");
        if (resonant) back.Append("<s>").Append(I("def")).Append("</s>");
        else
        {
            back.Append(guardBase).Append(I("def"));
            foreach (var r in _blockReasons) back.Append("  −").Append(r.amount).Append(I("club"));
        }
        back.Append("  = ").Append(C(arrives > 0 ? DangerHex : GreyHex, "<b>" + arrives + "</b>"));
        back.Append(' ').Append(I("arrow")).Append(' ').Append(card.health).Append(I("drop"));
        Line(back.ToString());

        if (arrives > card.health)
            Line($"     {C(DangerHex, "<b>passa</b>")}  {I("arrow")} −{arrives - card.health}{Big(I("led_player"))}");
        else if (arrives == card.health)
            Line($"     {C(DangerHex, "la carta cade")}");
        else if (arrives > 0)
            Line($"     {C(GreyHex, $"regge: restano {card.health - arrives}")}{I("drop")}");
        else
            Line($"     {C(GoodHex, "parato")}");
    }

    /// <summary>A chi serve adesso l'insegna di questa carta coperta.</summary>
    void AppendBannerTargets(GameManager gm, CardInstance card, int lane)
    {
        if (gm == null || lane < 0 || card.side != Side.Retro) return;
        if (card.def.backDamageBonusSameFaction <= 0 && card.def.backBlockBonusSameFaction <= 0) return;

        SynergyResolver.CollectBannerTargets(gm, lane, _bannerTargets);
        if (_bannerTargets.Count == 0)
        {
            Line($"{I("spade")} {I("arrow")} {C(DangerHex, "nessuna vicina")} {I(FactionIcon(card.def.faction))}  {I("swap")}");
            return;
        }
        foreach (var t in _bannerTargets)
            Line($"{I("spade")} {C(GoodHex, "+" + t.amount)} {I("arrow")} <b>{t.who}</b> {C(GreyHex, "corsia " + t.lane)}");
    }

    /// <summary>
    /// I bonus attivi, ognuno con la sua causa: il registro (<see cref="BonusLedger"/>)
    /// li scrive nel momento in cui somma, qui si leggono e basta.
    /// </summary>
    void AppendActiveBonuses(BonusLedger attack, BonusLedger block)
    {
        if (attack == null || block == null) return;
        if (!attack.Any && !block.Any) return;

        Gap();
        foreach (var e in attack.Entries)
            Line($"{I("atk")}{C(GoodHex, "+" + e.amount)}  {C(GreyHex, e.reason)}");
        foreach (var e in block.Entries)
            Line($"{I("def")}{C(RetroHex, "+" + e.amount)}  {C(GreyHex, e.reason)}");
    }

    /// <summary>Carta in mano: senza CardInstance, la scheda si legge dalla Spec del prefab.</summary>
    public void ShowCardPreview(CardDefinition definition)
    {
        if (definition == null || Locked(definition)) return;
        _source = definition;

        var def = definition.BuildSpec();
        if (titleText != null) titleText.text = def.cardName;
        if (subtitleText != null) subtitleText.text = $"{I(FactionIcon(def.faction))} {GamePalette.FactionName(def.faction)}";
        if (sideStrip != null) sideStrip.color = GamePalette.WithAlpha(GamePalette.Neutral, 0.7f);
        if (sideText != null) { sideText.text = $"{I("deck")} in mano"; sideText.color = GamePalette.InkMuted; }
        SetPortrait(CardPortrait(definition));

        _sb.Clear();
        Line(Big(Pips("drop", "drop_empty", def.maxHealth, def.maxHealth)));
        Line($"{I("card_front")} {Pips("atk", "atk_empty", def.frontDamage, def.frontDamage)}   {Pips("def", "def_empty", def.frontBlockValue, def.frontBlockValue)}");
        Line($"{I("card_back")} {Pips("def", "def_empty", def.backBlockValue, def.backBlockValue)}");
        Line($"{I("flip")} {FlipRisk(def)}");

        AppendBanner(def);
        AppendAbilities(definition.gameObject);

        bodyText.text = _sb.ToString();
        SetHint($"casella libera, poi la carta · {GameManager.Instance?.playCardCost ?? 1}{I("star")}");
    }

    // ── Slot ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Scheda di una casella del rullo. Un nemico non si gira: ha un giro carico
    /// (colpisce, ⟨lampada ambra⟩) o trattenuto (para e basta, ⟨tubo azzurro⟩) e
    /// un programma che avanza da solo, mostrato come fila di lampade.
    /// </summary>
    public void ShowSlot(SlotView view)
    {
        if (view == null || view.instance == null || Locked(view)) return;
        _source = view;

        var inst = view.instance;
        var def = inst.def;
        bool armed = inst.side == Side.Fronte;

        SetHeader(def.SlotName, $"{I(FactionIcon(def.faction))} {GamePalette.FactionName(def.faction)}" +
                  (inst.PoolNumber > 0 ? $"   #{inst.PoolNumber}" : ""), inst.side, SlotPortrait(view));
        if (sideText != null) sideText.text = armed ? $"{I("lamp_atk")} carica" : $"{I("lamp_def")} trattenuta";

        _sb.Clear();
        // Le stesse tre grandezze delle lampade della cassa: la lampada dice quale,
        // la fila dice quanto.
        Line(Big(I("lamp_hp") + " " + Pips("drop", "drop_empty", inst.health, def.maxHealth)));
        Line(Big(I("lamp_atk") + " " + Pips("atk", "atk_empty", def.atkDamage + inst.tempAtkBonus, def.atkDamage)));
        int guardNow = (armed ? def.blockFront : def.blockRetro) + inst.tempBlockBonus;
        Line(Big(I("lamp_def") + " " + Pips("def", "def_empty", guardNow, Mathf.Max(def.blockFront, def.blockRetro))));

        AppendActiveBonuses(inst.AtkBonuses, inst.BlockBonuses);

        var gmRef = GameManager.Instance;
        int wounds = Mathf.Max(0, def.maxHealth - inst.health);
        int laneIndex = gmRef != null ? gmRef.GetLaneIndexFor(inst) : -1;
        bool resonantLane = gmRef != null && SynergyResolver.Resonates(gmRef, laneIndex);

        if (resonantLane)
            Line($"{Big(I("broken"))} {C(DangerHex, "nessuno para, da tutte e due le parti")}");

        Section("led_boss", "COLPIRLA");
        int toKill = inst.health + (resonantLane ? 0 : inst.ComputeSelfBlock());
        Line($"{I("atk")}<b>{toKill}</b> {I("arrow")} {C(GoodHex, "rotta")}   +1{I("atk")} {I("arrow")} {Big(I("led_boss"))}");
        if (wounds > 0) Line($"{I("drop_empty")}×{wounds}  {C(GreyHex, "le ferite restano")}");
        if (gmRef != null && inst.PoolNumber > 0) Line($"{I("reel")} {C(GreyHex, gmRef.Pool.Summary())}");

        if (inst.PatternLength > 0)
        {
            Section("reel", "PROGRAMMA");
            var line = new StringBuilder();
            for (int i = 0; i < inst.PatternLength; i++)
            {
                string icon = inst.PatternSideAt(i) == Side.Fronte ? "lamp_atk" : "lamp_def";
                line.Append(i == inst.PatternStep ? "<size=150%>" + I(icon) + "</size>" : I(icon)).Append(' ');
            }
            Line(line.ToString());
        }

        var berserker = view.GetComponent<SlotBerserker>();
        if (berserker != null)
            Line(berserker.BurstReady
                ? $"{Big(I("lamp_atk"))}×2  {C(DangerHex, "furia pronta")}"
                : $"{I("lamp_atk")} {C(GreyHex, $"furia {berserker.FuryStacks}/{berserker.furyThreshold}")}");

        AppendAbilities(view.gameObject);

        bodyText.text = _sb.ToString();
        SetHint(ReferenceEquals(_pinned, view) ? "scheda agganciata · clic per sganciarla" : "clic: aggancia la scheda");
    }

    /// <summary>Aggancia o sgancia la scheda di una casella: sul rullo non si interviene, lo si legge.</summary>
    public void TogglePinSlot(SlotView view)
    {
        if (view == null || view.instance == null) return;

        bool wasPinned = ReferenceEquals(_pinned, view);
        _pinned = null;
        ShowSlot(view);
        if (wasPinned) return;

        _pinned = view;
        SetHint("scheda agganciata · clic per sganciarla");
    }

    // ── Chiusura ──────────────────────────────────────────────────────────────

    public void HideFor(object source)
    {
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
        if (subtitleText != null) subtitleText.text = $"{I("card_front")} {I("reel")}  scegli un elemento";
        if (sideStrip != null) sideStrip.color = GamePalette.WithAlpha(GamePalette.Neutral, 0.35f);
        if (sideText != null) sideText.text = string.Empty;
        if (bodyText != null) bodyText.text = string.Empty;
        if (hintText != null) hintText.text = string.Empty;
        SetPortrait(null);
    }

    // ── Helper di composizione ────────────────────────────────────────────────

    void SetHeader(string title, string subtitle, Side side, Sprite image)
    {
        if (titleText != null) titleText.text = title;
        if (subtitleText != null) subtitleText.text = subtitle;
        if (sideStrip != null) sideStrip.color = GamePalette.InkSide(side);
        if (sideText != null)
        {
            sideText.text = side == Side.Fronte ? $"{I("card_front")} fronte" : $"{I("card_back")} retro";
            sideText.color = GamePalette.InkSide(side);
        }
        SetPortrait(image);
    }

    void SetPortrait(Sprite sprite)
    {
        if (portrait == null) return;
        portrait.sprite = sprite;
        portrait.enabled = sprite != null;
    }

    static Sprite CardPortrait(CardDefinition definition)
    {
        if (definition == null) return null;
        var art = FindDeep(definition.transform, "imagecharacter");
        return art != null && art.TryGetComponent(out Image image) ? image.sprite : null;
    }

    static Sprite SlotPortrait(SlotView view)
    {
        var art = FindDeep(view.transform, "Sprite");
        return art != null && art.TryGetComponent(out Image image) ? image.sprite : null;
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            var found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    void SetHint(string text) { if (hintText != null) hintText.text = text; }

    /// <summary>Quanto e' probabile che il fine turno la giri da sola: tre gradi, non una percentuale.</summary>
    static string FlipRisk(CardDefinition.Spec def)
    {
        float chance = Mathf.Clamp01(def.endTurnFlipChance);
        return chance >= 0.5f  ? C(DangerHex, "instabile")
             : chance >= 0.35f ? C("8A6A22", "incerta")
             :                   C(GoodHex, "stabile");
    }

    /// <summary>L'insegna: vale solo da coperta, e solo per le vicine della stessa fazione.</summary>
    void AppendBanner(CardDefinition.Spec def)
    {
        string faction = I(FactionIcon(def.faction));
        bool any = false;
        var line = new StringBuilder();
        line.Append(I("card_back")).Append(' ');
        if (def.backDamageBonusSameFaction > 0) { line.Append(I("spade")).Append("<b>+").Append(def.backDamageBonusSameFaction).Append("</b>  "); any = true; }
        if (def.backBlockBonusSameFaction > 0) { line.Append(I("club")).Append("<b>+").Append(def.backBlockBonusSameFaction).Append("</b>  "); any = true; }
        if (!any) return;
        line.Append(I("arrow")).Append(" vicine ").Append(faction);
        Section("spade", "INSEGNA");
        Line(line.ToString());
        if (def.backBonusPAIfTwoRetroSameFaction > 0)
            Line($"{I("card_back")}{I("card_back")} {faction} {I("arrow")} +{def.backBonusPAIfTwoRetroSameFaction}{I("star")}");
    }

    /// <summary>Le abilita': l'unica parte che resta a parole, perche' ognuna e' una regola sua.</summary>
    void AppendAbilities(GameObject host)
    {
        if (host == null) return;
        var abilities = host.GetComponents<AbilityBase>();
        if (abilities == null || abilities.Length == 0) return;

        Section("book", "ABILITA'");
        foreach (var ability in abilities)
            Line(C(GreyHex, AbilityCatalog.Describe(ability)));
    }
}
