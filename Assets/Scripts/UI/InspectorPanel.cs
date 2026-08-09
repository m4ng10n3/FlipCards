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

    readonly StringBuilder _sb = new StringBuilder(512);
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

    public void ShowCard(CardView view)
    {
        if (view == null || view.instance == null || Locked(view)) return;
        _source = view;

        var inst = view.instance;
        var def = inst.def;
        bool front = inst.side == Side.Fronte;

        SetHeader(def.cardName, $"{def.cardClass}  ·  Fazione {def.faction}", inst.side);

        _sb.Clear();
        Stat("HP", $"{inst.health} / {def.maxHealth}");
        Stat("ATK Fronte", Delta(def.frontDamage, inst.tempAtkBonus + (front ? inst.flipCharge : 0)));
        Stat("BLOCK Fronte", Delta(def.frontBlockValue, front ? inst.tempBlockBonus : 0));
        Stat("BLOCK Retro", Delta(def.backBlockValue, front ? 0 : inst.tempBlockBonus));
        Stat("Cariche", $"{inst.flipCharge} / {CardInstance.MaxFlipCharge}  <color=#8888aa>(danno bonus al prossimo attacco in Fronte)</color>");
        Stat("Instabilita'", $"{Mathf.RoundToInt(def.endTurnFlipChance * 100f)}%  <color=#8888aa>(si gira da sola a fine turno)</color>");

        if (inst.incomingDamageOverride.HasValue)
            Stat("Parata", $"danno in arrivo forzato a {inst.incomingDamageOverride.Value}");

        Section("Passive in Retro");
        bool anyPassive = false;
        if (def.backDamageBonusSameFaction > 0) { Line($"+{def.backDamageBonusSameFaction} ATK alle carte {def.faction} in Fronte"); anyPassive = true; }
        if (def.backBlockBonusSameFaction > 0)  { Line($"+{def.backBlockBonusSameFaction} BLOCK alle carte {def.faction}"); anyPassive = true; }
        if (def.backBonusPAIfTwoRetroSameFaction > 0) { Line($"+{def.backBonusPAIfTwoRetroSameFaction} AP con due {def.faction} in Retro"); anyPassive = true; }
        if (!anyPassive) Line("<color=#66667a>nessuna</color>");

        AppendAbilities(view.GetComponentInParent<CardDefinition>()?.gameObject);

        bodyText.text = _sb.ToString();
        SetHint("Doppio click per girare · 1 AP     Trascina su un'altra corsia per scambiare · 1 AP");
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
            sideText.text = "IN MANO";
            sideText.color = GamePalette.TextMuted;
        }

        _sb.Clear();
        Stat("HP", $"{def.maxHealth}");
        Stat("ATK Fronte", $"{def.frontDamage}");
        Stat("BLOCK Fronte", $"{def.frontBlockValue}");
        Stat("BLOCK Retro", $"{def.backBlockValue}");
        Stat("Instabilita'", $"{Mathf.RoundToInt(def.endTurnFlipChance * 100f)}%  <color=#8888aa>(si gira da sola a fine turno)</color>");

        Section("Passive in Retro");
        bool anyPassive = false;
        if (def.backDamageBonusSameFaction > 0) { Line($"+{def.backDamageBonusSameFaction} ATK alle carte {def.faction} in Fronte"); anyPassive = true; }
        if (def.backBlockBonusSameFaction > 0)  { Line($"+{def.backBlockBonusSameFaction} BLOCK alle carte {def.faction}"); anyPassive = true; }
        if (def.backBonusPAIfTwoRetroSameFaction > 0) { Line($"+{def.backBonusPAIfTwoRetroSameFaction} AP con due {def.faction} in Retro"); anyPassive = true; }
        if (!anyPassive) Line("<color=#66667a>nessuna</color>");

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
        Stat("DEF da carica", $"{def.blockFront}");
        Stat("DEF trattenuta", $"{def.blockRetro}");
        Stat("DEF adesso", $"{inst.ComputeSelfBlock()}");

        Section("Programma del rullo");
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
            Line($"<color=#8888aa>prossimo giro: " +
                 $"{(inst.PatternSideAt(inst.PatternStep + 1) == Side.Fronte ? "colpisce" : "trattiene")}</color>");
        }

        var berserker = view.GetComponent<SlotBerserker>();
        if (berserker != null)
        {
            Section("Furia");
            Line(berserker.BurstReady
                ? "<b>BURST PRONTO</b> — il prossimo attacco vale doppio"
                : $"{berserker.FuryStacks} / {berserker.furyThreshold} stack");
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
