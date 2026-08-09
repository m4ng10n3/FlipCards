// AUTO-GENERATO - FlipCards Arcade Horror UI Kit
// Costruttori serializzati: carta (fronte/retro) e casella del rullo nemico.
using UnityEngine;
using UnityEngine.UI;

namespace FlipCards.UI
{
    public enum Faction { A, B, C }
    public enum Face { Front, Back }

    [System.Serializable]
    public class CardView
    {
        public string displayName;
        public Faction faction = Faction.C;
        public Sprite portrait;                 // usato solo dalla faccia FRONTE
        public int atk, def, hp, hpMax;
        public Face[] flipPattern = { Face.Front, Face.Back, Face.Front };
        public int flipIndex;
        public Face face = Face.Front;
        public string abilityIcon = "icon_star";
        public string abilityShort;
    }

    [System.Serializable]
    public class EnemyView                      // nemico = simbolo su un rullo
    {
        public string displayName;
        public Faction faction = Faction.A;
        public Sprite symbol;
        public int atk, hp, hpMax, defCurrent;
        public Face[] flipPattern = { Face.Back, Face.Front, Face.Back };
        public int flipIndex;
        public bool held;                       // casella bloccata
    }

    /// Monta gli sprite del kit su uGUI usando le ancore del manifest.
    public class CardBuilder : MonoBehaviour
    {
        public SpriteLibrary lib;
        public int scale = UIKit.Scale;         // 2 = asset @2x su canvas 1920x1080

        // ---------------------------------------------------------- carta
        public RectTransform BuildCard(CardView d, RectTransform parent)
        {
            var root = NewRect("Card_" + d.displayName, parent,
                               UIKit.Card.x * scale, UIKit.Card.y * scale);
            bool front = d.face == Face.Front;

            // il ritratto sta SOTTO la cornice: la finestra e' trasparente
            if (front) AddImage(root, "Art", d.portrait, UIKit.CardAnatomy.ArtWindow, true);

            AddImage(root, "Frame", lib.Get((front ? "card_front_" : "card_back_") + d.faction),
                     new RectInt(0, 0, UIKit.Card.x, UIKit.Card.y));
            AddImage(root, "Faction", lib.Get("tag_faction_" + d.faction), UIKit.CardAnatomy.FactionTag);

            // FRONTE = ATK + HP, RETRO = DEF + HP. Nessun banner: la faccia si vede dal template.
            AddStat(root, "S0", front ? "badge_atk" : "badge_def", UIKit.CardAnatomy.StatSlots[0],
                    (front ? d.atk : d.def).ToString());
            AddStat(root, "S1", "badge_hp", UIKit.CardAnatomy.StatSlots[1], d.hp + "/" + d.hpMax);

            for (int i = 0; i < UIKit.CardAnatomy.FlipCells.Length && i < d.flipPattern.Length; i++)
                AddStat(root, "Flip" + i, CellSprite(d.flipPattern[i], i == d.flipIndex),
                        UIKit.CardAnatomy.FlipCells[i], d.flipPattern[i] == Face.Front ? "F" : "R");

            AddImage(root, "Ability", lib.Get(d.abilityIcon), UIKit.CardAnatomy.AbilityIcon);
            return root;
        }

        // ---------------------------------------------------- casella rullo
        public RectTransform BuildReelCell(EnemyView d, RectTransform parent)
        {
            var root = NewRect("Reel_" + d.displayName, parent,
                               UIKit.Cell.x * scale, UIKit.Cell.y * scale);

            AddImage(root, "Symbol", d.symbol, UIKit.ReelCell.SymbolWindow, true);
            AddImage(root, "Medallion", lib.Get("enemy_medallion_" + d.faction), UIKit.ReelCell.SymbolWindow);
            AddImage(root, "Cell", lib.Get(d.held ? "reel_cell_locked" : "reel_cell_" + d.faction),
                     new RectInt(0, 0, UIKit.Cell.x, UIKit.Cell.y));
            AddImage(root, "Faction", lib.Get("tag_faction_" + d.faction), UIKit.ReelCell.FactionTag);

            for (int i = 0; i < UIKit.ReelCell.FlipPips.Length && i < d.flipPattern.Length; i++)
                AddImage(root, "Pip" + i, lib.Get(PipSprite(d.flipPattern[i], i == d.flipIndex)),
                         UIKit.ReelCell.FlipPips[i]);

            AddStat(root, "Atk", "micro_atk", UIKit.ReelCell.StatCells[0], d.atk.ToString());
            AddStat(root, "Hp", "micro_hp", UIKit.ReelCell.StatCells[1], d.hp + "/" + d.hpMax);
            AddStat(root, "Def", "micro_def", UIKit.ReelCell.StatCells[2], d.defCurrent.ToString());
            return root;
        }

        // ------------------------------------------------------------ mazzo
        /// Sprite del mazzo in base alle carte rimaste: piu' carte = piu' spesso.
        public static string DeckSprite(int count)
        {
            if (count <= 0) return "deck_stack_0";
            if (count <= 2) return "deck_stack_1";
            if (count <= 5) return "deck_stack_2";
            if (count <= 9) return "deck_stack_3";
            if (count <= 15) return "deck_stack_4";
            return "deck_stack_5";
        }
        public static string DiscardSprite(int count)
        {
            if (count <= 0) return "discard_stack_0";
            if (count <= 4) return "discard_stack_1";
            if (count <= 11) return "discard_stack_2";
            return "discard_stack_3";
        }

        static string CellSprite(Face f, bool cur)
        { return cur ? "flip_cell_current" : (f == Face.Front ? "flip_cell_front" : "flip_cell_back"); }
        static string PipSprite(Face f, bool cur)
        { return cur ? "reel_pip_current" : (f == Face.Front ? "reel_pip_front" : "reel_pip_back"); }

        // ---------------------------------------------------------- helpers
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

        Image AddImage(RectTransform parent, string n, Sprite s, RectInt r, bool allowNull = false)
        {
            if (s == null && !allowNull) return null;
            var rt = NewRect(n, parent, r.width * scale, r.height * scale);
            rt.anchoredPosition = new Vector2(r.x * scale, -r.y * scale);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = s;
            img.raycastTarget = false;
            img.preserveAspect = true;
            if (s == null) img.color = new Color(0, 0, 0, 0);
            return img;
        }

        void AddStat(RectTransform parent, string n, string sprite, RectInt r, string value)
        {
            var img = AddImage(parent, n, lib.Get(sprite), r);
            if (img == null) return;
            img.preserveAspect = false;
            var t = new GameObject("Value", typeof(RectTransform)).GetComponent<RectTransform>();
            t.SetParent(img.rectTransform, false);
            t.anchorMin = Vector2.zero; t.anchorMax = Vector2.one;
            t.offsetMin = new Vector2(11 * scale, 0); t.offsetMax = Vector2.zero;
            var txt = t.gameObject.AddComponent<Text>();
            txt.text = value; txt.alignment = TextAnchor.MiddleCenter; txt.raycastTarget = false;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 6 * scale;
            // sostituire con TextMeshProUGUI + font pixel per la resa finale
        }
    }
}
