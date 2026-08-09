using UnityEngine;

/// <summary>
/// Palette unica del layout. Ogni colore ha un solo significato: il lato di una
/// carta e quello di uno slot usano la stessa codifica, altrimenti la lettura a
/// colpo d'occhio del tavolo non funziona.
///
/// I valori vengono dalla palette del kit **Arcade Horror CRT**
/// (Assets/Graphics/FlipCards_ArcadeHorrorUI/.../README.md): fondi quasi neri,
/// accenti al neon saturi. E' quello che rende il tabellone simile al preview del
/// kit anche prima di montarne gli sprite.
/// </summary>
public static class GamePalette
{
    /// <summary>Colore da esadecimale del kit ("#RRGGBB"), alpha piena.</summary>
    static Color Hex(int rgb, float a = 1f) => new Color(
        ((rgb >> 16) & 0xFF) / 255f,
        ((rgb >> 8) & 0xFF) / 255f,
        (rgb & 0xFF) / 255f,
        a);

    // Fondi: void / void2 / ink / steel del kit.
    public static readonly Color Background  = Hex(0x040508);
    public static readonly Color Panel       = Hex(0x0F121A);
    public static readonly Color PanelSunken = Hex(0x090B10);
    public static readonly Color Border      = Hex(0x262E3C);
    public static readonly Color BorderHi    = Hex(0x3E4A5E);

    public static readonly Color TextPrimary = Hex(0xD8E4D6);   // bone
    public static readonly Color TextMuted   = Hex(0x8A988E);   // bone_dim
    public static readonly Color TextFaint   = Hex(0x4E5854);   // bone_lo

    /// Fronte = ambra (attacca), Retro = ciano (blocca e carica).
    public static readonly Color Fronte = Hex(0xFFB000);   // amber
    public static readonly Color Retro  = Hex(0x38E8FF);   // cyan

    public static readonly Color PlayerHp = Hex(0x3DFF7A);  // phos
    public static readonly Color BossHp   = Hex(0xFF2B3C);  // blood
    public static readonly Color Ap       = Hex(0x1FBEE4);  // cyan spento: non si confonde con Retro
    public static readonly Color Charge   = Hex(0xFF2FD0);  // mag

    public static readonly Color Danger  = Hex(0xFF2B3C);
    public static readonly Color Good    = Hex(0x3DFF7A);
    public static readonly Color Neutral = Hex(0x6E7A74);

    /// Alone della payline del rullo e delle cornici che marcano una corsia.
    public static readonly Color Payline = Hex(0xFFB000, 0.55f);

    public static Color SideColor(Side side) => side == Side.Fronte ? Fronte : Retro;

    /// Fazioni come nel kit: A sangue, B ciano, C fosforo.
    public static Color FactionColor(Faction f) => f switch
    {
        Faction.A => Hex(0xFF2B3C),
        Faction.B => Hex(0x38E8FF),
        _         => Hex(0x3DFF7A),
    };

    public static Color ClassColor(CardClass c) => c switch
    {
        CardClass.Assalto => Hex(0xFF8A8A),
        CardClass.Tecnico => Hex(0xFFB000),
        CardClass.Mistico => Hex(0x843ED2),
        _                 => Hex(0x38E8FF),
    };

    public static Color WithAlpha(Color c, float a) { c.a = a; return c; }
}
