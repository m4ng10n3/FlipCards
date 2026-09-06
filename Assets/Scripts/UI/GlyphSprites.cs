using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// I simboli del gioco disegnati a mano, pixel per pixel, invece che presi dal
/// font.
///
/// PERCHE' NON UN GLIFO: la spada e lo scudo sono l'unica cosa che spiega la
/// sinergia al giocatore, e devono esserci sempre. I caratteri unicode che li
/// rappresentano stanno fuori dal set base e la loro presenza dipende dal font
/// di sistema: su una macchina si vedono, su un'altra compare il quadratino.
/// Un simbolo che a volte non c'e' e' peggio di nessun simbolo.
///
/// Sono pixel art 12x12 perche' il resto della grafica e' arcade: ingranditi
/// restano nitidi (filtro Point) e si colorano con la tinta della fazione, che
/// e' il secondo mezzo con cui il giocatore legge la sinergia.
/// </summary>
public static class GlyphSprites
{
    // '#' pieno, '.' vuoto. Prima riga = alto.
    static readonly string[] SwordArt =
    {
        ".....##.....",
        ".....##.....",
        ".....##.....",
        ".....##.....",
        ".....##.....",
        ".....##.....",
        "...######...",
        ".##########.",
        "...######...",
        ".....##.....",
        "....####....",
        ".....##.....",
    };

    static readonly string[] ShieldArt =
    {
        "############",
        "############",
        "############",
        ".##########.",
        ".##########.",
        ".##########.",
        "..########..",
        "..########..",
        "...######...",
        "....####....",
        ".....##.....",
        "............",
    };

    /// <summary>Scudo spezzato: la corsia in risonanza, dove il blocco non tiene.</summary>
    static readonly string[] BrokenShieldArt =
    {
        "#####..#####",
        "####....####",
        "###......###",
        ".##......##.",
        ".#.......##.",
        ".##.....###.",
        "..##...####.",
        "..###...##..",
        "...###.##...",
        "....##.##...",
        ".....###....",
        "............",
    };

    static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

    /// <summary>
    /// I tre simboli, in una forma che si puo' serializzare. Gli Sprite no: sono
    /// costruiti in memoria e marcati HideAndDontSave, quindi un riferimento
    /// salvato in una scena o in un prefab torna null al reload. Chi deve
    /// ricordarsi quale simbolo mostrare salva questo enum e chiama
    /// <see cref="Of"/> a runtime — vedi <see cref="GlyphIcon"/>.
    /// </summary>
    public enum Kind { Sword, Shield, BrokenShield }

    public static Sprite Sword => Get("sword", SwordArt);
    public static Sprite Shield => Get("shield", ShieldArt);
    public static Sprite BrokenShield => Get("broken", BrokenShieldArt);

    public static Sprite Of(Kind kind) => kind switch
    {
        Kind.Shield => Shield,
        Kind.BrokenShield => BrokenShield,
        _ => Sword,
    };

    static Sprite Get(string key, string[] art)
    {
        if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;

        int h = art.Length;
        int w = art[0].Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "glyph_" + key,
            hideFlags = HideFlags.HideAndDontSave,
        };

        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            // L'array parte dall'alto, la texture dal basso.
            string row = art[h - 1 - y];
            for (int x = 0; x < w; x++)
                pixels[y * w + x] = x < row.Length && row[x] == '#'
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(255, 255, 255, 0);
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, false);

        var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 12f);
        sprite.name = "glyph_" + key;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        _cache[key] = sprite;
        return sprite;
    }
}
