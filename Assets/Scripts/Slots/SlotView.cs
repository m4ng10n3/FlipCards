using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

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

    // Le reazioni si animano sull'artwork, non sulla radice della cella: la
    // radice e' figlia diretta dell'HorizontalLayoutGroup e a ogni layout pass
    // (una morte, un respawn) il gruppo le riscrive l'anchoredPosition e il tween
    // salterebbe. Il figlio "Sprite" non lo tocca nessuno.
    private RectTransform _art;
    private Image _bg;
    private Tween _motionTween;
    private Tween _flashTween;
    private Color _bgBaseColor;
    private bool _bgBaseColorRead;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        _btn = GetComponent<Button>();
        var bg = GetComponent<Image>();
        var le = GetComponent<LayoutElement>();

        if (_btn == null) _btn = gameObject.AddComponent<Button>();
        if (bg   == null) bg   = gameObject.AddComponent<Image>();
        if (_btn.targetGraphic == null) _btn.targetGraphic = bg;

        _bg = bg;
        _art = transform.Find("Sprite") as RectTransform;
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
        if (defText != null) defText.text = def.blockFront.ToString();
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

        // Solo il numero: il ruolo lo dichiara il badge del kit sotto (scudo),
        // come per ATK e HP. La parola "DEF" ripetuta tre volte sul rullo e'
        // rumore, e in 108 px si mangia lo spazio della cifra.
        if (defText != null)
            defText.text = instance.ComputeSelfBlock().ToString();

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
                // Niente glifi fuori dal set del font legacy: ⚔ e 🛡 uscivano
                // come rettangoli vuoti.
                if (ctx.source == instance)
                {
                    // Questo slot attacca una carta player
                    int atk = instance.def.atkDamage + instance.tempAtkBonus;
                    ShowHint($"ATTACCA {atk}");
                    PlayAttack();
                }
                else if (ctx.target == instance)
                {
                    // Questo slot sta per essere colpito
                    int block = instance.ComputeSelfBlock();
                    ShowHint($"PARA {block}");
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
                        PlayHit();
                    }
                    else
                    {
                        ClearHint();
                        ShowHint("BLOCCATO");
                        PlayBlock();
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

    public void Blink() => Flash(Color.yellow, 0.18f);

    // ── Reazioni di combattimento ─────────────────────────────────────────────
    //
    // Stesso vocabolario delle carte (CardView.PlayAttack / PlayBlock / PlayHit),
    // specchiato: lo slot sta in alto, quindi attacca verso il basso.

    /// <summary>Attacca: scatto verso la corsia del giocatore, cioe' in basso.</summary>
    public void PlayAttack()
    {
        Punch(Vector3.down * 40f, 0.34f, 0f);
        Flash(GamePalette.Danger, 0.30f, 0f);
    }

    /// <summary>Para: rimbalzo all'indietro e tinta del lato che ha bloccato.</summary>
    public void PlayBlock(float delay = CardView.ReactionDelay)
    {
        Punch(Vector3.up * 16f, 0.30f, delay);
        Flash(instance != null ? GamePalette.SideColor(instance.side) : GamePalette.Retro, 0.26f, delay);
    }

    /// <summary>Incassa: scossa e lampo rosso.</summary>
    public void PlayHit(float delay = CardView.ReactionDelay)
    {
        if (_art != null)
        {
            _motionTween?.Kill(complete: true);
            _motionTween = _art.DOShakeAnchorPos(0.34f, 26f, 20, 90f, false, false)
                               .SetDelay(delay)
                               .SetUpdate(true)
                               .SetLink(gameObject);
        }

        Flash(GamePalette.Danger, 0.32f, delay);
    }

    /// <summary>
    /// Ingresso in campo dopo il rullo: la cella si assesta con uno scatto, cosi
    /// gli slot si leggono uno per volta invece di comparire tutti insieme gia'
    /// coi valori finali.
    /// </summary>
    public void PlayEnter()
    {
        transform.DOKill(complete: true);
        transform.localScale = Vector3.one;
        transform.DOPunchScale(Vector3.one * 0.09f, 0.34f, 6, 0.7f)
                 .SetUpdate(true)
                 .SetLink(gameObject);

        Flash(GamePalette.SideColor(instance != null ? instance.side : Side.Fronte), 0.34f);
    }

    /// <summary>
    /// Evidenzia un valore che e' appena cambiato per effetto di un'abilita': la
    /// statistica finale da sola non dice da dove viene.
    /// </summary>
    public void PulseStat(Color accent)
    {
        Flash(accent, 0.42f);
        if (_art == null) return;

        _motionTween?.Kill(complete: true);
        _motionTween = _art.DOPunchAnchorPos(Vector2.up * 10f, 0.36f, 5, 0.7f)
                           .SetUpdate(true)
                           .SetLink(gameObject);
    }

    void Punch(Vector3 direction, float duration, float delay)
    {
        if (_art == null) return;

        _motionTween?.Kill(complete: true);
        _motionTween = _art.DOPunchAnchorPos(direction, duration, 4, 0.55f)
                           .SetDelay(delay)
                           .SetUpdate(true)
                           .SetLink(gameObject);
    }

    /// <summary>
    /// Scarto di colore sul fondo della cella. Il colore base si legge una volta
    /// sola: un flash che parte mentre il precedente e' in corso ripristinerebbe
    /// un colore gia' alterato e la cella resterebbe tinta.
    /// </summary>
    public void Flash(Color tint, float duration = 0.28f, float delay = 0f)
    {
        if (_bg == null) _bg = GetComponent<Image>();
        if (_bg == null) return;

        if (!_bgBaseColorRead)
        {
            _bgBaseColor = _bg.color;
            _bgBaseColorRead = true;
        }

        Color from = Color.Lerp(_bgBaseColor, tint, 0.8f);
        Color to = _bgBaseColor;
        var img = _bg;

        _flashTween?.Kill();
        _flashTween = DOTween.To(() => 0f, v => img.color = Color.Lerp(from, to, v), 1f, duration)
                             .SetDelay(delay)
                             .SetEase(Ease.OutQuad)
                             .OnKill(() => { if (img != null) img.color = to; })
                             .SetUpdate(true)
                             .SetLink(gameObject);
    }

    /// <summary>L'hint sostituisce il precedente: vedi CardView.ShowHint.</summary>
    public void ShowHint(string msg)
    {
        if (hintText == null) return;
        hintText.gameObject.SetActive(true);
        hintText.text = msg;
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
