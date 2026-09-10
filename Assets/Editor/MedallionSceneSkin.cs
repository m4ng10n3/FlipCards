using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Explicit production assets for the approved 2026-09-10 composition.</summary>
public static class MedallionSceneSkin
{
    public const string Root = "Assets/Graphics/NeonMonte/SceneKit_v2/";

    // I comandi fisici stanno ancora nello studio v1: la leva e' l'unico pezzo
    // consegnato in parti separate (perno, asta, manopola), che e' quel che
    // serve per animare la tirata invece di ruotare un'immagine sola.
    public const string RootV1 = "Assets/Graphics/NeonMonte/SceneKit_v1/";

    /// <summary>Riga di taglio fra cappello e basamento del fungo, in pixel dall'alto.</summary>
    public const int MushroomCut = 600;

    public static Sprite Load(string relative) => Load(Root, relative);

    /// <summary>
    /// Ritaglio persistente da una texture gia' importata. Serve un asset vero,
    /// non uno Sprite creato al volo: la skin e' un ScriptableObject e un
    /// riferimento a uno sprite di sola memoria non sopravvive al reload.
    /// </summary>
    static Sprite Slice(string name, Texture2D texture, Rect rect)
    {
        const string folder = "Assets/Graphics/NeonMonte/Runtime/Chrome/";
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

        // Leva in tre pezzi: il perno non si muove, l'asta ruota, la manopola
        // insegue la proiezione. Vedi SceneKit_v1/Layout/animation_spec.md.
        entries["scene_lever_pivot"] = Load(RootV1, "Controls/lever_pivot");
        entries["scene_lever_shaft"] = Load(RootV1, "Controls/lever_shaft");
        entries["scene_lever_grip"] = Load(RootV1, "Controls/lever_grip");

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
