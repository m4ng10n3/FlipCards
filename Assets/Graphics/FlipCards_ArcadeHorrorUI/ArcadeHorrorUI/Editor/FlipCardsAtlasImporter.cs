// AUTO-GENERATO - FlipCards Arcade Horror UI Kit
// Mettere in una cartella "Editor". Menu: Tools > FlipCards > Import UI Kit
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FlipCards.UI.EditorTools
{
    public class FlipCardsAtlasImporter : EditorWindow
    {
        [System.Serializable] public class SpriteEntry
        {
            public string name; public string group; public int[] size;
            public int[] atlasRect; public float[] pivot; public int[] border;
            public bool tileable; public bool nineSlice; public string note;
        }
        [System.Serializable] class Manifest { public List<SpriteEntry> sprites; }

        // percorso della cartella del kit, relativo al progetto
        const string KitRoot = "Assets/Graphics/ArcadeHorrorUI";
        const string ManifestPath = KitRoot + "/flipcards_ui_manifest.json";
        const float PixelsPerUnit = 1f;   // UI: 1 px sprite = 1 px canvas

        [MenuItem("Tools/FlipCards/Import UI Kit")]
        public static void ImportAll()
        {
            if (!File.Exists(ManifestPath))
            { Debug.LogError("Manifest non trovato: " + ManifestPath); return; }

            var man = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            var byName = new Dictionary<string, SpriteEntry>();
            foreach (var s in man.sprites) byName[s.name] = s;

            // 1) sprite singoli
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { KitRoot });
            int done = 0;
            try
            {
                foreach (var g in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(g);
                    EditorUtility.DisplayProgressBar("FlipCards UI Kit", path, (float)done / guids.Length);
                    var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (ti == null) continue;

                    var file = Path.GetFileNameWithoutExtension(path);
                    bool isAtlas = file.StartsWith("flipcards_ui_atlas");
                    SpriteEntry e = null; byName.TryGetValue(file, out e);

                    ti.textureType = TextureImporterType.Sprite;
                    ti.spritePixelsPerUnit = PixelsPerUnit;
                    ti.filterMode = FilterMode.Point;
                    ti.mipmapEnabled = false;
                    ti.alphaIsTransparency = true;
                    ti.spriteMeshType = SpriteMeshType.FullRect;
                    ti.wrapMode = (e != null && e.tileable) ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                    var ps = ti.GetDefaultPlatformTextureSettings();
                    ps.textureCompression = TextureImporterCompression.Uncompressed;
                    ti.SetPlatformTextureSettings(ps);

                    if (isAtlas) ApplyAtlas(ti, path, man, file.EndsWith("@2x") ? 2 : 1);
                    else
                    {
                        ti.spriteImportMode = SpriteImportMode.Single;
                        if (e != null && e.nineSlice)
                        {
                            int k = ScaleOfPath(path);
                            ti.spriteBorder = new Vector4(e.border[0] * k, e.border[1] * k,
                                                          e.border[2] * k, e.border[3] * k);
                        }
                    }
                    ti.SaveAndReimport();
                    done++;
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
            AssetDatabase.Refresh();
            Debug.Log($"[FlipCards] importate {done} texture con Point filter, no compression, bordi 9-slice.");
        }

        static int ScaleOfPath(string path)
        {
            if (path.Contains("/4x/")) return 4;
            if (path.Contains("/2x/")) return 2;
            return 1;
        }

        // taglia l'atlante in sprite multipli leggendo i rect dal manifest
        static void ApplyAtlas(TextureImporter ti, string path, Manifest man, int k)
        {
            ti.spriteImportMode = SpriteImportMode.Multiple;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            int atlasH = tex != null ? tex.height : 1024 * k;

            var list = new List<SpriteMetaData>();
            foreach (var e in man.sprites)
            {
                if (e.atlasRect == null || e.atlasRect.Length != 4) continue;
                int x = e.atlasRect[0] * k, y = e.atlasRect[1] * k;
                int w = e.atlasRect[2] * k, h = e.atlasRect[3] * k;
                var md = new SpriteMetaData
                {
                    name = e.name,
                    // il manifest usa origine in alto a sinistra, Unity in basso a sinistra
                    rect = new Rect(x, atlasH - y - h, w, h),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(e.pivot[0], e.pivot[1]),
                    border = new Vector4(e.border[0] * k, e.border[1] * k,
                                         e.border[2] * k, e.border[3] * k)
                };
                list.Add(md);
            }
#pragma warning disable 0618
            ti.spritesheet = list.ToArray();
#pragma warning restore 0618
        }
    }
}
#endif
