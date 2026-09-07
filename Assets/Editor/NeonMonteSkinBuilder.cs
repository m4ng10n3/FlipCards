using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;

/// <summary>Pixel-art templates and restrained UI chrome matching the original monster illustrations.</summary>
public static class NeonMonteSkinBuilder
{
    public const string Root = "Assets/Graphics/NeonMonte";
    const string Kit = "Assets/Graphics/FlipCards_ArcadeHorrorUI/ArcadeHorrorUI/2x";

    public static Sprite Art(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/{name}.png");

    public static Material PaperMaterial()
    {
        const string path = Root + "/PrintedPaper.mat";
        var shader = Shader.Find("FlipCards/Printed Paper");
        if (shader == null) return null;
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetFloat("_PaperSheen", 0.12f);
        EditorUtility.SetDirty(material);
        return material;
    }

    public static void Prepare()
    {
        if (!File.Exists(Root + "/card_front.png") || !File.Exists(Root + "/card_back.png")) return;
        foreach (var path in Directory.GetFiles(Root, "*.png")) Import(path.Replace('\\', '/'), Vector4.zero);
        PrepareDisplayFont();
        var pathSkin = "Assets/Resources/FlipCardsUiSkin.asset";
        var skin = AssetDatabase.LoadAssetAtPath<UiSkin>(pathSkin);
        if (skin == null)
        {
            skin = ScriptableObject.CreateInstance<UiSkin>();
            AssetDatabase.CreateAsset(skin, pathSkin);
        }
        var entries = new Dictionary<string, Sprite>();
        // Always start from the source kit: repeated rebuilds cannot progressively alter a sprite.
        foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { Kit }))
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid));
            if (sprite != null) entries[sprite.name] = sprite;
        }
        Directory.CreateDirectory(Root + "/Chrome");
        foreach (var key in new List<string>(entries.Keys))
        {
            if (key.StartsWith("card_front")) entries[key] = Art("card_front");
            else if (key.StartsWith("card_back")) entries[key] = Art("card_back");
            else if (key == "board_bg") entries[key] = Art("board") ?? entries[key];
            else if (key == "reel_backing") entries[key] = Art("reel_housing") ?? entries[key];
            else if (IsChrome(key)) entries[key] = Chrome(key, entries[key]);
        }
        foreach (var name in new[] { "sword", "shield", "broken" })
        {
            var glyph = Art("glyph_" + name);
            if (glyph != null)
            {
                entries["glyph_" + name] = glyph;
                if (name != "broken") entries["icon_" + name] = glyph;
            }
        }
        skin.entries.Clear();
        foreach (var pair in entries)
            if (pair.Value != null) skin.entries.Add(new UiSkin.Entry { key = pair.Key, sprite = pair.Value });
        skin.neonMonte = true;
        skin.Invalidate();
        UiSkin.ForgetActive();
        EditorUtility.SetDirty(skin);
        AssetDatabase.SaveAssets();
    }

    static bool IsChrome(string key) => key.StartsWith("panel_") || key.StartsWith("plate_") ||
        key.StartsWith("banner_") || key.StartsWith("bar_") || key.StartsWith("btn_") ||
        key.StartsWith("reel_") || key.StartsWith("flip_cell_") || key.StartsWith("ap_seg_") ||
        key.StartsWith("card_rim_") || key.EndsWith("slot_empty") || key.StartsWith("hand_dock_") ||
        key == "hand_lift" || key == "deck_pulse" || key.StartsWith("badge_") || key.StartsWith("micro_");

    public static Font DisplaySource => AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/VT323-Regular.ttf");

    static void PrepareDisplayFont()
    {
        const string path = "Assets/Resources/NeonMonteDisplay.asset";
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path) != null || DisplaySource == null) return;
        var font = TMP_FontAsset.CreateFontAsset(DisplaySource);
        font.name = "NeonMonteDisplay";
        string characters = "";
        for (int i = 32; i < 256; i++) characters += (char)i;
        font.TryAddCharacters(characters);
        font.fallbackFontAssetTable = new List<TMP_FontAsset> { TMP_Settings.defaultFontAsset };
        AssetDatabase.CreateAsset(font, path);
        if (font.material != null) AssetDatabase.AddObjectToAsset(font.material, font);
        foreach (var atlas in font.atlasTextures)
            if (atlas != null) AssetDatabase.AddObjectToAsset(atlas, font);
        EditorUtility.SetDirty(font);
    }

    static Color Accent(string key)
    {
        if (key.EndsWith("_A") || key.Contains("blood") || key.Contains("boss") || key.Contains("atk")) return GamePalette.Danger;
        if (key.EndsWith("_C") || key.Contains("phos") || key.Contains("hp")) return GamePalette.PlayerHp;
        if (key.Contains("amber") || key.EndsWith("front") || key == "reel_frame" || key == "reel_payline") return GamePalette.Fronte;
        if (key.Contains("charge") || key.Contains("flip_cell") || key.StartsWith("hand_")) return GamePalette.Charge;
        return GamePalette.Retro;
    }

    static Sprite Chrome(string key, Sprite source)
    {
        string path = $"{Root}/Chrome/{key}.png";
        // Retain source dimensions: reel glass/payline use sprite height as geometry.
        int w = Mathf.RoundToInt(source.rect.width), h = Mathf.RoundToInt(source.rect.height);
        bool ring = key.StartsWith("card_rim") || key.StartsWith("reel_cell") || key == "reel_frame" ||
            key.EndsWith("slot_empty") || key == "deck_pulse" || key == "hand_lift";
        bool fill = key.StartsWith("bar_fill") || key.Contains("seg_full") || key == "flip_cell_current";
        bool glass = key == "reel_glass";
        bool blur = key == "reel_col_blur";
        bool highlight = key == "reel_col_highlight";
        bool badge = key.StartsWith("badge_") || key.StartsWith("micro_");
        bool quiet = key.StartsWith("panel_") || key.StartsWith("plate_") || key.StartsWith("banner_");
        bool button = key.StartsWith("btn_");
        var accent = Accent(key);
        if (key.EndsWith("disabled") || key.Contains("unknown") || key.Contains("empty") || key == "reel_cell_locked")
            accent = Color.Lerp(GamePalette.BorderHi, accent, 0.2f);
        float strength = key.EndsWith("hover") ? 1f : key.EndsWith("press") ? 0.65f : 0.8f;
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float v = (float)y / Mathf.Max(1, h - 1);
            int edge = Mathf.Min(x, y, w - 1 - x, h - 1 - y);
            Color c = GamePalette.PanelSunken;
            if (ring) c = Color.clear;
            if (edge < 2) c = Color.Lerp(GamePalette.Border, accent, quiet ? 0.18f : strength);
            else if (edge < 4) c = GamePalette.WithAlpha(accent, ring ? 0.32f : 0.18f);
            if (fill) c = accent;
            if (button)
            {
                c = Color.Lerp(GamePalette.PanelSunken, accent, key.EndsWith("disabled") ? 0.12f : key.EndsWith("hover") ? 0.65f : 0.32f);
                if (edge < 3) c = accent;
                else if (edge == 6) c = GamePalette.WithAlpha(GamePalette.Paper, 0.35f);
            }
            if (key.Contains("seg_empty") || key.Contains("seg_spent")) c = GamePalette.WithAlpha(GamePalette.BorderHi, 0.65f);
            if (glass) c = new Color(0.65f, 0.82f, 1f, v > 0.88f ? (v - 0.88f) * 0.5f : 0f);
            if (blur) c = GamePalette.WithAlpha(accent, ((x / 6) % 5 == 0 ? 0.13f : 0.015f) * Mathf.Sin(v * Mathf.PI));
            if (highlight) c = GamePalette.WithAlpha(GamePalette.Fronte, edge < 4 ? 0.8f : 0.035f);
            if (key == "reel_payline") c = GamePalette.WithAlpha(GamePalette.Fronte, Mathf.Abs(v - 0.5f) < 0.08f ? 0.35f : 0.015f);
            if (key.StartsWith("reel_sliver")) c = GamePalette.WithAlpha(GamePalette.BorderHi, 0.12f);
            if (key == "reel_frame") c = Color.clear; // The illustrated housing supplies its own perimeter.
            if (key.StartsWith("reel_pip"))
            {
                float dx = Mathf.Abs(x - w * 0.5f), dy = Mathf.Abs(y - h * 0.5f);
                bool current = key.EndsWith("current");
                bool on = current ? edge < 2 : key.EndsWith("front") ? dx + dy < w * 0.42f
                    : key.EndsWith("back") ? dx < w * 0.32f && dy < h * 0.28f : dx < w * 0.13f && dy < h * 0.13f;
                c = on ? (current ? GamePalette.Paper : accent) : Color.clear;
            }
            if (badge)
            {
                // Small code-native pictograms: numerals remain independent runtime text.
                float gx = (x - h * 0.44f) / (h * 0.25f), gy = (y - h * 0.5f) / (h * 0.27f);
                bool mark = key.Contains("hp") ? (Mathf.Abs(gx) < 0.28f || Mathf.Abs(gy) < 0.28f) && Mathf.Abs(gx) < 0.9f && Mathf.Abs(gy) < 0.9f
                    : key.Contains("def") ? Mathf.Abs(gx) < 0.8f && gy < 0.9f && gy > -1f + Mathf.Abs(gx) * 0.8f
                    : Mathf.Abs(gx) < 0.18f && Mathf.Abs(gy) < 1f || Mathf.Abs(gy + 0.45f) < 0.14f && Mathf.Abs(gx) < 0.65f;
                if (mark) c = accent;
            }
            // Stable pixel wear confined to outlines, never across numbers or illustration wells.
            if (edge < 4 && ((x / 2 * 17 + y / 2 * 31) % 67 == 0)) c.a *= 0.25f;
            pixels[y * w + x] = c;
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        var bytes = texture.EncodeToPNG();
        Object.DestroyImmediate(texture);
        // Do not trigger reimports on an unchanged rebuild.
        if (!File.Exists(path) || !Equal(File.ReadAllBytes(path), bytes))
        {
            File.WriteAllBytes(path, bytes);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
        var border = ring || fill || glass || blur || highlight || key == "reel_payline" || badge
            ? Vector4.zero : new Vector4(Mathf.Min(6, w / 3), Mathf.Min(6, h / 3), Mathf.Min(6, w / 3), Mathf.Min(6, h / 3));
        Import(path, border);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static bool Equal(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    static void Import(string path, Vector4 border)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            importer = AssetImporter.GetAtPath(path) as TextureImporter;
        }
        if (importer == null) return;
        if (importer.textureType == TextureImporterType.Sprite && importer.spritePixelsPerUnit == 1f &&
            importer.spriteBorder == border && !importer.mipmapEnabled && importer.filterMode == FilterMode.Point &&
            importer.textureCompression == TextureImporterCompression.Uncompressed && importer.maxTextureSize == 2048) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 1f;
        importer.spriteBorder = border;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
    }
}
