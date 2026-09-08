using UnityEngine;

/// <summary>
/// Palette unica del layout. Ogni colore ha un solo significato: il lato di una
/// carta e quello di uno slot usano la stessa codifica, altrimenti la lettura a
/// colpo d'occhio del tavolo non funziona.
///
/// Direzione Bisca indie: carta avorio, petrolio, rosso mattone e muschio.
/// Gli accenti conservano i significati di gioco del kit originale.
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
    public static readonly Color Background  = Hex(0x10191C);
    public static readonly Color Panel       = Hex(0x203334);
    public static readonly Color PanelSunken = Hex(0x111E21);
    public static readonly Color Border      = Hex(0x46554D);
    public static readonly Color BorderHi    = Hex(0x829182);
    public static readonly Color Paper       = Hex(0xE3D2A3);
    public static readonly Color Ink         = Hex(0x152023);

    public static readonly Color TextPrimary = Hex(0xE8DBB3);
    public static readonly Color TextMuted   = Hex(0xB0B69C);
    public static readonly Color TextFaint   = Hex(0x8E9A8A);

    /// Fronte = ambra (attacca), Retro = ciano (blocca e carica).
    public static readonly Color Fronte = Hex(0xE9AF65);
    public static readonly Color Retro  = Hex(0x69C5BC);

    public static readonly Color PlayerHp = Hex(0xB5CF82);
    public static readonly Color BossHp   = Hex(0xED725B);
    public static readonly Color Ap       = Hex(0x5EAFA5);
    public static readonly Color Charge   = Hex(0xD896B2);

    public static readonly Color Danger  = Hex(0xED725B);
    public static readonly Color Good    = Hex(0xB5CF82);
    public static readonly Color Neutral = Hex(0x6E7A74);

    /// Alone della payline del rullo e delle cornici che marcano una corsia.
    public static readonly Color Payline = Hex(0xFFB000, 0.55f);

    public static Color SideColor(Side side) => side == Side.Fronte ? Fronte : Retro;

    public static string FactionName(Faction faction) => faction switch
    {
        Faction.A => "Braci",
        Faction.B => "Abissi",
        _ => "Rovi",
    };

    /// Fazioni come nel kit: A sangue, B ciano, C fosforo.
    public static Color FactionColor(Faction f) => f switch
    {
        Faction.A => Hex(0xED725B),
        Faction.B => Hex(0x69C5BC),
        _         => Hex(0xB5CF82),
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
