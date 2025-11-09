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
    [HideInInspector] public CardInstance instance { get; private set; }

    Button btn;

    [Header("Card Size (for Layout)")]
    public Vector2 preferredSize = new Vector2(260, 160);

    [SerializeField] private Text hintText; // opzionale; se non assegnato, fa fallback al log
    private int _lastHp = int.MinValue, _lastDmg = int.MinValue, _lastBlk = int.MinValue;



    void Awake()
    {
        btn = GetComponent<Button>();
        var bg = GetComponent<Image>();
        var le = GetComponent<LayoutElement>();

        if (btn == null) btn = gameObject.AddComponent<Button>();
        if (bg == null) bg = gameObject.AddComponent<Image>();
        if (btn.targetGraphic == null) btn.targetGraphic = bg;
        if (le == null) le = gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = preferredSize.x;
        le.preferredHeight = preferredSize.y;

        if (autoBindByName) TryAutoBindTexts();

        // Preview immediato leggendo l'inline quando la scena monta
        PreviewFromInlineIfNoInstance();
    }

    // =========================================================
    // PREVIEW: mostra i dati della CardDefinitionInline se non c' ancora un'istanza runtime
    // =========================================================
    void PreviewFromInlineIfNoInstance()
    {
        if (instance != null) return; // runtime already wired

        var inline = GetComponent<CardDefinitionInline>();
        if (inline == null) return;

        // costruiamo la spec temporanea e visualizziamo
        var def = inline.BuildSpec(); // <--- era BuildRuntimeDefinition()

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
                "      +PA(2 retro same) " + def.backBonusPAIfTwoRetroSameFaction;
        }
    }

    // =========================================================
    // Autowire in base ai nomi figli (facoltativo)
    // =========================================================
    void TryAutoBindTexts()
    {
        Text Find(string n)
        {
            var t = transform.Find(n);
            return t != null ? t.GetComponent<Text>() : null;
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

        highlight.effectColor = Color.yellow;
        highlight.effectDistance = new Vector2(5, 5);
        highlight.useGraphicAlpha = true;
        highlight.enabled = on;
    }

    public void Refresh()
    {
        // instance pu essere null; def  una struct quindi NON si confronta con null
        if (instance == null) return;

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
                "      +PA(2 retro same) " + def.backBonusPAIfTwoRetroSameFaction;
        }

        int newHp = instance.health;
        int newDmg = instance.def.frontDamage;
        int newBlk = instance.def.frontBlockValue;

        if (_lastHp != int.MinValue && _lastHp != newHp) Hint($"HP: {_lastHp} : {newHp}");
        if (_lastDmg != int.MinValue && _lastDmg != newDmg) Hint($"DMG: {_lastDmg} : {newDmg}");
        if (_lastBlk != int.MinValue && _lastBlk != newBlk) Hint($"BLK: {_lastBlk} : {newBlk}");

        _lastHp = newHp; _lastDmg = newDmg; _lastBlk = newBlk;
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

    public void Hint(string msg, float seconds = 1.2f)
    {
        if (hintText == null) { GameManagerInteractive.Log?.Invoke("[Card] " + msg); return; }
        StopAllCoroutines();
        StartCoroutine(FlashHint(msg, seconds));
    }

    private System.Collections.IEnumerator FlashHint(string msg, float seconds)
    {
        hintText.gameObject.SetActive(true);
        hintText.text = msg;
        yield return new WaitForSeconds(seconds);
        hintText.gameObject.SetActive(false);
    }
}
