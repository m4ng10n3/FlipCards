// AUTO-GENERATO - FlipCards Arcade Horror UI Kit
// Costruttore serializzato di carte / nemici a partire dai dati.
using UnityEngine;
using UnityEngine.UI;

namespace FlipCards.UI
{
    /// Dati minimi da cui si costruisce una carta o un nemico.
    [System.Serializable]
    public class CardView
    {
        public string displayName;
        public Faction faction = Faction.C;
        public Sprite portrait;
        public int atk, hp, hpMax, def;
        public Face[] flipPattern = new[] { Face.Front, Face.Back, Face.Front };
        public int flipIndex;
        public Face face = Face.Front;
        public bool isEnemy;
    }
    public enum Faction { A, B, C }
    public enum Face { Front, Back }

    /// Monta gli sprite del kit su una gerarchia uGUI seguendo l'anatomia del manifest.
    /// Ogni figlio e' posizionato dai RectInt di UIKit.CardAnatomy: cambiando il kit
    /// non serve toccare le scene, basta rigenerare gli sprite con gli stessi nomi.
    public class CardBuilder : MonoBehaviour
    {
        public SpriteLibrary lib;              // vedi SpriteLibrary.cs
        public int scale = UIKit.Scale;        // 2 = asset @2x su canvas 1920x1080

        public RectTransform Build(CardView d, RectTransform parent)
        {
            var root = NewRect("Card_" + d.displayName, parent,
                               UIKit.Card.x * scale, UIKit.Card.y * scale);

            // 1. ritratto SOTTO la cornice (la finestra della cornice e' trasparente)
            var art = AddImage(root, "Art", d.portrait, UIKit.CardAnatomy.ArtWindow);
            if (art) art.preserveAspect = true;

            // 2. cornice
            string frame = d.isEnemy ? "enemy_panel_" + d.faction : "card_front_" + d.faction;
            AddImage(root, "Frame", lib.Get(frame), new RectInt(0, 0, UIKit.Card.x, UIKit.Card.y));

            // 3. tag fazione
            AddImage(root, "Faction", lib.Get("tag_faction_" + d.faction), UIKit.CardAnatomy.FactionTag);

            // 4. statistiche
            AddStat(root, "Atk", "badge_atk", UIKit.CardAnatomy.StatSlots[0], d.atk.ToString());
            AddStat(root, "Hp", "badge_hp", UIKit.CardAnatomy.StatSlots[1], d.hp + "/" + d.hpMax);
            AddStat(root, "Def", "badge_def", UIKit.CardAnatomy.StatSlots[2], d.def.ToString());

            // 5. pattern di flip
            for (int i = 0; i < UIKit.CardAnatomy.FlipCells.Length && i < d.flipPattern.Length; i++)
            {
                string cell = i == d.flipIndex ? "flip_cell_current"
                            : (d.flipPattern[i] == Face.Front ? "flip_cell_front" : "flip_cell_back");
                AddStat(root, "Flip" + i, cell, UIKit.CardAnatomy.FlipCells[i],
                        d.flipPattern[i] == Face.Front ? "F" : "R");
            }

            // 6. banner di stato (9-slice)
            string banner = d.isEnemy ? "banner_enemy" : (d.face == Face.Front ? "banner_front" : "banner_back");
            var b = AddImage(root, "State", lib.Get(banner), UIKit.CardAnatomy.StateBanner);
            if (b) b.type = Image.Type.Sliced;

            return root;
        }

        // ---------- helpers ----------
        RectTransform NewRect(string n, RectTransform parent, float w, float h)
        {
            var go = new GameObject(n, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        Image AddImage(RectTransform parent, string n, Sprite s, RectInt r)
        {
            if (s == null && n != "Art") return null;
            var rt = NewRect(n, parent, r.width * scale, r.height * scale);
            rt.anchoredPosition = new Vector2(r.x * scale, -r.y * scale);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = s;
            img.raycastTarget = false;
            if (s == null) img.color = new Color(0, 0, 0, 0);
            return img;
        }

        void AddStat(RectTransform parent, string n, string sprite, RectInt r, string value)
        {
            var img = AddImage(parent, n, lib.Get(sprite), r);
            if (img == null) return;
            var t = new GameObject("Value", typeof(RectTransform)).GetComponent<RectTransform>();
            t.SetParent(img.rectTransform, false);
            t.anchorMin = Vector2.zero; t.anchorMax = Vector2.one;
            t.offsetMin = new Vector2(11 * scale, 0); t.offsetMax = Vector2.zero;
            var txt = t.gameObject.AddComponent<Text>();
            txt.text = value; txt.alignment = TextAnchor.MiddleCenter;
            txt.raycastTarget = false;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 6 * scale;
            // sostituire con TextMeshProUGUI + font pixel per la resa finale
        }
    }
}
