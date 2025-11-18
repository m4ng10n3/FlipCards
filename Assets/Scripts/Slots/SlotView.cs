using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SlotView : MonoBehaviour
{
    [Header("Legacy UI Text (assign in prefab)")]
    public Text nameText;
    public Text factionText;
    public Text hpText;

    [Header("Runtime wiring")]
    [HideInInspector] public GameManager gm;
    [HideInInspector] public PlayerState owner;
    [HideInInspector] public SlotInstance instance { get; private set; }

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
        if (bg == null) bg = gameObject.AddComponent<Image>();
        if (btn.targetGraphic == null) btn.targetGraphic = bg;
        if (le == null) le = gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = preferredSize.x;
        le.preferredHeight = preferredSize.y;

        // Preview editor-only: se non c'è istanza runtime, mostra i dati dell'inline
        PreviewFromInlineIfNoInstance();
        if (hintText != null) hintText.gameObject.SetActive(false);
    }

    /// <summary>
    /// Anteprima: mostra i dati statici se la carta è presente in scena ma senza istanza runtime.
    /// </summary>
    void PreviewFromInlineIfNoInstance()
    {
        if (instance != null) return;

        var inline = GetComponent<SlotDefinition>();
        if (inline == null) return;

        var def = inline.BuildSpec();

        if (nameText != null) nameText.text = def.SlotName;
        if (factionText != null) factionText.text = def.faction.ToString();
        if (hpText != null) hpText.text = def.maxHealth + "/" + def.maxHealth;
    }


    public void Init(GameManager gm, PlayerState owner, SlotInstance instance)
    {
        this.gm = gm;
        this.owner = owner;
        this.instance = instance;

        if (btn == null) btn = GetComponent<Button>();
        btn.onClick.RemoveAllListeners();

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

        if (nameText != null) nameText.text = def.SlotName;
        if (factionText != null) factionText.text = def.faction.ToString();
        if (hpText != null) hpText.text = instance.health + "/" + def.maxHealth;

        _lastHp = instance.health;
    }


    // ======== Event handling: SOLO Hint + aggiornamento HP ========

    void OnGameEvent(GameEventType t, EventContext ctx)
    {
        if (instance == null) return;

        switch (t)
        {
            case GameEventType.AttackResolved:
                
                // Difensore: mostra HP persi
                if (ctx.target == instance && ctx.amount > 0)
                {
                    //ClearHint();
                    ShowHint($"-{ctx.amount}HP");
                    UpdateHpOnly();
                    Blink();
                }
                // Attaccante: mostra il danno inflitto
                if (ctx.source == instance && ctx.amount > 0)
                {
                    //ClearHint();
                    ShowHint($"Dealt {ctx.amount}");
                }
                break;


            case GameEventType.AttackDeclared:
                if (ctx.source == instance) ShowHint("Attack!");
                else if (ctx.target == instance) ShowHint("Under attack!");
                break;

            case GameEventType.TurnEnd:
                // a fine turno l'hint viene nascosto
                //HideHint();
                //ClearHint();
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
                //HideHint();
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
        var img = GetComponent<Image>();
        if (img == null) yield break;
        Color c = img.color;
        img.color = Color.yellow;
        yield return new WaitForSeconds(0.08f);
        img.color = c;
    }

    public void ShowHint(string msg)
    {
        hintText.gameObject.SetActive(true);

        if (string.IsNullOrEmpty(hintText.text))
            hintText.text = msg;
        else
            hintText.text += "\n" + msg;
    }

    public void ClearHint()
    {
        if (hintText != null)
        {
            hintText.text = string.Empty;   // svuota coda messaggi
        }
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
