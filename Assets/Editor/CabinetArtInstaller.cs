using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Deterministic installation contract shared by the editor and Kilo.</summary>
public static class CabinetArtInstaller
{
    public const string BundleRoot = "Assets/Graphics/1_NeonMonte_Attivo/SceneKit_v2/10_CabinetIntegrated/";
    public const string ActivePath = "Assets/Resources/ActiveCabinetArt.asset";
    [Serializable] public sealed class Manifest
    {
        public string id, cabinet, mask;
        public Vector2 sourceSize;
        public CabinetArtDefinition.Bank[] banks;
        public CabinetArtDefinition.MeasuredRect[] windowRegions;
    }
    public static Manifest Inspect()
    {
        var m = JsonUtility.FromJson<Manifest>(File.ReadAllText(BundleRoot + "bundle.json"));
        if(m == null || m.id != "cabinet-integrated-v4" || m.banks == null || m.banks.Length != 9 || m.windowRegions == null || m.windowRegions.Length != 3 || m.sourceSize.x<=0 || m.sourceSize.y<=0)
            throw new InvalidOperationException("Invalid cabinet bundle structure");
        foreach(var r in m.windowRegions)ValidateRect(r,m.sourceSize);
        var pairs = new System.Collections.Generic.HashSet<int>();
        foreach(var bank in m.banks)
        {
            ValidateRect(bank.totalRect,m.sourceSize);
            if(bank.lane < 0 || bank.lane > 2 || bank.stat < 0 || bank.stat > 2 || !pairs.Add(bank.lane * 3 + bank.stat) || bank.glassRadius.x <= 0 || bank.glassRadius.y <= 0 || bank.step.sqrMagnitude < 1)
                throw new InvalidOperationException("Invalid or duplicate glass bank");
            for(int n=0;n<7;n++) {
                var p=bank.first+n*bank.step;
                if(p.x-bank.glassRadius.x<0 || p.y-bank.glassRadius.y<0 || p.x+bank.glassRadius.x>m.sourceSize.x || p.y+bank.glassRadius.y>m.sourceSize.y)
                    throw new InvalidOperationException("Glass region outside artwork");
            }
        }
        if(!m.cabinet.StartsWith(BundleRoot,StringComparison.Ordinal) || !m.mask.StartsWith(NeonMonteSkinBuilder.Root+"/",StringComparison.Ordinal) || m.cabinet.Contains("..") || m.mask.Contains("..") || !File.Exists(m.cabinet) || !File.Exists(m.mask))
            throw new InvalidOperationException("Invalid bundle asset path");
        return m;
    }

    static void ValidateRect(CabinetArtDefinition.MeasuredRect r,Vector2 size) {
        if(r.x<0 || r.y<0 || r.width<=0 || r.height<=0 || r.x+r.width>size.x || r.y+r.height>size.y)
            throw new InvalidOperationException("Invalid measured rectangle in cabinet manifest");
    }

    [MenuItem("FlipCards/Installa cassa con luci integrate")]
    public static void Install()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play in a separate command before installation");
        var m=Inspect();
        var sprite=MedallionSceneSkin.Load("",m.cabinet.Substring(0,m.cabinet.Length-4));
        var mask=MedallionSceneSkin.Load("",m.mask.Substring(0,m.mask.Length-4));
        if(sprite.texture.width!=(int)m.sourceSize.x || sprite.texture.height!=(int)m.sourceSize.y)
            throw new InvalidOperationException("Artwork dimensions differ from measured manifest");
        var definition=AssetDatabase.LoadAssetAtPath<CabinetArtDefinition>(ActivePath);
        if(definition==null) { definition=ScriptableObject.CreateInstance<CabinetArtDefinition>();AssetDatabase.CreateAsset(definition,ActivePath); }
        Undo.RecordObject(definition,"Install integrated cabinet art");
        definition.bundleId=m.id;definition.illustration=sprite.texture;definition.silhouette=mask.texture;
        definition.sourceSize=m.sourceSize;definition.banks=m.banks;definition.windowRegions=m.windowRegions;
        EditorUtility.SetDirty(definition);
        AssetDatabase.SaveAssets();
        FlipCardsLayoutBuilder.Rebuild();
    }
}
