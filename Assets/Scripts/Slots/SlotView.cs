using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SlotView : MonoBehaviour
{
    [Header("UI Text")]
    public Text nameText;     // nome slot — sempre visibile
    public Text hpText;       // HP correnti — sempre visibile
    public Text defText;      // DEF corrente (valore effettivo calcolato internamente)

    [SerializeField] private Text hintText; // messaggi contestuali: ATK, DEF, danno ricevuto

    [Header("Runtime wiring")]
    [HideInInspector] public GameManager gm;
    [HideInInspector] public PlayerState owner;
    public SlotInstance instance { get; private set; }

    [Header("Layout")]
    public Vector2 preferredSize = new Vector2(260, 160);

    private int _lastHp = int.MinValue;
    private EventBus.Handler _evtHandler;
    private Button _btn;
    private Outline _highlight;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        _btn = GetComponent<Button>();
        var bg = GetComponent<Image>();
        var le = GetComponent<LayoutElement>();

        if (_btn == null) _btn = gameObject.AddComponent<Button>();
        if (bg   == null) bg   = gameObject.AddComponent<Image>();
        if (_btn.targetGraphic == null) _btn.targetGraphic = bg;
        if (le   == null) le   = gameObject.AddComponent<LayoutElement>();
        le.preferredWidth  = preferredSize.x;
        le.preferredHeight = preferredSize.y;

        PreviewFromInlineIfNoInstance();
        if (hintText != null) hintText.gameObject.SetActive(false);
    }

    void PreviewFromInlineIfNoInstance()
    {
        if (instance != null) return;
        var inline = GetComponent<SlotDefinition>();
        if (inline == null) return;
        var def = inline.BuildSpec();
        if (nameText != null) nameText.text = def.SlotName;
        if (hpText   != null) hpText.text   = $"{def.maxHealth}/{def.maxHealth}";
        if (defText != null) defText.text = $"DEF {def.blockFront}";
    }

    public void Init(GameManager gm, PlayerState owner, SlotInstance instance)
    {
        this.gm       = gm;
        this.owner    = owner;
        this.instance = instance;

        if (_btn == null) _btn = GetComponent<Button>();
        _btn.onClick.RemoveAllListeners();

        Refresh();
        HideHint();

        _evtHandler = OnGameEvent;
        EventBus.Subscribe(GameEventType.AttackDeclared,  _evtHandler);
        EventBus.Subscribe(GameEventType.AttackResolved,  _evtHandler);
        EventBus.Subscribe(GameEventType.TurnStart,       _evtHandler);
        EventBus.Subscribe(GameEventType.TurnEnd,         _evtHandler);
        EventBus.Subscribe(GameEventType.Info,            _evtHandler);
    }

    void OnDestroy()
    {
        if (_evtHandler == null) return;
        EventBus.Unsubscribe(GameEventType.AttackDeclared,  _evtHandler);
        EventBus.Unsubscribe(GameEventType.AttackResolved,  _evtHandler);
        EventBus.Unsubscribe(GameEventType.TurnStart,       _evtHandler);
        EventBus.Unsubscribe(GameEventType.TurnEnd,         _evtHandler);
        EventBus.Unsubscribe(GameEventType.Info,            _evtHandler);
        _evtHandler = null;
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    /// <summary>Aggiorna nome, HP e DEF attivo. Non tocca l'hint.</summary>
    public void Refresh()
    {
        if (instance == null) return;
        var def = instance.def;

        if (nameText     != null) nameText.text = def.SlotName;
        if (hpText       != null) hpText.text   = $"{instance.health}/{def.maxHealth}";

        if (defText != null)
            defText.text = $"DEF {instance.ComputeSelfBlock()}";

        _lastHp = instance.health;
    }

    // ── Events → Hint ────────────────────────────────────────────────────────

    void OnGameEvent(GameEventType t, EventContext ctx)
    {
        if (instance == null) return;

        switch (t)
        {
            // Prima dell'attacco: mostra cosa sta per succedere
            case GameEventType.AttackDeclared:
                if (ctx.source == instance)
                {
                    // Questo slot attacca una carta player
                    int atk = instance.def.atkDamage + instance.tempAtkBonus;
                    ShowHint($"⚔ {atk}");
                }
                else if (ctx.target == instance)
                {
                    // Questo slot sta per essere colpito
                    int block = instance.ComputeSelfBlock();
                    ShowHint($"🛡 {block}");
                }
                break;

            // Dopo la risoluzione: mostra il risultato
            case GameEventType.AttackResolved:
                if (ctx.target == instance)
                {
                    if (ctx.amount > 0)
                    {
                        ClearHint();
                        ShowHint($"-{ctx.amount} HP");
                        UpdateHpOnly();
                        Blink();
                    }
                    else
                    {
                        ClearHint();
                        ShowHint("bloccato");
                    }
                }
                break;

            // Inizio turno: pulisci hint
            case GameEventType.TurnStart:
                HideHint();
                Refresh(); // aggiorna DEF che potrebbe essere cambiato con AdvanceFlip
                break;

            case GameEventType.TurnEnd:
                HideHint();
                break;

            // Hint diretto via PushHint (abilità, effetti passivi)
            case GameEventType.Info:
                if (ctx.source == instance &&
                    !string.IsNullOrEmpty(ctx.phase) &&
                    ctx.phase.StartsWith("HINT:"))
                {
                    ShowHint(ctx.phase.Substring("HINT:".Length).Trim());
                }
                break;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void UpdateHpOnly()
    {
        if (instance == null || hpText == null) return;
        hpText.text = $"{instance.health}/{instance.def.maxHealth}";
        _lastHp = instance.health;
    }

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
        if (hintText == null) return;
        hintText.gameObject.SetActive(true);
        hintText.text = string.IsNullOrEmpty(hintText.text) ? msg : hintText.text + "\n" + msg;
    }

    public void ClearHint()
    {
        if (hintText != null) hintText.text = string.Empty;
    }

    public void HideHint()
    {
        if (hintText == null) return;
        hintText.text = string.Empty;
        hintText.gameObject.SetActive(false);
    }

    public void SetHighlight(bool on)
    {
        if (_highlight == null)
            _highlight = gameObject.GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();
        _highlight.effectColor    = Color.yellow;
        _highlight.effectDistance = new Vector2(5, 5);
        _highlight.useGraphicAlpha = true;
        _highlight.enabled = on;
    }
}
