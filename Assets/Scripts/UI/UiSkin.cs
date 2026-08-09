using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registro nome → Sprite del kit *Arcade Horror CRT*.
///
/// Serve perche' il chrome non lo costruisce solo il builder di scena (editor,
/// che potrebbe usare AssetDatabase) ma anche <see cref="CardOverlay"/>,
/// <see cref="SlotOverlay"/> e <see cref="DeckView"/>, che si disegnano addosso
/// ai prefab **a runtime**. Un asset in Resources e' l'unico modo di dare gli
/// stessi sprite ai due mondi senza duplicare riferimenti su ogni prefab.
///
/// Lo popola <c>FlipCardsLayoutBuilder</c> a ogni rebuild leggendo la cartella
/// <c>2x/</c> del kit: le chiavi sono i nomi dei file, cioe' quelli del manifest.
///
/// **Tutto il chrome deve funzionare anche con la skin assente.** Se il kit non
/// e' importato <see cref="Get"/> torna null e i costruttori di UiBuild ripiegano
/// su tinte piatte: cambia la pelle, non il layout.
/// </summary>
public class UiSkin : ScriptableObject
{
    public const string ResourcePath = "FlipCardsUiSkin";

    [System.Serializable]
    public struct Entry
    {
        public string key;
        public Sprite sprite;
    }

    public List<Entry> entries = new List<Entry>();

    Dictionary<string, Sprite> _map;

    static UiSkin _active;
    static bool _probed;

    /// <summary>
    /// La skin in uso, o null se il kit non e' stato importato. Il probe si fa
    /// una volta sola: un Resources.Load fallito a ogni frame di LateUpdate
    /// costerebbe piu' del chrome che disegna.
    /// </summary>
    public static UiSkin Active
    {
        get
        {
            if (_probed) return _active;
            _probed = true;
            _active = Resources.Load<UiSkin>(ResourcePath);
            return _active;
        }
    }

    /// <summary>Scorciatoia: sprite del kit per chiave, null se manca.</summary>
    public static Sprite Sprite(string key)
    {
        var skin = Active;
        return skin != null ? skin.Get(key) : null;
    }

    public Sprite Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;

        if (_map == null)
        {
            _map = new Dictionary<string, Sprite>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.sprite != null && !string.IsNullOrEmpty(e.key)) _map[e.key] = e.sprite;
            }
        }

        return _map.TryGetValue(key, out var sprite) ? sprite : null;
    }

    /// <summary>Invalida la cache: la chiama il builder dopo aver riscritto l'asset.</summary>
    public void Invalidate() => _map = null;

    public static void ForgetActive()
    {
        _active = null;
        _probed = false;
    }

    // ── Chiavi usate dal layout ───────────────────────────────────────────────
    //
    // Stanno qui e non sparse come stringhe: un nome sbagliato non da' errore di
    // compilazione, da' un rettangolo colorato al posto di uno sprite, e trovarlo
    // a occhio in mezzo al tabellone e' lungo.

    public const string PanelWell = "panel_well";
    public const string PanelConsole = "panel_console";
    public const string PanelScreen = "panel_screen";
    public const string PanelDark = "panel_dark";
    public const string PanelBlood = "panel_blood";
    public const string PlateCounter = "plate_counter";
    public const string PlateTooltip = "plate_tooltip";
    public const string BannerPhase = "banner_phase";
    public const string BannerFlat = "banner_flat";
    public const string BannerEnemy = "banner_enemy";
    public const string DividerH = "divider_h";

    public const string BadgeAtk = "badge_atk";
    public const string BadgeHp = "badge_hp";
    public const string BadgeDef = "badge_def";
    public const string BadgeNeutral = "badge_neutral";
    public const string MicroAtk = "micro_atk";
    public const string MicroHp = "micro_hp";
    public const string MicroDef = "micro_def";

    public const string StatusSlot = "status_slot";
    public const string StatusSlotActive = "status_slot_active";
    public const string ApSegFull = "ap_seg_full";
    public const string ApSegEmpty = "ap_seg_empty";
    public const string ApSegSpent = "ap_seg_spent";

    public const string ReelBacking = "reel_backing";
    public const string ReelFrame = "reel_frame";
    public const string ReelGlass = "reel_glass";
    public const string ReelPayline = "reel_payline";
    public const string ReelCellLocked = "reel_cell_locked";
    public const string ReelCellEmpty = "reel_cell_empty";
    public const string ReelColBlur = "reel_col_blur";
    public const string ReelColHighlight = "reel_col_highlight";
    public const string ReelSliverTop = "reel_sliver_top";
    public const string ReelSliverBottom = "reel_sliver_bottom";
    public const string ReelPipUnknown = "reel_pip_unknown";

    public const string DeckPulse = "deck_pulse";
    public const string HandDockLow = "hand_dock_low";
    public const string HandLift = "hand_lift";

    public const string CardSlotEmpty = "card_slot_empty";
    public const string EnemySlotEmpty = "enemy_slot_empty";

    // Frecce del pronostico di corsia: il kit ne ha tre, una per esito.
    public const string ReadoutUp = "readout_up";
    public const string ReadoutDown = "readout_down";
    public const string ReadoutBlock = "readout_block";

    public static string BarFrame(string kind) => "bar_frame_" + kind;
    public static string BarFill(string kind) => "bar_fill_" + kind;
    public static string Button(string tone, string state) => $"btn_{tone}_{state}";
    public static string FactionTag(Faction f) => "tag_faction_" + f;
    public static string ReelCell(Faction f) => "reel_cell_" + f;
    public static string EnemyMedallion(Faction f) => "enemy_medallion_" + f;
    public static string FlipCell(Side side) => side == Side.Fronte ? "flip_cell_front" : "flip_cell_back";
    public static string ReelPip(Side side) => side == Side.Fronte ? "reel_pip_front" : "reel_pip_back";
    public const string FlipCellCurrent = "flip_cell_current";
    public const string FlipCellUnknown = "flip_cell_unknown";
    public const string ReelPipCurrent = "reel_pip_current";

    /// <summary>Icona 16x16 del kit: <c>Icon("sword")</c> → <c>icon_sword</c>.</summary>
    public static string Icon(string name) => "icon_" + name;

    // ── Carta ─────────────────────────────────────────────────────────────────
    //
    // La faccia della carta E' il template: non c'e' nessuna etichetta che la
    // dichiari. Fronte = cornice con la finestra del ritratto aperta, Retro =
    // cornice cieca su cui si stampa il sigillo.

    public static string CardFront(Faction f) => "card_front_" + f;
    public static string CardBack(Faction f) => "card_back_" + f;
    public static string CardRim(Faction f) => "card_rim_" + f;
    public const string CardShadow = "card_shadow";

    /// <summary>Sigillo della faccia Retro: due disegni, alternati per fazione.</summary>
    public static string Sigil(Faction f) => f == Faction.B ? "decal_sigil_b" : "decal_sigil_a";

    /// <summary>Pila del mazzo per carte residue, secondo gli scaglioni del kit.</summary>
    public static string DeckStack(int count)
    {
        if (count <= 0) return "deck_stack_0";
        if (count <= 2) return "deck_stack_1";
        if (count <= 5) return "deck_stack_2";
        if (count <= 9) return "deck_stack_3";
        if (count <= 15) return "deck_stack_4";
        return "deck_stack_5";
    }

    public static string DiscardStack(int count)
    {
        if (count <= 0) return "discard_stack_0";
        if (count <= 2) return "discard_stack_1";
        if (count <= 5) return "discard_stack_2";
        return "discard_stack_3";
    }
}
