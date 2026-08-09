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

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        Clear();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // ── Carta ─────────────────────────────────────────────────────────────────

    public void ShowCard(CardView view)
    {
        if (view == null || view.instance == null) return;
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
        if (definition == null) return;
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

    public void ShowSlot(SlotView view)
    {
        if (view == null || view.instance == null) return;
        _source = view;

        var inst = view.instance;
        var def = inst.def;

        SetHeader(def.SlotName, $"Slot nemico  ·  Fazione {def.faction}", inst.side);

        _sb.Clear();
        Stat("HP", $"{inst.health} / {def.maxHealth}");
        Stat("ATK", Delta(def.atkDamage, inst.tempAtkBonus));
        Stat("DEF Fronte", $"{def.blockFront}");
        Stat("DEF Retro", $"{def.blockRetro}");
        Stat("DEF attuale", $"{inst.ComputeSelfBlock()}");

        Section("Pattern di flip");
        if (inst.PatternLength == 0)
        {
            Line("<color=#66667a>nessuno: sempre in Fronte</color>");
        }
        else
        {
            var line = new StringBuilder();
            for (int i = 0; i < inst.PatternLength; i++)
            {
                var side = inst.PatternSideAt(i);
                string label = side == Side.Fronte ? "F" : "R";
                string hex = ColorUtility.ToHtmlStringRGB(GamePalette.SideColor(side));
                line.Append(i == inst.PatternStep
                    ? $"<b><color=#{hex}>[{label}]</color></b> "
                    : $"<color=#{hex}>{label}</color>  ");
            }
            Line(line.ToString());
            Line($"<color=#8888aa>prossimo: {inst.PatternSideAt(inst.PatternStep + 1)}</color>");
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
        SetHint("Il fronte nemico viene sostituito a fine turno: vale solo per questo turno.");
    }

    // ── Chiusura ──────────────────────────────────────────────────────────────

    public void HideFor(object source)
    {
        if (source != null && !ReferenceEquals(source, _source)) return;
        Clear();
    }

    public void Clear()
    {
        _source = null;
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
