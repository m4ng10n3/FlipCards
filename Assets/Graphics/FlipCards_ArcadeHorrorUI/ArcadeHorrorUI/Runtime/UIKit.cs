// AUTO-GENERATO da build.py - non modificare a mano.
// Griglia di authoring 1x; usare Scale=2 per un canvas 1920x1080.
using UnityEngine;

namespace FlipCards.UI
{
    public static class UIKit
    {
        public const int Scale = 2;
        public static readonly Vector2Int Board = new Vector2Int(960, 540);
        public static readonly Vector2Int Card  = new Vector2Int(112, 168);

        public static class CardAnatomy
        {
            public static readonly RectInt NamePlate = new RectInt(6, 6, 100, 17);
            public static readonly RectInt ArtWindow = new RectInt(16, 26, 80, 80);
            public static readonly RectInt StatRow = new RectInt(6, 110, 100, 18);
            public static readonly RectInt FlipStrip = new RectInt(6, 131, 100, 11);
            public static readonly RectInt StateBanner = new RectInt(6, 145, 100, 17);
            public static readonly RectInt FactionTag = new RectInt(92, 8, 13, 13);
            public static readonly RectInt CostStud = new RectInt(6, 26, 12, 12);
            public static readonly RectInt[] StatSlots = { new RectInt(6, 110, 32, 18), new RectInt(40, 110, 32, 18), new RectInt(74, 110, 32, 18) };
            public static readonly RectInt[] FlipCells = { new RectInt(6, 131, 32, 11), new RectInt(40, 131, 32, 11), new RectInt(74, 131, 32, 11) };
        }

        public static class BoardLayout
        {
            public static readonly RectInt MainArea = new RectInt(8, 8, 736, 524);
            public static readonly RectInt Sidebar = new RectInt(752, 8, 200, 524);
            public static readonly RectInt TurnPlate = new RectInt(8, 10, 201, 31);
            public static readonly RectInt PhaseBanner = new RectInt(470, 10, 275, 31);
            public static readonly RectInt BossPanel = new RectInt(8, 46, 737, 53);
            public static readonly RectInt PlayerBar = new RectInt(8, 480, 737, 39);
            public static readonly RectInt SideCounters = new RectInt(752, 10, 201, 27);
            public static readonly RectInt SideInspector = new RectInt(752, 42, 201, 259);
            public static readonly RectInt SideLog = new RectInt(752, 306, 201, 109);
            public static readonly RectInt SideButtons = new RectInt(752, 420, 201, 113);
            public static readonly RectInt[] EnemySlots = { new RectInt(168, 106, 112, 168), new RectInt(320, 106, 112, 168), new RectInt(472, 106, 112, 168) };
            public static readonly RectInt[] PlayerSlots = { new RectInt(168, 304, 112, 168), new RectInt(320, 304, 112, 168), new RectInt(472, 304, 112, 168) };
            public static readonly RectInt[] CombatReadout = { new RectInt(168, 280, 112, 20), new RectInt(320, 280, 112, 20), new RectInt(472, 280, 112, 20) };
        }

        public static class Sprites
        {
            public const string BadgeAtk = "badge_atk";
            public const string BadgeCost = "badge_cost";
            public const string BadgeDef = "badge_def";
            public const string BadgeHp = "badge_hp";
            public const string BadgeNeutral = "badge_neutral";
            public const string BannerBack = "banner_back";
            public const string BannerEnemy = "banner_enemy";
            public const string BannerFlat = "banner_flat";
            public const string BannerFront = "banner_front";
            public const string BannerNeutral = "banner_neutral";
            public const string BannerPhase = "banner_phase";
            public const string BannerWarn = "banner_warn";
            public const string BarCapBoss = "bar_cap_boss";
            public const string BarCapCharge = "bar_cap_charge";
            public const string BarCapHp = "bar_cap_hp";
            public const string BarCapShield = "bar_cap_shield";
            public const string BarFillBoss = "bar_fill_boss";
            public const string BarFillCharge = "bar_fill_charge";
            public const string BarFillHp = "bar_fill_hp";
            public const string BarFillShield = "bar_fill_shield";
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
            public const string DividerH = "divider_h";
            public const string DividerV = "divider_v";
            public const string EnemyMedallion = "enemy_medallion";
            public const string EnemyMedallionA = "enemy_medallion_A";
            public const string EnemyMedallionB = "enemy_medallion_B";
            public const string EnemyMedallionC = "enemy_medallion_C";
            public const string EnemyPanel = "enemy_panel";
            public const string EnemyPanelA = "enemy_panel_A";
            public const string EnemyPanelB = "enemy_panel_B";
            public const string EnemyPanelC = "enemy_panel_C";
            public const string EnemySlotEmpty = "enemy_slot_empty";
            public const string FlipCellBack = "flip_cell_back";
            public const string FlipCellCurrent = "flip_cell_current";
            public const string FlipCellFront = "flip_cell_front";
            public const string FlipCellUnknown = "flip_cell_unknown";
            public const string FxDamage = "fx_damage";
            public const string FxFlip = "fx_flip";
            public const string FxSelect = "fx_select";
            public const string FxTarget = "fx_target";
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
            public const string MaskArtCircle = "mask_art_circle";
            public const string MaskArtSquare = "mask_art_square";
            public const string OverlayBezel = "overlay_bezel";
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
