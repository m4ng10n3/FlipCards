// AUTO-GENERATO da build.py - non modificare a mano.
// Griglia di authoring 1x; Scale=2 per un canvas 1920x1080.
using UnityEngine;

namespace FlipCards.UI
{
    public static class UIKit
    {
        public const int Scale = 2;
        public const int TabHeight = 56;
        public static readonly Vector2Int Board = new Vector2Int(960, 540);
        public static readonly Vector2Int Card  = new Vector2Int(112, 168);
        public static readonly Vector2Int Cell  = new Vector2Int(176, 144);

        public static class CardAnatomy
        {
            public static readonly RectInt NamePlate = new RectInt(6, 6, 100, 17);
            public static readonly RectInt ArtWindow = new RectInt(16, 26, 80, 80);
            public static readonly RectInt[] StatSlots = { new RectInt(6, 110, 49, 18), new RectInt(57, 110, 49, 18) };
            public static readonly RectInt[] FlipCells = { new RectInt(6, 131, 32, 11), new RectInt(40, 131, 32, 11), new RectInt(74, 131, 32, 11) };
            public static readonly RectInt AbilityStrip = new RectInt(6, 145, 100, 17);
            public static readonly RectInt AbilityIcon = new RectInt(8, 147, 13, 13);
            public static readonly RectInt FactionTag = new RectInt(92, 8, 13, 13);
            public const int TabHeight = 56;
        }

        public static class ReelCell
        {
            public static readonly RectInt NameStrip = new RectInt(4, 4, 168, 14);
            public static readonly RectInt FactionTag = new RectInt(6, 5, 13, 13);
            public static readonly RectInt[] FlipPips = { new RectInt(128, 7, 8, 8), new RectInt(141, 7, 8, 8), new RectInt(154, 7, 8, 8) };
            public static readonly RectInt SymbolWindow = new RectInt(40, 22, 96, 96);
            public static readonly RectInt[] StatCells = { new RectInt(5, 122, 54, 18), new RectInt(61, 122, 54, 18), new RectInt(117, 122, 54, 18) };
        }

        public static class BoardLayout
        {
            public static readonly RectInt RailLeft = new RectInt(6, 6, 147, 528);
            public static readonly RectInt Main = new RectInt(158, 6, 589, 528);
            public static readonly RectInt RailRight = new RectInt(752, 6, 200, 528);
            public static readonly RectInt YouPlate = new RectInt(6, 6, 147, 25);
            public static readonly RectInt BarHp = new RectInt(6, 34, 147, 23);
            public static readonly RectInt BarAp = new RectInt(6, 60, 147, 19);
            public static readonly RectInt BarShield = new RectInt(6, 82, 147, 17);
            public static readonly RectInt StatusRow = new RectInt(6, 102, 147, 24);
            public static readonly RectInt[] StatusSlots = { new RectInt(9, 102, 24, 24), new RectInt(33, 102, 24, 24), new RectInt(57, 102, 24, 24), new RectInt(81, 102, 24, 24), new RectInt(105, 102, 24, 24), new RectInt(129, 102, 24, 24) };
            public static readonly RectInt DeckSlot = new RectInt(12, 146, 128, 184);
            public static readonly RectInt DiscardSlot = new RectInt(12, 350, 128, 184);
            public static readonly RectInt TurnPlate = new RectInt(158, 6, 200, 24);
            public static readonly RectInt PhaseBanner = new RectInt(430, 6, 317, 24);
            public static readonly RectInt BossPanel = new RectInt(158, 34, 589, 28);
            public static readonly RectInt ReelHousing = new RectInt(158, 66, 589, 200);
            public static readonly RectInt[] ReelColumns = { new RectInt(166, 74, 176, 184), new RectInt(364, 74, 176, 184), new RectInt(562, 74, 176, 184) };
            public static readonly RectInt[] ReelCells = { new RectInt(166, 94, 176, 144), new RectInt(364, 94, 176, 144), new RectInt(562, 94, 176, 144) };
            public const int ReelPaylineY = 166;
            public static readonly RectInt[] CombatReadout = { new RectInt(198, 270, 112, 16), new RectInt(396, 270, 112, 16), new RectInt(594, 270, 112, 16) };
            public static readonly RectInt[] PlayerSlots = { new RectInt(198, 290, 112, 168), new RectInt(396, 290, 112, 168), new RectInt(594, 290, 112, 168) };
            public static readonly RectInt HandDockLow = new RectInt(158, 462, 589, 72);
            public static readonly RectInt HandDockRaised = new RectInt(0, 320, 960, 214);
            public static readonly RectInt[] HandTabSlots = { new RectInt(164, 470, 112, 56), new RectInt(230, 470, 112, 56), new RectInt(296, 470, 112, 56), new RectInt(362, 470, 112, 56), new RectInt(428, 470, 112, 56), new RectInt(494, 470, 112, 56), new RectInt(560, 470, 112, 56), new RectInt(626, 470, 112, 56) };
            public static readonly RectInt SideCounters = new RectInt(752, 6, 200, 26);
            public static readonly RectInt SideInspector = new RectInt(752, 36, 200, 264);
            public static readonly RectInt SideLog = new RectInt(752, 304, 200, 152);
            public static readonly RectInt SideButtons = new RectInt(752, 460, 200, 74);
            public const int ButtonGap = 10;
        }

