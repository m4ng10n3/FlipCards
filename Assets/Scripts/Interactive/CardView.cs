using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CardView : MonoBehaviour
{
    [Header("Legacy UI Text (assign in prefab)")]
    public Text nameText;
    public Text factionText;
    public Text sideText;
    public Text hpText;
    public Text frontTypeText;
    public Text frontDamageText;
    public Text frontBlockText;
    public Text backBonusesText;

    [Header("Auto-bind by child names (optional)")]
    public bool autoBindByName = true;

    [Header("Runtime wiring")]
    [HideInInspector] public GameManagerInteractive gm;
    [HideInInspector] public PlayerState owner;
    [HideInInspector] public CardInstance instance;

    Button btn;

    [Header("Card Size (for Layout)")]
    public Vector2 preferredSize = new Vector2(160f, 220f);

    void Awake()
    {
        btn = GetComponent<Button>();
        if (btn == null) btn = gameObject.AddComponent<Button>();

        var bg = GetComponent<Image>();
        if (bg == null) bg = gameObject.AddComponent<Image>();
        if (btn.targetGraphic == null) btn.targetGraphic = bg;

        var le = GetComponent<LayoutElement>();
        if (le == null) le = gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = preferredSize.x;
        le.preferredHeight = preferredSize.y;

        if (autoBindByName) TryAutoBindTexts();

        // NEW: preview immediato leggendo CardDefinitionInline quando la scena monta
        PreviewFromInlineIfNoInstance();
    }

    // =========================================================
    // PREVIEW: mostra i dati della CardDefinitionInline se non c'e' ancora un'istanza runtime
    // =========================================================
    void PreviewFromInlineIfNoInstance()
    {
        if (instance != null) return; // runtime already wired

        var inline = GetComponent<CardDefinitionInline>();
        if (inline == null) return;

        // costruiamo una def temporanea e visualizziamo
        var def = inline.BuildRuntimeDefinition();
        if (def == null) return;

        // lato e hp preview (non esiste ancora lo stato runtime)
        string sidePreview = "Side";
        int hpCur = def.maxHealth;

        if (nameText != null) nameText.text = def.cardName;
        if (factionText != null) factionText.text = "" + def.faction.ToString();
        if (sideText != null) sideText.text = sidePreview;
        if (hpText != null) hpText.text = hpCur + "/" + def.maxHealth;
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
    }

    // =========================================================
    // AUTOBIND
    // =========================================================
    void TryAutoBindTexts()
    {
        Text Find(string childName)
        {
            var t = transform.Find(childName);
            if (t != null) return t.GetComponent<Text>();
            var texts = GetComponentsInChildren<Text>(true);
            foreach (var tx in texts)
                if (tx.gameObject.name == childName) return tx;
            return null;
        }

        if (nameText == null) nameText = Find("Name");
        if (factionText == null) factionText = Find("Faction");
        if (sideText == null) sideText = Find("Side");
        if (hpText == null) hpText = Find("HP");
        if (frontTypeText == null) frontTypeText = Find("FrontType");
        if (frontDamageText == null) frontDamageText = Find("FrontDamage");
        if (frontBlockText == null) frontBlockText = Find("FrontBlock");
        if (backBonusesText == null) backBonusesText = Find("BackBonuses");
    }

    // =========================================================
    // RUNTIME WIRING
    // =========================================================
    public void Init(GameManagerInteractive gm, PlayerState owner, CardInstance instance)
    {
        this.gm = gm;
        this.owner = owner;
        this.instance = instance;

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnClicked);

        Refresh();
    }

    void OnClicked()
    {
        if (gm != null) { gm.OnCardClicked(this); return; }
        SetHighlight(highlight == null ? false : !highlight.enabled);
    }

    Outline highlight;
    public void SetHighlight(bool on)
    {
        if (highlight == null)
            highlight = gameObject.GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();
        highlight.effectDistance = on ? new Vector2(4f, -4f) : Vector2.zero;
        highlight.enabled = on;
    }

    // =========================================================
    // REFRESH RUNTIME
    // =========================================================
    public void Refresh()
    {
        if (instance == null || instance.def == null) return;
        var def = instance.def;

        if (nameText != null) nameText.text = def.cardName;
        if (factionText != null) factionText.text = "" + def.faction.ToString();
        if (sideText != null) sideText.text = "" + instance.side;
        if (hpText != null) hpText.text = instance.health + "/" + def.maxHealth;
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

        var bg = GetComponent<Image>();
        if (bg != null)
        {
            bg.color = (instance.side == Side.Fronte)
                ? new Color(0.90f, 0.98f, 1f, 1f)
                : new Color(1f, 0.95f, 0.90f, 1f);
        }
    }

    // Small visual feedback
    public void Blink() { StartCoroutine(BlinkRoutine()); }
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
