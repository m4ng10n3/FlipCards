using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CardView : MonoBehaviour
{
    // ---- LEGACY UI TEXT REFERENCES ----
    [Header("Legacy Text (assign from prefab or will be auto-created)")]
    public Text nameText;
    public Text factionText;
    public Text sideText;
    public Text hpText;

    [Header("Legacy Text - Details")]
    public Text frontTypeText;
    public Text frontDamageText;
    public Text frontBlockText;
    public Text backBonusesText;

    // ---- RUNTIME WIRING ----
    [HideInInspector] public GameManagerInteractive gm;
    [HideInInspector] public PlayerState owner;
    [HideInInspector] public CardInstance instance;

    Button btn;

    // Suggested size for layout groups
    [Header("Card Size")]
    public Vector2 preferredSize = new Vector2(160f, 220f);

    void Awake()
    {
        // Ensure Button exists to handle clicks
        btn = GetComponent<Button>();
        if (btn == null) btn = gameObject.AddComponent<Button>();

        // Ensure background Image used by Button
        var bg = GetComponent<Image>();
        if (bg == null) bg = gameObject.AddComponent<Image>();
        if (btn.targetGraphic == null) btn.targetGraphic = bg;

        // Ensure LayoutElement for layout groups
        var le = GetComponent<LayoutElement>();
        if (le == null) le = gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = preferredSize.x;
        le.preferredHeight = preferredSize.y;

        // Create missing Text boxes if not assigned from prefab
        EnsureTextBoxes();
    }

    // Create basic Text components as children if missing
    void EnsureTextBoxes()
    {
        Text Make(string label, int fontSize = 14, FontStyle style = FontStyle.Normal, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, 18f);

            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.alignment = anchor;
            t.color = Color.black;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }

        // Rows: Name / Faction
        if (nameText == null) nameText = Make("Name", 18, FontStyle.Bold);
        if (factionText == null) factionText = Make("Faction", 14);

        // Rows: Side / HP
        if (sideText == null) sideText = Make("Side", 14);
        if (hpText == null) hpText = Make("HP", 14);

        // Rows: Front details
        if (frontTypeText == null) frontTypeText = Make("FrontType", 14);
        if (frontDamageText == null) frontDamageText = Make("FrontDamage", 14);
        if (frontBlockText == null) frontBlockText = Make("FrontBlock", 14);

        // Row: Back bonuses (multiline)
        if (backBonusesText == null)
        {
            backBonusesText = Make("BackBonuses", 12);
            var rt = backBonusesText.rectTransform;
            rt.sizeDelta = new Vector2(0f, 40f);
        }

        // Simple vertical stacking from top
        float y = -6f;
        Text[] all = new Text[]
        {
            nameText, factionText, sideText, hpText,
            frontTypeText, frontDamageText, frontBlockText, backBonusesText
        };

        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null) continue;
            var rt = t.rectTransform;
            rt.anchoredPosition = new Vector2(0f, y);
            y -= rt.sizeDelta.y + 2f;
        }
    }

    // Called by GameManager when creating/assigning the instance
    public void Init(GameManagerInteractive gm, PlayerState owner, CardInstance instance)
    {
        this.gm = gm;
        this.owner = owner;
        this.instance = instance;

        // Route clicks to GameManager
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnClicked);

        Refresh();
    }

    void OnClicked()
    {
        if (gm != null)
        {
            gm.OnCardClicked(this);
            return;
        }
        // Standalone preview mode: toggle highlight
        SetHighlight(highlight == null ? false : !highlight.enabled);
    }

    // Simple highlight feedback (optional)
    Outline highlight;
    public void SetHighlight(bool on)
    {
        if (highlight == null)
            highlight = gameObject.GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();

        highlight.effectDistance = on ? new Vector2(4f, -4f) : Vector2.zero;
        highlight.enabled = on;
    }

    // Update all text fields from CardDefinition and runtime state
    public void Refresh()
    {
        if (instance == null || instance.def == null) return;
        var def = instance.def;

        if (nameText != null) nameText.text = def.cardName;
        if (factionText != null) factionText.text = "Faction: " + def.faction;
        if (sideText != null) sideText.text = "Side: " + instance.side;
        if (hpText != null) hpText.text = "HP: " + instance.health + "/" + def.maxHealth;

        if (frontTypeText != null) frontTypeText.text = "Front: " + def.frontType;
        if (frontDamageText != null) frontDamageText.text = "Front Dmg: " + def.frontDamage;
        if (frontBlockText != null) frontBlockText.text = "Front Block: " + def.frontBlockValue;

        if (backBonusesText != null)
        {
            backBonusesText.text =
                "Back: +DMG same " + def.backDamageBonusSameFaction + "\n" +
                "      +BLK same " + def.backBlockBonusSameFaction + "\n" +
                "      +AP if 2 Back same: " + def.backBonusPAIfTwoRetroSameFaction;
        }

        // Simple background color by side
        var bg = GetComponent<Image>();
        if (bg != null)
        {
            bg.color = (instance.side == Side.Fronte)
                ? new Color(0.90f, 0.98f, 1f, 1f)   // front
                : new Color(1f, 0.95f, 0.90f, 1f); // back
        }
    }

    // Small visual feedback
    public void Blink()
    {
        StartCoroutine(BlinkRoutine());
    }

    IEnumerator BlinkRoutine()
    {
        var img = GetComponent<Image>();
        if (img == null) yield break;

        Color c = img.color;
        img.color = Color.yellow;
        yield return new WaitForSeconds(0.08f);
        img.color = c;
    }
}