        public static class Sprites
        {
            public const string ApSegEmpty = "ap_seg_empty";
            public const string ApSegFull = "ap_seg_full";
            public const string ApSegSpent = "ap_seg_spent";
            public const string BadgeAtk = "badge_atk";
            public const string BadgeCost = "badge_cost";
            public const string BadgeDef = "badge_def";
            public const string BadgeHp = "badge_hp";
            public const string BadgeNeutral = "badge_neutral";
            public const string BannerEnemy = "banner_enemy";
            public const string BannerFlat = "banner_flat";
            public const string BannerNeutral = "banner_neutral";
            public const string BannerPhase = "banner_phase";
            public const string BannerWarn = "banner_warn";
            public const string BarCapAp = "bar_cap_ap";
            public const string BarCapBoss = "bar_cap_boss";
            public const string BarCapCharge = "bar_cap_charge";
            public const string BarCapHp = "bar_cap_hp";
            public const string BarCapShield = "bar_cap_shield";
            public const string BarFillAp = "bar_fill_ap";
            public const string BarFillBoss = "bar_fill_boss";
            public const string BarFillCharge = "bar_fill_charge";
            public const string BarFillHp = "bar_fill_hp";
            public const string BarFillShield = "bar_fill_shield";
            public const string BarFrameAp = "bar_frame_ap";
            public const string BarFrameBoss = "bar_frame_boss";
            public const string BarFrameCharge = "bar_frame_charge";
            public const string BarFrameHp = "bar_frame_hp";
            public const string BarFrameShield = "bar_frame_shield";
            public const string BarTick = "bar_tick";
            public const string BoardBg = "board_bg";
            public const string BtnAmberDisabled = "btn_amber_disabled";
            public const string BtnAmberHover = "btn_amber_hover";
            public const string BtnAmberIdle = "btn_amber_idle";
            public const string BtnAmberPress = "btn_amber_press";
            public const string BtnBloodDisabled = "btn_blood_disabled";
            public const string BtnBloodHover = "btn_blood_hover";
            public const string BtnBloodIdle = "btn_blood_idle";
            public const string BtnBloodPress = "btn_blood_press";
            public const string BtnPhosDisabled = "btn_phos_disabled";
            public const string BtnPhosHover = "btn_phos_hover";
            public const string BtnPhosIdle = "btn_phos_idle";
            public const string BtnPhosPress = "btn_phos_press";
            public const string BtnSteelDisabled = "btn_steel_disabled";
            public const string BtnSteelHover = "btn_steel_hover";
            public const string BtnSteelIdle = "btn_steel_idle";
            public const string BtnSteelPress = "btn_steel_press";
            public const string CardBack = "card_back";
            public const string CardBackA = "card_back_A";
            public const string CardBackB = "card_back_B";
            public const string CardBackC = "card_back_C";
            public const string CardBackPlain = "card_back_plain";
            public const string CardFront = "card_front";
            public const string CardFrontA = "card_front_A";
            public const string CardFrontB = "card_front_B";
            public const string CardFrontC = "card_front_C";
            public const string CardFrontNeutral = "card_front_neutral";
            public const string CardRimA = "card_rim_A";
            public const string CardRimB = "card_rim_B";
            public const string CardRimC = "card_rim_C";
            public const string CardShadow = "card_shadow";
            public const string CardSlotEmpty = "card_slot_empty";
            public const string DecalCrackA = "decal_crack_a";
            public const string DecalCrackB = "decal_crack_b";
            public const string DecalDripA = "decal_drip_a";
            public const string DecalDripB = "decal_drip_b";
            public const string DecalDripC = "decal_drip_c";
            public const string DecalScratch = "decal_scratch";
            public const string DecalSigilA = "decal_sigil_a";
            public const string DecalSigilB = "decal_sigil_b";
            public const string DecalStainA = "decal_stain_a";
            public const string DecalStainB = "decal_stain_b";
            public const string DeckPulse = "deck_pulse";
            public const string DeckStack0 = "deck_stack_0";
            public const string DeckStack1 = "deck_stack_1";
            public const string DeckStack2 = "deck_stack_2";
            public const string DeckStack3 = "deck_stack_3";
            public const string DeckStack4 = "deck_stack_4";
            public const string DeckStack5 = "deck_stack_5";
            public const string DiscardStack0 = "discard_stack_0";
            public const string DiscardStack1 = "discard_stack_1";
            public const string DiscardStack2 = "discard_stack_2";
            public const string DiscardStack3 = "discard_stack_3";
            public const string DividerH = "divider_h";
            public const string DividerV = "divider_v";
            public const string EnemyMedallion = "enemy_medallion";
            public const string EnemyMedallionA = "enemy_medallion_A";
            public const string EnemyMedallionB = "enemy_medallion_B";
            public const string EnemyMedallionC = "enemy_medallion_C";
            public const string FlipCellBack = "flip_cell_back";
            public const string FlipCellCurrent = "flip_cell_current";
            public const string FlipCellFront = "flip_cell_front";
            public const string FlipCellUnknown = "flip_cell_unknown";
            public const string FxDamage = "fx_damage";
            public const string FxFlip = "fx_flip";
            public const string FxSelect = "fx_select";
            public const string FxTarget = "fx_target";
            public const string HandDockLow = "hand_dock_low";
            public const string HandDockRaised = "hand_dock_raised";
            public const string HandLift = "hand_lift";
            public const string HandTabA = "hand_tab_A";
            public const string HandTabB = "hand_tab_B";
            public const string HandTabC = "hand_tab_C";
            public const string HandTabHover = "hand_tab_hover";
            public const string IconArrowDown = "icon_arrow_down";
            public const string IconArrowUp = "icon_arrow_up";
            public const string IconBolt = "icon_bolt";
            public const string IconClock = "icon_clock";
            public const string IconDeck = "icon_deck";
            public const string IconDiamond = "icon_diamond";
            public const string IconDrop = "icon_drop";
            public const string IconEye = "icon_eye";
            public const string IconFlip = "icon_flip";
            public const string IconHand = "icon_hand";
            public const string IconHeart = "icon_heart";
            public const string IconHeartGreen = "icon_heart_green";
            public const string IconLock = "icon_lock";
            public const string IconMinus = "icon_minus";
            public const string IconPlus = "icon_plus";
            public const string IconShield = "icon_shield";
            public const string IconShieldCyan = "icon_shield_cyan";
            public const string IconSkull = "icon_skull";
            public const string IconStar = "icon_star";
            public const string IconSwap = "icon_swap";
            public const string IconSword = "icon_sword";
            public const string IconSwordRed = "icon_sword_red";
            public const string IconTarget = "icon_target";
            public const string MaskArtSquare = "mask_art_square";
            public const string MaskSymbolCircle = "mask_symbol_circle";
            public const string MaskSymbolSquare = "mask_symbol_square";
            public const string MicroAtk = "micro_atk";
            public const string MicroCost = "micro_cost";
            public const string MicroDef = "micro_def";
            public const string MicroHp = "micro_hp";
            public const string MicroNeutral = "micro_neutral";
            public const string OverlayBezel = "overlay_bezel";
            public const string OverlayDim = "overlay_dim";
            public const string OverlayGlitch = "overlay_glitch";
            public const string OverlayScanlines = "overlay_scanlines";
            public const string OverlayStatic = "overlay_static";
            public const string OverlayVignette = "overlay_vignette";
            public const string PanelBlood = "panel_blood";
            public const string PanelConsole = "panel_console";
            public const string PanelDark = "panel_dark";
            public const string PanelMag = "panel_mag";
            public const string PanelPhos = "panel_phos";
            public const string PanelScreen = "panel_screen";
            public const string PanelWell = "panel_well";
            public const string PipApEmpty = "pip_ap_empty";
            public const string PipApFull = "pip_ap_full";
            public const string PipApSpent = "pip_ap_spent";
            public const string PlateCounter = "plate_counter";
            public const string PlateTooltip = "plate_tooltip";
            public const string ReadoutBlock = "readout_block";
            public const string ReadoutDown = "readout_down";
            public const string ReadoutUp = "readout_up";
            public const string ReelBacking = "reel_backing";
            public const string ReelCell = "reel_cell";
            public const string ReelCellA = "reel_cell_A";
            public const string ReelCellB = "reel_cell_B";
            public const string ReelCellC = "reel_cell_C";
            public const string ReelCellEmpty = "reel_cell_empty";
            public const string ReelCellLocked = "reel_cell_locked";
            public const string ReelColBlur = "reel_col_blur";
            public const string ReelColHighlight = "reel_col_highlight";
            public const string ReelFrame = "reel_frame";
            public const string ReelGlass = "reel_glass";
            public const string ReelPayline = "reel_payline";
            public const string ReelPipBack = "reel_pip_back";
            public const string ReelPipCurrent = "reel_pip_current";
            public const string ReelPipFront = "reel_pip_front";
            public const string ReelPipUnknown = "reel_pip_unknown";
            public const string ReelSliverBottom = "reel_sliver_bottom";
            public const string ReelSliverTop = "reel_sliver_top";
            public const string StatusSlot = "status_slot";
            public const string StatusSlotActive = "status_slot_active";
            public const string TagFactionA = "tag_faction_A";
            public const string TagFactionB = "tag_faction_B";
            public const string TagFactionC = "tag_faction_C";
            public const string TileGrid = "tile_grid";
            public const string TileHazard = "tile_hazard";
            public const string TileNoise = "tile_noise";
            public const string TileScanline = "tile_scanline";
            public const string TileStatic = "tile_static";
        }
    }
}
