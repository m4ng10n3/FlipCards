using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CardView : MonoBehaviour
{
    [SerializeField] private Sprite frontSprite;      // assegna nel prefab
    [SerializeField] private Sprite backSprite;       // assegna nel prefab
    private Image img;


    [Header("Legacy UI Text (assign in prefab)")]
    public Text nameText;
    public Text factionText;
    public Text sideText;
    public Text hpText;
    public Text frontTypeText;
    public Text frontDamageText;
    public Text frontBlockText;
    public Text backBonusesText;

    [Header("Runtime wiring")]
    [HideInInspector] public GameManager gm;
    [HideInInspector] public PlayerState owner;
    [HideInInspector] public CardInstance instance { get; private set; }

    [Header("Card Size (for Layout)")]
    public Vector2 preferredSize = new Vector2(260, 160);

    [SerializeField] private Text hintText; // opzionale; se assente, fallback su Logger

    Button btn;
    Outline highlight;

    // tracking solo HP, niente più hint automatici su DMG/BLK
    private int _lastHp = int.MinValue;

    // handler eventi
    private EventBus.Handler _evtHandler;

    void Awake()
    {
        btn = GetComponent<Button>();
        var bg = GetComponent<Image>();
        var le = GetComponent<LayoutElement>();

        if (btn == null) btn = gameObject.AddComponent<Button>();
        if (bg == null) bg = gameObject.AddComponent<Image>(); // <-- QUI: usa Image UI
        if (btn.targetGraphic == null) btn.targetGraphic = bg;
        if (le == null) le = gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = preferredSize.x;
        le.preferredHeight = preferredSize.y;

        img = bg;                    // tieni un riferimento
        img.preserveAspect = true;   // utile per carte

        // sprite iniziale (prima dell'Init)
        if (frontSprite != null) img.sprite = frontSprite;

        PreviewFromInlineIfNoInstance();
        if (hintText != null) hintText.gameObject.SetActive(false);
    }


    /// <summary>
    /// Anteprima: mostra i dati statici se la carta è presente in scena ma senza istanza runtime.
    /// </summary>
    void PreviewFromInlineIfNoInstance()
    {
        if (instance != null) return;

        var inline = GetComponent<CardDefinition>();
        if (inline == null) return;

        var def = inline.BuildSpec();

        if (nameText != null) nameText.text = def.cardName;
        if (factionText != null) factionText.text = def.faction.ToString();
        if (sideText != null) sideText.text = "Side";
        if (hpText != null) hpText.text = def.maxHealth + "/" + def.maxHealth;
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

        // Sottoscrizione agli eventi: solo l'Hint reagisce
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

    /// <summary>
    /// Aggiorna tutti i testi statici della carta (restano sempre visibili).
    /// Non mostra hint.
    /// </summary>
    public void Refresh()
    {
        if (instance == null) return;

        var def = instance.def;

        if (nameText != null) nameText.text = def.cardName;
        if (factionText != null) factionText.text = def.faction.ToString();
        if (sideText != null) sideText.text = instance.side.ToString();
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

        _lastHp = instance.health; // tracking interno per eventuali usi futuri
        RefreshSpriteBySide();

    }

    private void RefreshSpriteBySide()
    {
        if (img == null || instance == null) return;
        img.sprite = (instance.side == Side.Fronte) ? frontSprite : backSprite;
    }



    // ======== Event handling: SOLO Hint + aggiornamento HP ========

    void OnGameEvent(GameEventType t, EventContext ctx)
    {
        if (instance == null) return;

        switch (t)
        {
            case GameEventType.AttackResolved:
                if (ctx.target == instance && ctx.amount > 0)
                {
                    ShowHint($"-{ctx.amount}HP");   // <--- persiste finché non arriva TurnEnd
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
                // a fine turno l'hint viene nascosto
                HideHint();
                break;

            case GameEventType.Info:
                // HINT "diretto" per questa carta: phase = "HINT:messaggio"
                if (ctx.source == instance && !string.IsNullOrEmpty(ctx.phase) && ctx.phase.StartsWith("HINT:"))
                {
                    ShowHint(ctx.phase.Substring("HINT:".Length).Trim());
                    // niente Blink obbligatorio qui: lascialo a discrezione di chi invia l’hint
                }
                break;

            case GameEventType.TurnStart:
                // Nuovo turno: rimuovi gli Hint del turno precedente prima che arrivino quelli nuovi
                HideHint();
                break;
            case GameEventType.Flip:
                if (ctx.source == instance || ctx.target == instance)
                {
                    RefreshSpriteBySide();
                    Blink(); // opzionale
                }
                break;

        }
    }

    private void UpdateHpOnly()
    {
        if (instance == null) return;
        if (hpText != null)
            hpText.text = instance.health + "/" + instance.def.maxHealth;
        _lastHp = instance.health;
    }

    // Small visual feedback
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

        if (string.IsNullOrEmpty(hintText.text))
            hintText.text = msg;
        else
            hintText.text += "\n" + msg;
    }

    public void HideHint()
    {
        if (hintText != null)
        {
            hintText.text = string.Empty;   // svuota coda messaggi
            hintText.gameObject.SetActive(false);
        }
    }

}
