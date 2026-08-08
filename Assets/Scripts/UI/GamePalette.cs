using UnityEngine;

/// <summary>
/// Palette unica del layout. Ogni colore ha un solo significato: il lato di una
/// carta e quello di uno slot usano la stessa codifica, altrimenti la lettura a
/// colpo d'occhio del tavolo non funziona.
/// </summary>
public static class GamePalette
{
    public static readonly Color Background  = new Color(0.043f, 0.051f, 0.075f, 1f);
    public static readonly Color Panel       = new Color(0.094f, 0.106f, 0.141f, 1f);
    public static readonly Color PanelSunken = new Color(0.063f, 0.071f, 0.098f, 1f);
    public static readonly Color Border      = new Color(0.196f, 0.227f, 0.298f, 1f);

    public static readonly Color TextPrimary = new Color(0.918f, 0.937f, 0.961f, 1f);
    public static readonly Color TextMuted   = new Color(0.545f, 0.596f, 0.678f, 1f);

    /// Fronte = ambra (attacca), Retro = blu (blocca e carica).
    public static readonly Color Fronte = new Color(0.980f, 0.722f, 0.220f, 1f);
    public static readonly Color Retro  = new Color(0.353f, 0.616f, 0.980f, 1f);

    public static readonly Color PlayerHp = new Color(0.361f, 0.804f, 0.451f, 1f);
    public static readonly Color BossHp   = new Color(0.882f, 0.310f, 0.329f, 1f);
    public static readonly Color Ap       = new Color(0.361f, 0.820f, 0.902f, 1f);
    public static readonly Color Charge   = new Color(0.749f, 0.451f, 0.949f, 1f);

    public static readonly Color Danger  = new Color(0.902f, 0.298f, 0.322f, 1f);
    public static readonly Color Good    = new Color(0.451f, 0.851f, 0.549f, 1f);
    public static readonly Color Neutral = new Color(0.400f, 0.435f, 0.502f, 1f);

    public static Color SideColor(Side side) => side == Side.Fronte ? Fronte : Retro;

    public static Color FactionColor(Faction f) => f switch
    {
        Faction.A => new Color(0.925f, 0.463f, 0.365f, 1f),
        Faction.B => new Color(0.400f, 0.753f, 0.898f, 1f),
        _         => new Color(0.667f, 0.808f, 0.400f, 1f),
    };

    public static Color ClassColor(CardClass c) => c switch
    {
        CardClass.Assalto => new Color(0.949f, 0.427f, 0.325f, 1f),
        CardClass.Tecnico => new Color(0.941f, 0.788f, 0.353f, 1f),
        CardClass.Mistico => new Color(0.749f, 0.518f, 0.949f, 1f),
        _                 => new Color(0.400f, 0.741f, 0.941f, 1f),
    };

    public static Color WithAlpha(Color c, float a) { c.a = a; return c; }
}
