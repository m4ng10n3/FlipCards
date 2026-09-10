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
        entries["scene_cabinet"] = Load("08_Integration/cabinet_clean");
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
    }

    public static Material CabinetMaterial()
    {
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
