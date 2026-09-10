using System;
using System.IO;
using UnityEditor;

/// <summary>Moves assets through Unity so existing GUID references survive.</summary>
public static class NeonMonteAssetOrganization
{
    const string Root = NeonMonteSkinBuilder.Root;
    public const string Final = Root + "/Cards/_Final";
    /// <summary>Studies and superseded Neon Monte assets, outside the active style.</summary>
    public const string Studies = NeonMonteSkinBuilder.Archive + "/NeonMonte_Studi";

    public static string ArtPath(string name)
    {
        string folder = name.StartsWith("portrait_") ? "Portraits" : name.StartsWith("symbol_") || name.StartsWith("slot_") ? "Slots" :
            name.StartsWith("glyph_") ? "Glyphs" : name == "card_neutral" ? "Cards" :
            name.StartsWith("card_") || name == "back_seal" || name == "stat_ink_fill" || name == "base_front_unfinished" || name == "indie-concept-study" ? null : "Board";
        return folder == null ? Studies + "/PreviousCards/" + name + ".png" : Root + "/Runtime/" + folder + "/" + name + ".png";
    }

    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        Folder(Path.GetDirectoryName(path).Replace('\\', '/'));
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }

    static void Move(string source, string destination)
    {
        if (!File.Exists(source) && !Directory.Exists(source)) return;
        if (File.Exists(destination) || Directory.Exists(destination)) throw new InvalidOperationException("Asset destination already exists: " + destination);
        Folder(Path.GetDirectoryName(destination).Replace('\\', '/'));
        string error = AssetDatabase.MoveAsset(source, destination);
        if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
    }

    public static void Organize()
    {
        Move(Root + "/Cards_Final", Final);
        foreach (string name in new[] { "Front_French_v2", "Back_French_v2", "SharedCardAssets" })
            Move(Root + "/" + name, Studies + "/Cards_Sources/" + name);
        Move(Root + "/Chrome", Root + "/Runtime/Chrome");
        Move(Root + "/Fonts", Root + "/Runtime/Fonts");
        foreach (string path in Directory.GetFiles(Root, "*.png")) Move(path.Replace('\\', '/'), ArtPath(Path.GetFileNameWithoutExtension(path)));
        foreach (string path in Directory.GetFiles(Root, "*.mat")) Move(path.Replace('\\', '/'), Root + "/Runtime/Materials/" + Path.GetFileName(path));
        foreach (string path in Directory.GetFiles(Root, "*.md"))
            if (Path.GetFileName(path) != "README.md") Move(path.Replace('\\', '/'), Studies + "/Notes/" + Path.GetFileName(path));
    }
}
