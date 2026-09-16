using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Explicit production assets for the approved 2026-09-10 composition.</summary>
public static class MedallionSceneSkin
{
    public const string Root = NeonMonteSkinBuilder.Root + "/SceneKit_v2/";

    /// <summary>Riga di taglio fra cappello e basamento del fungo, in pixel dall'alto.</summary>
    public const int MushroomCut = 600;

    // La leva in tre pezzi tagliati da 04_Controls/lever_rest.png. Misure in
    // pixel della tela, stampate da 08_Integration/Tools/build_lever_parts.py:
    // se si rigenera il taglio vanno ricopiate qui.
    public const float LeverLean = 10.767f;                                        // gradi, la cima pende a destra
    public const float LeverAttach = 1113.1f;                                      // dal perno al colletto
    public static readonly Vector2 LeverHubSize = new Vector2(470f, 670f);
    public static readonly Vector2 LeverHubPivot = new Vector2(.4273f, .5201f);    // asse del tamburo
    public static readonly Vector2 LeverShaftSize = new Vector2(162f, 684f);
    /// <summary>Quota della tela occupata dal tondino: per uno spessore voluto la tela va larga spessore / fill.</summary>
    public const float LeverShaftFill = .9093f;
    public static readonly Vector2 LeverKnobSize = new Vector2(463f, 497f);
    public static readonly Vector2 LeverKnobPivot = new Vector2(.5008f, .0121f);   // colletto

    public static Sprite Load(string relative) => Load(Root, relative);

    /// <summary>Faccia del rullo: carta, curvatura e filetto. Da 08_Integration/Tools/build_reel_face.py.</summary>
    public static Sprite ReelFace => Load("02_Machine/reel_face_ink");

