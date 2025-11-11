using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(RectTransform))]
public class CardView : MonoBehaviour
{
    [Header("Image Handling")]
    [Tooltip("Sprite del retro (assegnare come Source Image in Inspector)")]
    [SerializeField] private Sprite backImage;
    [SerializeField] private Image artworkMonster;

    private Image img;
    private Sprite frontImage;

    [Header("Legacy UI Text (assign in prefab)")]
    public Text nameText;
    public Text factionText;
    public Text sideText;
    public Text hpText;
    public Text AttackPwrText;
    public Text BlockPwrText;

    [Header("Runtime wiring")]
    [HideInInspector] public GameManager gm;
    [HideInInspector] public PlayerState owner;
    [HideInInspector] public CardInstance instance { get; private set; }

    [SerializeField] private Text hintText;

    private Button btn;
    private Outline highlight;
    private int _lastHp = int.MinValue;
    private EventBus.Handler _evtHandler;

    void Awake()
    {
        btn = GetComponent<Button>();
        var bg = GetComponent<Image>();

        if (btn == null) btn = gameObject.AddComponent<Button>();
        if (bg == null) bg = gameObject.AddComponent<Image>();
        if (btn.targetGraphic == null) btn.targetGraphic = bg;

        img = bg;
        img.preserveAspect = false; // rispetta le proporzioni dentro al RectTransform

        // il fronte è l'immagine impostata nel componente Image (Source Image)
        frontImage = img.sprite;

        // mostra il fronte di default
        if (frontImage != null) img.sprite = frontImage;

        PreviewFromInlineIfNoInstance();
        if (hintText != null) hintText.gameObject.SetActive(false);
    }

    void PreviewFromInlineIfNoInstance()
    {
        if (instance != null) return;

        var inline = GetComponent<CardDefinition>();
        if (inline == null) return;

        var def = inline.BuildSpec();

        if (nameText != null) nameText.text = def.cardName;
        if (factionText != null) factionText.text = def.faction.ToString();
        if (sideText != null) sideText.text = "Side";
        if (hpText != null) hpText.text = def.maxHealth + "";
        if (AttackPwrText != null) AttackPwrText.text = "" + def.frontDamage;
        if (BlockPwrText != null) BlockPwrText.text = "" + def.frontBlockValue;
    }

    public void Init(GameManager gm, PlayerState owner, CardInstance instance)
    {
        this.gm = gm;
        this.owner = owner;
        this.instance = instance;

        if (btn == null) btn = GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnClicked);

        Refresh();
        if (hintText != null) hintText.gameObject.SetActive(false);

        _evtHandler = OnGameEvent;
        EventBus.Subscribe(GameEventType.AttackResolved, _evtHandler);
        EventBus.Subscribe(GameEventType.Flip, _evtHandler);
        EventBus.Subscribe(GameEventType.AttackDeclared, _evtHandler);
        EventBus.Subscribe(GameEventType.TurnEnd, _evtHandler);
        EventBus.Subscribe(GameEventType.Info, _evtHandler);
        EventBus.Subscribe(GameEventType.TurnStart, _evtHandler);
    }

    void OnDestroy()
    {
        if (_evtHandler != null)
        {
            EventBus.Unsubscribe(GameEventType.AttackResolved, _evtHandler);
            EventBus.Unsubscribe(GameEventType.Flip, _evtHandler);
            EventBus.Unsubscribe(GameEventType.AttackDeclared, _evtHandler);
            EventBus.Unsubscribe(GameEventType.TurnEnd, _evtHandler);
            EventBus.Unsubscribe(GameEventType.Info, _evtHandler);
            EventBus.Unsubscribe(GameEventType.TurnStart, _evtHandler);
            _evtHandler = null;
        }
    }

    void OnClicked()
    {
        if (gm != null) { gm.OnCardClicked(this); return; }
        SetHighlight(highlight == null ? false : !highlight.enabled);
    }

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
        if (instance == null) return;

        var def = instance.def;

        if (nameText != null) nameText.text = def.cardName;
        if (factionText != null) factionText.text = def.faction.ToString();
        if (sideText != null) sideText.text = instance.side.ToString();
        if (hpText != null) hpText.text = instance.health + "";
        if (AttackPwrText != null) AttackPwrText.text = "" + def.frontDamage;
        if (BlockPwrText != null) BlockPwrText.text = "" + def.frontBlockValue;

        _lastHp = instance.health;

        FlipSide();
    }

    private void FlipSide()
    {
        bool isFront = (instance != null && instance.side == Side.Fronte);

        // Cambia solo lo sprite (niente SetNativeSize / scale)
        var newSprite = isFront ? frontImage : (backImage != null ? backImage : frontImage);
        img.type = Image.Type.Simple; // per sicurezza
        img.preserveAspect = false;   // il RectTransform decide; cambia a true se vuoi letterboxing
        img.sprite = newSprite;

        // Mostra/nascondi i testi a seconda del lato

        if (nameText) nameText.enabled = isFront;
        if (hpText) hpText.enabled = isFront;
        if (AttackPwrText) AttackPwrText.enabled = isFront;
        if (BlockPwrText) BlockPwrText.enabled = isFront;
        if (artworkMonster) artworkMonster.enabled = isFront;
        if (hintText) hintText.enabled = isFront;
    }


    void OnGameEvent(GameEventType t, EventContext ctx)
    {
        if (instance == null) return;

        switch (t)
        {
            case GameEventType.AttackResolved:
                if (ctx.target == instance && ctx.amount > 0)
                {
                    ShowHint($"-{ctx.amount}HP");
                    UpdateHpOnly();
                    Blink();
                }
                if (ctx.source == instance && ctx.amount > 0)
                {
                    ShowHint($"Dealt {ctx.amount}");
                }
                break;

            case GameEventType.AttackDeclared:
                if (ctx.source == instance) ShowHint("Attack!");
                else if (ctx.target == instance) ShowHint("Under attack!");
                break;

            case GameEventType.TurnEnd:
                HideHint();
                break;

            case GameEventType.Info:
                if (ctx.source == instance && !string.IsNullOrEmpty(ctx.phase) && ctx.phase.StartsWith("HINT:"))
                    ShowHint(ctx.phase.Substring("HINT:".Length).Trim());
                break;

            case GameEventType.TurnStart:
                HideHint();
                break;

            case GameEventType.Flip:
                if (ctx.source == instance || ctx.target == instance)
                {
                    FlipSide();
                    Blink();
                }
                break;
        }
    }

    private void UpdateHpOnly()
    {
        if (instance == null) return;
        if (hpText != null)
            hpText.text = instance.health + "";
        _lastHp = instance.health;
    }

    public void Blink() { StartCoroutine(BlinkRoutine()); }
    IEnumerator BlinkRoutine()
    {
        if (img == null) yield break;
        var c = img.color;
        img.color = Color.yellow;
        yield return new WaitForSeconds(0.08f);
        img.color = c;
    }

    public void ShowHint(string msg)
    {
        if (hintText == null)
        {
            Logger.Info("[Card] " + msg);
            return;
        }

        hintText.gameObject.SetActive(true);
        hintText.text = string.IsNullOrEmpty(hintText.text) ? msg : hintText.text + "\n" + msg;
    }

    public void HideHint()
    {
        if (hintText != null)
        {
            hintText.text = string.Empty;
            hintText.gameObject.SetActive(false);
        }
    }
}
