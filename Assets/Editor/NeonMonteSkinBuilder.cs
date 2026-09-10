using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;

/// <summary>Pixel-art templates and restrained UI chrome matching the original monster illustrations.</summary>
public static class NeonMonteSkinBuilder
{
    /// <summary>The selected art style: only what the game mounts, plus its references and spec.</summary>
    public const string Root = "Assets/Graphics/1_NeonMonte_Attivo";
    /// <summary>Old assets and studies. Nothing here is mounted, except the kit below.</summary>
    public const string Archive = "Assets/Graphics/2_Archivio";
    // The old kit is still the base of the skin: keys Neon Monte does not redraw
    // keep pointing at its sprites. Archived, but read on every rebuild.
    public const string Kit = Archive + "/FlipCards_ArcadeHorrorUI/ArcadeHorrorUI/2x";

    public static Sprite Art(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(NeonMonteAssetOrganization.ArtPath(name));

    public static Sprite FinalArt(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(NeonMonteAssetOrganization.Final + "/" + path + ".png");

    public static Material PaperMaterial()
    {
        const string path = Root + "/Runtime/Materials/PrintedPaper.mat";
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

    public static Editions EditionFor(CardDefinition card)
    {
        switch (card.cardName.ToLowerInvariant())
        {
            case "vanguard": case "oracle": case "spark": case "scythe": return Editions.POLYCHROME;
            case "relay": case "bulwark": case "hex": case "bastion": return Editions.FOIL;
            default: return Editions.REGULAR;
        }
    }

    public static Material EditionMaterial(Editions edition)
    {
        if (edition == Editions.REGULAR) return PaperMaterial();
        var original = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Shader Graphs_CardShaderGraph.mat");
        if (original == null) return PaperMaterial();
        string path = Root + "/Runtime/Materials/Card_" + edition + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(original); AssetDatabase.CreateAsset(material, path); }
        foreach (var keyword in material.enabledKeywords)
            if (keyword.name.StartsWith("_EDITION_")) material.DisableKeyword(keyword);
        material.EnableKeyword("_EDITION_" + edition);
        material.SetFloat("_EDITION", edition == Editions.FOIL ? 2f : 1f);
        material.SetFloat("_poly_power", 0.3f);
        EditorUtility.SetDirty(material);
        return material;
    }

    public static void Prepare()
    {
        NeonMonteAssetOrganization.Organize();
        foreach (var path in Directory.GetFiles(Root + "/Runtime", "*.png", SearchOption.AllDirectories))
            if (!path.Replace('\\', '/').Contains("/Chrome/")) Import(path.Replace('\\', '/'), Vector4.zero);
        foreach (var face in new[] { "Front", "Back" })
            foreach (var directory in new[] { "Template", "Indicators", "Symbols" })
                foreach (var path in Directory.GetFiles(NeonMonteAssetOrganization.Final + "/" + face + "/" + directory, "*.png"))
                    Import(path.Replace('\\', '/'), Vector4.zero);
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
        Directory.CreateDirectory(Root + "/Runtime/Chrome");
        foreach (var key in new List<string>(entries.Keys))
        {
            if (key.StartsWith("card_front")) entries[key] = FinalArt("Front/Template/front_clean");
            else if (key.StartsWith("card_back")) entries[key] = FinalArt("Back/Template/back_clean");
            else if (key == "board_bg") entries[key] = Art("board") ?? entries[key];
            else if (key == "reel_backing") entries[key] = Art("reel_housing") ?? entries[key];
            else if (IsChrome(key)) entries[key] = Chrome(key, entries[key]);
        }
        foreach (var name in new[] { "sword", "shield", "broken", "flame", "wave", "thorn" })
        {
            var glyph = Art("glyph_" + name);
            if (glyph != null)
            {
                entries["glyph_" + name] = glyph;
                if (name != "broken") entries["icon_" + name] = glyph;
            }
        }
        PrepareParts(entries);
        entries["card_neutral"] = FinalArt("Back/Template/back_clean");
        foreach (var face in new[] { "Front", "Back" })
            foreach (var folder in new[] { "Symbols", "Indicators", "Template" })
                foreach (var path in Directory.GetFiles(NeonMonteAssetOrganization.Final + "/" + face + "/" + folder, "*.png"))
                    entries["final_" + face.ToLowerInvariant() + "_" + Path.GetFileNameWithoutExtension(path)] = AssetDatabase.LoadAssetAtPath<Sprite>(path.Replace('\\', '/'));
        var families = new[] { "sun", "moon", "saturn" };
        var glyphs = new[] { "flame", "wave", "thorn" };
        for (int i = 0; i < 3; i++) entries["glyph_" + glyphs[i]] = FinalArt("Front/Symbols/faction_" + families[i] + "_mask");
        entries["glyph_sword"] = FinalArt("Back/Symbols/attack_spade_mask");
        entries["glyph_shield"] = FinalArt("Back/Symbols/defense_club_B_mask");
        MedallionSceneSkin.Prepare(entries);
        skin.entries.Clear();
        foreach (var pair in entries)
            if (pair.Value != null) skin.entries.Add(new UiSkin.Entry { key = pair.Key, sprite = pair.Value });
        skin.neonMonte = true;
        skin.Invalidate();
        UiSkin.ForgetActive();
        EditorUtility.SetDirty(skin);
        AssetDatabase.SaveAssets();
    }

    public static Sprite SlotPaper => AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Runtime/Chrome/slot_paper.asset");

    static Sprite Part(string name, Texture2D texture, Rect rect, float ppu = 1f, Vector4 border = default, bool inkCut = false)
    {
        string path = $"{Root}/Runtime/Chrome/{name}.asset";
        var sprite = Sprite.Create(texture, rect, new Vector2(.5f, .5f), ppu, 0, inkCut ? SpriteMeshType.Tight : SpriteMeshType.FullRect, border);
        sprite.name = name;
        if (inkCut) CutInkSilhouette(sprite, texture);
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing == null) { AssetDatabase.CreateAsset(sprite, path); return sprite; }
        EditorUtility.CopySerialized(sprite, existing);
        Object.DestroyImmediate(sprite);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    static void PrepareParts(Dictionary<string, Sprite> entries)
    {
        foreach (var key in new[] { "card_neutral" })
            if (Art(key) != null) entries[key] = Art(key);
        var lamps = AssetDatabase.LoadAssetAtPath<Texture2D>(NeonMonteAssetOrganization.ArtPath("cabinet_lamps"));
        if (lamps != null)
        {
            var names = new[] { "round", "spear", "shield" };
            float w = lamps.width / 3f, h = lamps.height / 2f;
            for (int row = 0; row < 2; row++)
            for (int col = 0; col < 3; col++)
            {
                string key = "lamp_" + names[col] + (row == 1 ? "_on" : "_off");
                entries[key] = Part(key, lamps, new Rect(col * w + w*.13f, row * h + h*.13f, w*.74f, h*.74f));
            }
        }
        // Final cards use delivered individual sprites instead of the old engraved sheet.
        var headers = AssetDatabase.LoadAssetAtPath<Texture2D>(NeonMonteAssetOrganization.ArtPath("slot_name_frames"));
        if (headers != null)
        {
            // Three equal source rows; the suit is part of each printed name frame.
            float h = headers.height * .338f;
            float[] centers = { .214f, .506f, .806f };
            for (int i = 0; i < 3; i++)
            {
                string key = "slot_name_" + (char)('A' + i);
                entries[key] = Part(key, headers, new Rect(0, headers.height*(1f-centers[i])-h*.5f, headers.width, h));
            }
            entries["slot_paper"] = Part("slot_paper", headers,
                new Rect(headers.width*.5f, headers.height*.74f, headers.height*.08f, headers.height*.08f));
        }
        var cabinet = AssetDatabase.LoadAssetAtPath<Texture2D>(NeonMonteAssetOrganization.ArtPath("cabinet_body"));
        if (cabinet != null)
            entries["reel_backing"] = Part("CabinetBody", cabinet, new Rect(0, 0, cabinet.width, cabinet.height),
                3f, new Vector4(cabinet.width*.04f, cabinet.height*.2f, cabinet.width*.105f, cabinet.height*.17f));
    }

    static void PrepareEngravedParts(Dictionary<string, Sprite> entries)
    {
        string path = NeonMonteAssetOrganization.ArtPath("card_engraved_parts");
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        if (!importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        // Artwork measured in the 1254px production sheet, never resynthesized.
        string[] names = { "sword", "shield", "heart", "point_on", "point_off", "terminal", "family_A", "family_B", "family_C" };
        Rect[] regions = {
            new Rect(128,25,176,384), new Rect(510,25,232,384), new Rect(918,25,254,384),
            new Rect(148,466,132,324), new Rect(560,466,132,324), new Rect(958,480,176,280),
            new Rect(29,865,366,331), new Rect(436,865,384,331), new Rect(855,865,372,331)
        };
        for (int i = 0; i < names.Length; i++)
        {
            var r = regions[i]; float scale = texture.width / 1254f;
            r = new Rect(Mathf.Round(r.x*scale), Mathf.Round(texture.height-(r.y+r.height)*scale), Mathf.Round(r.width*scale), Mathf.Round(r.height*scale));
            string key = "engraved_" + names[i];
            var sprite = Part(key, texture, r, 1f, default, true);
            entries[key] = sprite;
        }
    }

    // Native sprite-mesh cutout: retains original raster ink, removes the carrier
    // paper geometrically. Enclosed coloured areas (heart/family) stay intact.
    static void CutInkSilhouette(Sprite sprite, Texture2D texture)
    {
        var r = sprite.rect; int w=(int)r.width, h=(int)r.height;
        var pixels=texture.GetPixels((int)r.x,(int)r.y,w,h);
        var outside=new bool[w*h]; var queue=new Queue<int>();
        bool Paper(int n) { var c=pixels[n]; return c.r>c.g*1.4f && c.r>c.b*1.3f && c.r>.15f; }
        void Seed(int n) { if(!outside[n] && Paper(n)){outside[n]=true;queue.Enqueue(n);} }
        for(int x=0;x<w;x++){Seed(x);Seed((h-1)*w+x);}
        for(int y=0;y<h;y++){Seed(y*w);Seed(y*w+w-1);}
        while(queue.Count>0){int n=queue.Dequeue(),x=n%w,y=n/w;if(x>0)Seed(n-1);if(x<w-1)Seed(n+1);if(y>0)Seed(n-w);if(y<h-1)Seed(n+w);}
        // Discard detached print-grain specks; keep the connected ornament.
        var seen=new bool[w*h]; var largest=new List<int>();
        for(int n=0;n<seen.Length;n++)
        {
            if(seen[n]||outside[n])continue;
            var component=new List<int>();queue.Enqueue(n);seen[n]=true;
            while(queue.Count>0){int k=queue.Dequeue();component.Add(k);int x=k%w,y=k/w;
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){int xx=x+dx,yy=y+dy;if(xx<0||xx>=w||yy<0||yy>=h)continue;int j=yy*w+xx;if(!seen[j]&&!outside[j]){seen[j]=true;queue.Enqueue(j);}}}
            if(component.Count>largest.Count)largest=component;
        }
        var ink=new bool[w*h];foreach(int n in largest)ink[n]=true;
        var vertices=new List<Vector2>();var triangles=new List<ushort>();
        for(int y=0;y<h;y++)for(int x=0;x<w;)
        {
            if(!ink[y*w+x]){x++;continue;}int left=x;while(x<w&&ink[y*w+x])x++;
            ushort n=(ushort)vertices.Count;
            vertices.Add(new Vector2(left-w*.5f,y-h*.5f));vertices.Add(new Vector2(left-w*.5f,y+1-h*.5f));
            vertices.Add(new Vector2(x-w*.5f,y+1-h*.5f));vertices.Add(new Vector2(x-w*.5f,y-h*.5f));
            triangles.Add(n);triangles.Add((ushort)(n+1));triangles.Add((ushort)(n+2));triangles.Add(n);triangles.Add((ushort)(n+2));triangles.Add((ushort)(n+3));
        }
        const string path="Assets/Resources/EngravedInkLibrary.asset";
        var library=AssetDatabase.LoadAssetAtPath<EngravedInkLibrary>(path);
        if(library==null){library=ScriptableObject.CreateInstance<EngravedInkLibrary>();AssetDatabase.CreateAsset(library,path);}
        var normalized=new Vector2[vertices.Count];var uv=new Vector2[vertices.Count];
        for(int i=0;i<vertices.Count;i++)
        {
            var pixel=vertices[i]+new Vector2(w*.5f,h*.5f);
            normalized[i]=new Vector2(pixel.x/w,pixel.y/h);
            uv[i]=new Vector2((r.x+pixel.x)/texture.width,(r.y+pixel.y)/texture.height);
        }
        library.entries.RemoveAll(e=>e.key==sprite.name);
        library.entries.Add(new EngravedInkLibrary.Entry{key=sprite.name,vertices=normalized,uv=uv,triangles=triangles.ConvertAll(t=>(int)t).ToArray()});
        EditorUtility.SetDirty(library);
    }

    static bool IsChrome(string key) => key.StartsWith("panel_") || key.StartsWith("plate_") ||
        key.StartsWith("banner_") || key.StartsWith("bar_") || key.StartsWith("btn_") ||
        key.StartsWith("reel_") || key.StartsWith("flip_cell_") || key.StartsWith("ap_seg_") ||
        key.StartsWith("card_rim_") || key.EndsWith("slot_empty") || key.StartsWith("hand_dock_") ||
        key == "hand_lift" || key == "deck_pulse" || key.StartsWith("badge_") || key.StartsWith("micro_");

    public static Font DisplaySource => AssetDatabase.LoadAssetAtPath<Font>(Root + "/Runtime/Fonts/VT323-Regular.ttf");

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
        string path = $"{Root}/Runtime/Chrome/{key}.png";
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