    /// <summary>
    /// Ritaglio persistente da una texture gia' importata. Serve un asset vero,
    /// non uno Sprite creato al volo: la skin e' un ScriptableObject e un
    /// riferimento a uno sprite di sola memoria non sopravvive al reload.
    /// </summary>
    static Sprite Slice(string name, Texture2D texture, Rect rect)
    {
        const string folder = NeonMonteSkinBuilder.Root + "/Runtime/Chrome/";
        string path = folder + name + ".asset";
        var sprite = Sprite.Create(texture, rect, new Vector2(.5f, .5f), 1f, 0, SpriteMeshType.FullRect);
        sprite.name = name;
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing == null) { AssetDatabase.CreateAsset(sprite, path); return sprite; }
        EditorUtility.CopySerialized(sprite, existing);
        Object.DestroyImmediate(sprite);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    public static Sprite Load(string root, string relative)
    {
        string path = root + relative + ".png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { AssetDatabase.ImportAsset(path); importer = AssetImporter.GetAtPath(path) as TextureImporter; }
        if (importer == null) throw new System.InvalidOperationException("Missing scene asset: " + path);
        if (importer.textureType != TextureImporterType.Sprite || importer.spritePixelsPerUnit != 1 || importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed || importer.maxTextureSize != 4096)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 1;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    public static void Prepare(Dictionary<string, Sprite> entries)
    {
        PrepareGraphicUpgrade(entries);
        entries["scene_cabinet"] = Load("08_Integration/cabinet_clean");
        var integrated = Resources.Load<CabinetArtDefinition>("ActiveCabinetArt");
        if (integrated != null)
            entries["scene_cabinet"] = Load("", AssetDatabase.GetAssetPath(integrated.illustration).Replace(".png", ""));
        entries["scene_table"] = Load("03_Table/table_original_medallion_empty");
        entries["scene_lever"] = Load("04_Controls/lever_rest");
        entries["scene_attack"] = Load("08_Integration/mushroom_clean");

        // Il fungo e' un disegno solo e non ha un fotogramma "premuto" usabile:
        // quello dello studio v1 e' un altro bottone, disegnato piu' piccolo.
        // Lo taglio invece in cappello e basamento sulla riga dove il rosso
        // finisce (y=600 su 1254). Il basamento si disegna SOPRA, cosi' il
        // cappello che scende sparisce dietro la ghiera invece di uscirne.
        var mushroom = entries["scene_attack"].texture;
        entries["scene_attack_cap"] = Slice("scene_attack_cap", mushroom,
            new Rect(0, mushroom.height - MushroomCut, mushroom.width, MushroomCut));
        entries["scene_attack_base"] = Slice("scene_attack_base", mushroom,
            new Rect(0, 0, mushroom.width, mushroom.height - MushroomCut));
        entries["scene_paper"] = Load("06_UI/legend_paper");
        entries["scene_reel"] = Load("02_Machine/reel_drum_blank");
        entries["scene_reel_face"] = ReelFace;

        // Leva in tre pezzi dallo stesso disegno a inchiostro della cassa:
        // tamburo fermo, asta che ruota, manopola che si avvicina. I pezzi
        // realistici dello studio v1 erano un altro stile, e il loro perno a
        // disco non poteva appoggiarsi al fianco della cassa.
        entries["scene_lever_hub"] = Load("04_Controls/lever_ink_hub");
        entries["scene_lever_socket"] = Load("04_Controls/lever_ink_socket");
        entries["scene_lever_shaft"] = Load("04_Controls/lever_ink_shaft");
        entries["scene_lever_knob"] = Load("04_Controls/lever_ink_knob");

        foreach (var state in new[] { "on", "off" })
        {
            entries["lamp_round_" + state] = Load("05_Lamps/hp_round_" + state);
            entries["lamp_spear_" + state] = Load("05_Lamps/atk_taper_" + state);
            entries["lamp_shield_" + state] = Load("05_Lamps/def_discharge_" + state);
        }

        var atlas = Load("09_Instruments_v3/lamps_atlas");
        // vita OFF (112,44,384,440), ON (112,532,384,440)
        entries["lamp_v3_round_off"] = Slice("lamp_v3_round_off", atlas.texture, new Rect(112, 540, 384, 440));
        entries["lamp_v3_round_on"]  = Slice("lamp_v3_round_on",  atlas.texture, new Rect(112, 52,  384, 440));
        // attacco OFF (638,44,260,440), ON (638,532,260,440)
        entries["lamp_v3_spear_off"] = Slice("lamp_v3_spear_off", atlas.texture, new Rect(638, 540, 260, 440));
        entries["lamp_v3_spear_on"]  = Slice("lamp_v3_spear_on",  atlas.texture, new Rect(638, 52,  260, 440));
        // difesa OFF (1046,74,380,410), ON (1046,562,380,410)
        entries["lamp_v3_shield_off"] = Slice("lamp_v3_shield_off", atlas.texture, new Rect(1046, 540, 380, 410));
        entries["lamp_v3_shield_on"]  = Slice("lamp_v3_shield_on",  atlas.texture, new Rect(1046, 52,  380, 410));
    }

    const string Upgrade = "11_GraphicUpgrade/";
    const string Props = "12_TableProps/";
    static void PrepareGraphicUpgrade(Dictionary<string, Sprite> entries)
    {
        foreach (var name in new[] { "book_closed", "parchment" })
            entries["upgrade_" + name] = Load(Upgrade + name);
        var roller = Load(Upgrade + "roller").texture;
        entries["upgrade_roller"] = Slice("upgrade_roller", roller, new Rect(0, 724-445, 2172, 200));

        // Display LED: pixel originali ridipinti con la griglia forata, stessi
        // rettangoli della tela sorgente (12_TableProps/Tools/build_led_displays.py).
        entries["led_boss_cheek"] = Load(Props + "led_boss_cheek");
        entries["led_button_base"] = Load(Props + "led_button_base");

        // Libretto, segnalibri e oggetti di scena (12_TableProps/Tools/build_book.py).
        foreach (var name in new[] { "book_spread_left", "book_spread_right", "book_leaf_left", "book_leaf_right",
                                     "book_cover_front", "bookmark_campo", "bookmark_mano", "bookmark_rullo",
                                     "bookmark_registro", "table_shadow", "ap_constellation", "ap_star_on", "ap_star_off" })
            entries[name] = Load(Props + name);
    }

    public static Material BookLeafMaterial()
    {
        string path = Root + Props + "BookLeaf.mat";
        var shader = Shader.Find("FlipCards/Book Leaf");
        if (shader == null) throw new System.InvalidOperationException("Book leaf shader missing");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
        mat.shader = shader;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ── Display LED ──────────────────────────────────────────────────────────
    // Numeri copiati da 12_TableProps/led_displays.json: se si rigenera la
    // griglia vanno ricopiati qui, come per la leva.

    /// <summary>Rettangolo della guancia ridipinta su cabinet.png (x, y dall'alto, w, h).</summary>
    public static readonly (float x, float y, float w, float h) BossLedRect = (94f, 178f, 86f, 546f);

    public static void ConfigureBossLed(LedMatrix matrix, float scaleX, float scaleY)
    {
        matrix.shape = LedMatrix.Shape.Quad;
        matrix.columns = 3; matrix.rows = 62;
        matrix.sourceOrigin = new Vector2(BossLedRect.x, BossLedRect.y);
        matrix.sourceScale = new Vector2(scaleX, scaleY);
        matrix.topLeft = new Vector2(128.2f, 209.2f); matrix.topRight = new Vector2(149.3f, 209.2f);
        matrix.bottomLeft = new Vector2(114.7f, 702.8f); matrix.bottomRight = new Vector2(142.8f, 702.8f);
    }

    public static void ConfigurePlayerLed(LedMatrix matrix, float scale)
    {
        matrix.shape = LedMatrix.Shape.ConeArc;
        matrix.columns = 40; matrix.rows = 2;
        matrix.sourceOrigin = new Vector2(0f, MushroomCut);
        matrix.sourceScale = new Vector2(scale, scale);
        matrix.arcCenter = new Vector2(627f, 880f); matrix.arcRadii = new Vector2(585f, 231f); matrix.cone = .10f;
        matrix.thetaRange = new Vector2(-1.068034f, 1.068034f);
        matrix.dyRange = new Vector2(-159f, -119f);
    }

    public static Material LedMaterial(bool glow)
    {
        string path = Root + Props + (glow ? "LedGlow.mat" : "LedEmitter.mat");
        var shader = Shader.Find("FlipCards/LED Matrix");
        if (shader == null) throw new System.InvalidOperationException("LED matrix shader missing");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
        mat.shader = shader;
        mat.SetFloat("_Glow", glow ? 1 : 0);
        mat.SetFloat("_SrcBlend", glow ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", glow ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        // Bagliore basso e stretto: piu' largo lava la griglia e il display torna
        // a sembrare una barra piena.
        mat.SetFloat("_Intensity", .2f);
        mat.SetFloat("_Spread", .42f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    public static Material AnimatedSurface(bool roller)
    {
        string path = Root + Upgrade + (roller ? "Roller.mat" : "Sky.mat");
        var shader = Shader.Find("FlipCards/Animated Illustrated Surface");
        if (shader == null) throw new System.InvalidOperationException("Animated surface shader missing");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
        mat.shader = shader;
        mat.SetFloat("_Mode", roller ? 1 : 0);
        if (!roller)
        {
            // Disco da 4096 che gira attorno a un polo sotto il tavolo: a 1080p
            // copre 3400 pixel di canvas, cioe' ~1,2 texel per pixel.
            mat.SetTexture("_SkyTex", SkyDisc());
            mat.SetVector("_Canvas", new Vector4(1920, 1080, 0, 0));
            mat.SetVector("_Pole", new Vector4(960, 1250, 0, 0));
            mat.SetFloat("_Coverage", 3400);
            mat.SetFloat("_Spin", .0075f);
            mat.SetFloat("_NearSpin", 1.35f);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>
    /// Il cielo non e' uno sprite: e' una texture campionata ruotata. Compressa
    /// in alta qualita' (16 MB invece di 64) e senza mipmap, perche' a schermo
    /// non scende mai sotto la sua risoluzione.
    /// </summary>
    static Texture2D SkyDisc()
    {
        string path = Root + Props + "sky_disc.png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { AssetDatabase.ImportAsset(path); importer = AssetImporter.GetAtPath(path) as TextureImporter; }
        if (importer == null) throw new System.InvalidOperationException("Missing sky disc: " + path);
        if (importer.textureType != TextureImporterType.Default || importer.mipmapEnabled || importer.maxTextureSize != 4096
            || importer.textureCompression != TextureImporterCompression.CompressedHQ || importer.wrapMode != TextureWrapMode.Clamp)
        {
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    public static Material CabinetMaterial()
    {
        var integrated = Resources.Load<CabinetArtDefinition>("ActiveCabinetArt");
        if (integrated != null)
        {
            const string integratedPath = CabinetArtInstaller.BundleRoot + "IntegratedCabinet.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(integratedPath);
            var shader = Shader.Find("FlipCards/Integrated Cabinet Lamps");
            if(shader == null) throw new System.InvalidOperationException("Integrated cabinet shader missing");
            if(mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, integratedPath); }
            mat.shader = shader;
            mat.SetTexture("_MaskTex", integrated.silhouette);
            mat.SetVector("_SourceSize", integrated.sourceSize);
            var positions = new Vector4[9]; var sizes = new Vector4[9]; var states = new Vector4[9];
            for(int i=0;i<9;i++) {
                var bank=integrated.banks[i];
                positions[i]=new Vector4(bank.first.x,bank.first.y,bank.step.x,bank.step.y);
                sizes[i]=new Vector4(bank.glassRadius.x,bank.glassRadius.y,bank.stat,0);
                states[i]=new Vector4(0,-1,0,0);
            }
            var windows=new Vector4[3];
            for(int i=0;i<3;i++){var r=integrated.windowRegions[i];windows[i]=new Vector4(r.x,r.y,r.width,r.height);}
            mat.SetVectorArray("_GlassPositions",positions);mat.SetVectorArray("_GlassSizes",sizes);
            mat.SetVectorArray("_BankStates",states);mat.SetVectorArray("_WindowRegions",windows);
            EditorUtility.SetDirty(mat);
            return mat;
        }
        // ImageGen preserved the shape but delivered RGB. Reuse the authored alpha
        // texture in a UI material: no resampling or modification of either source.
        const string path = Root + "08_Integration/CabinetMasked.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("FlipCards/Illustration Alpha Mask"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetTexture("_MaskTex", Load("02_Machine/cabinet_illustrated").texture);
        EditorUtility.SetDirty(material);
        return material;
    }
}
