// AUTO-GENERATO - FlipCards Arcade Horror UI Kit
using System.Collections.Generic;
using UnityEngine;

namespace FlipCards.UI
{
    /// Contenitore serializzabile nome -> Sprite. I nomi sono quelli del manifest.
    [CreateAssetMenu(menuName = "FlipCards/UI Sprite Library")]
    public class SpriteLibrary : ScriptableObject
    {
        [System.Serializable] public struct Entry { public string key; public Sprite sprite; }
        public List<Entry> entries = new List<Entry>();
        Dictionary<string, Sprite> _map;

        public Sprite Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (_map == null)
            {
                _map = new Dictionary<string, Sprite>(entries.Count);
                foreach (var e in entries) if (e.sprite && !_map.ContainsKey(e.key)) _map[e.key] = e.sprite;
            }
            Sprite s; return _map.TryGetValue(key, out s) ? s : null;
        }

#if UNITY_EDITOR
        [ContextMenu("Popola dall'atlante")]
        void Populate()
        {
            entries.Clear();
            var path = "Assets/Graphics/ArcadeHorrorUI/atlas/flipcards_ui_atlas@2x.png";
            foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Sprite sp) entries.Add(new Entry { key = sp.name, sprite = sp });
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
