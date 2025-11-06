using UnityEngine;
using UnityEngine.UI;
#if TMP_PRESENT
using TMPro;
#endif
using System.Collections;

public class CardView : MonoBehaviour
{
    // ---- TEXT REFERENCES (support both TMP and Legacy) ----
    // If you use TextMeshPro, assign these:
#if TMP_PRESENT
    public TMP_Text nameTMP;
    public TMP_Text sideTMP;
    public TMP_Text hpTMP;
#endif
    // If you use Legacy UI.Text, assign these:
    public Text nameText;
    public Text sideText;
    public Text hpText;

    // Optional highlight overlay (Image enabled/disabled)
    public Image highlight;

    // Runtime wiring from GameManagerInteractive
    [HideInInspector] public GameManagerInteractive gm;
    [HideInInspector] public PlayerState owner;
    [HideInInspector] public CardInstance instance;

    Button btn;

    // Default card sizing (used for LayoutElement)
    [Header("Card Size")]
    public Vector2 preferredSize = new Vector2(160f, 220f);

    void Awake()
    {
        // Ensure Button exists so we can click-select even in a lone scene card
        btn = GetComponent<Button>();
        if (btn == null) btn = gameObject.AddComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnClicked);

        // Ensure a background Image exists (target graphic for Button)
        var bg = GetComponent<Image>();
        if (bg == null) bg = gameObject.AddComponent<Image>();
        if (btn.targetGraphic == null) btn.targetGraphic = bg;

        // Ensure LayoutElement exists for size control under LayoutGroups
        var le = GetComponent<LayoutElement>();
        if (le == null) le = gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = preferredSize.x;
        le.preferredHeight = preferredSize.y;

        EnsureRuntimeUI();
        // Safe default: hide highlight
        SetHighlight(false);
    }

    void EnsureRuntimeUI()
    {
        // 1) Overlay di highlight se assente (figlio a pieno schermo semitrasparente)
        if (highlight == null)
        {
            var hlGO = new GameObject("Highlight", typeof(RectTransform), typeof(Image));
            hlGO.transform.SetParent(transform, false);
            var rt = hlGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = hlGO.GetComponent<Image>();
            img.raycastTarget = false;
            img.color = new Color(1f, 0.9f, 0.2f, 0.25f);
            highlight = img;
        }

        // 2) Layout e testi base se non assegnati
#if TMP_PRESENT
    bool hasText = (nameTMP != null) || (nameText != null);
#else
        bool hasText = (nameText != null);
#endif
        if (!hasText)
        {
            // Contenitore per testi
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(transform, false);
            var crt = content.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0, 0);
            crt.anchorMax = new Vector2(1, 1);
            crt.offsetMin = new Vector2(8, 8);
            crt.offsetMax = new Vector2(-8, -8);

            // Vertical layout
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 4;

            // Helper per creare un Text legacy
            Text Make(string n, int size, FontStyle style = FontStyle.Normal)
            {
                var go = new GameObject(n, typeof(RectTransform), typeof(Text));
                go.transform.SetParent(content.transform, false);
                var t = go.GetComponent<Text>();
                t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                t.fontSize = size;
                t.fontStyle = style;
                t.alignment = TextAnchor.UpperLeft;
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.verticalOverflow = VerticalWrapMode.Truncate;
                t.raycastTarget = false;
                return t;
            }

            nameText = Make("Name", 18, FontStyle.Bold);
            sideText = Make("Side", 14);
            hpText = Make("HP", 14);
        }
    }

    // Called by GameManager after creating CardInstance
    public void Init(GameManagerInteractive gm, PlayerState owner, CardInstance instance)
    {
        this.gm = gm;
        this.owner = owner;
        this.instance = instance;

        // Rebind click (so it routes to the manager when present)
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnClicked);

        Refresh();
    }

    void OnClicked()
    {
        // If a manager exists, send selection there.
        if (gm != null)
        {
            gm.OnCardClicked(this);
            return;
        }

        // Standalone preview mode (no manager):
        // Toggle highlight and just log the state so you can test the prefab in-scene.
        SetHighlight(highlight == null ? false : !highlight.enabled);
        Debug.Log("[CardView] Click: " + GetDisplayName());
    }

    public void Refresh()
    {
        // Visual background tint if destroyed
        var bg = GetComponent<Image>();
        if (bg != null && instance != null)
            bg.color = instance.alive ? Color.white : new Color(1f, 0.8f, 0.8f, 0.6f);

        // Compose strings
        string title = instance != null
            ? instance.def.cardName + " (" + instance.def.faction + ")"
            : gameObject.name;

        string sideStr = instance != null
            ? "Side: " + instance.side + (instance.def.frontType == FrontType.Attacco ? " [Atk]" :
                                         instance.def.frontType == FrontType.Blocco ? " [Blk]" : "")
            : "Side: -";

        string hpStr = instance != null ? "HP: " + instance.health : "HP: -";

        // Write to TMP if present/assigned, else to Legacy Text
#if TMP_PRESENT
        if (nameTMP != null) nameTMP.text = title;
        if (sideTMP != null) sideTMP.text = sideStr;
        if (hpTMP   != null) hpTMP.text   = hpStr;
#endif

        if (nameText != null) nameText.text = title;
        if (sideText != null) sideText.text = sideStr;
        if (hpText != null) hpText.text = hpStr;
    }

    public void SetHighlight(bool on)
    {
        if (highlight != null) highlight.enabled = on;
    }

    public void Blink()
    {
        StartCoroutine(BlinkRoutine());
    }

    IEnumerator BlinkRoutine()
    {
        SetHighlight(true);
        yield return new WaitForSeconds(0.15f);
        SetHighlight(false);
    }

    string GetDisplayName()
    {
        if (instance == null || instance.def == null) return gameObject.name;
        return instance.def.cardName + " [" + instance.def.faction + "]";
    }
}
