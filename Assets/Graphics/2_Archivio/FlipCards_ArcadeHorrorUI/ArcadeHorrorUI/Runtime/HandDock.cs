// AUTO-GENERATO - FlipCards Arcade Horror UI Kit
// Mano con due viste: abbassata (solo linguette) e in primo piano (carte intere).
using UnityEngine;
using UnityEngine.UI;

namespace FlipCards.UI
{
    public class HandDock : MonoBehaviour
    {
        public enum State { Lowered, Raised }

        public SpriteLibrary lib;
        public CardBuilder builder;
        public RectTransform root;          // figlio a schermo intero del canvas
        public Image dimOverlay;            // overlay_dim
        public Image dockImage;             // hand_dock_low / hand_dock_raised
        public int scale = UIKit.Scale;
        public int arcHeight = 10;          // ventaglio: alzata in px al centro (niente rotazione,
                                            // cosi' i pixel restano allineati alla griglia)
        public State state = State.Lowered;

        public void SetState(State s)
        {
            state = s;
            bool raised = s == State.Raised;
            if (dimOverlay) dimOverlay.enabled = raised;
            if (dockImage)
            {
                dockImage.sprite = lib.Get(raised ? "hand_dock_raised" : "hand_dock_low");
                var r = raised ? UIKit.BoardLayout.HandDockRaised : UIKit.BoardLayout.HandDockLow;
                var rt = dockImage.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
                rt.sizeDelta = new Vector2(r.width * scale, r.height * scale);
                rt.anchoredPosition = new Vector2(r.x * scale, -r.y * scale);
            }
            Rebuild();
        }

        public void Toggle() { SetState(state == State.Lowered ? State.Raised : State.Lowered); }

        /// hand = le carte in mano, gia' popolate altrove
        public CardView[] hand = new CardView[0];

        public void Rebuild()
        {
            for (int i = root.childCount - 1; i >= 0; i--)
                if (root.GetChild(i).name.StartsWith("HandItem")) DestroyImmediate(root.GetChild(i).gameObject);

            if (state == State.Lowered)
            {
                var slots = UIKit.BoardLayout.HandTabSlots;
                for (int i = 0; i < hand.Length && i < slots.Length; i++)
                {
                    var go = new GameObject("HandItem" + i, typeof(RectTransform), typeof(Image));
                    var rt = (RectTransform)go.transform;
                    rt.SetParent(root, false);
                    rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                    rt.pivot = new Vector2(0, 1);
                    rt.sizeDelta = new Vector2(UIKit.Card.x * scale, UIKit.TabHeight * scale);
                    rt.anchoredPosition = new Vector2(slots[i].x * scale, -slots[i].y * scale);
                    go.GetComponent<Image>().sprite = lib.Get("hand_tab_" + hand[i].faction);
                }
            }
            else
            {
                var dock = UIKit.BoardLayout.HandDockRaised;
                float step = Mathf.Min(UIKit.Card.x + 8, (dock.width - 80f) / Mathf.Max(1, hand.Length));
                float x0 = dock.x + (dock.width - step * (hand.Length - 1) - UIKit.Card.x) * 0.5f;
                for (int i = 0; i < hand.Length; i++)
                {
                    var card = builder.BuildCard(hand[i], root);
                    card.name = "HandItem" + i;
                    float t = hand.Length > 1 ? (i / (float)(hand.Length - 1)) * 2f - 1f : 0f;
                    int arc = Mathf.RoundToInt((1f - t * t) * arcHeight);
                    card.anchoredPosition = new Vector2(Mathf.Round(x0 + step * i) * scale,
                                                        -(dock.y + 30 - arc) * scale);
                }
            }
        }
    }
}
