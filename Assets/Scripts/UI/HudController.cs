using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tutto quello che nel gioco esiste come dato ma non aveva una casa a schermo:
/// contatore turno, etichetta di fase, barre HP con massimo, pallini AP,
/// contatori mazzo/mano e pannello di fine partita.
///
/// Legge lo stato in LateUpdate invece di farsi notificare: GameManager cambia
/// fase da piu' punti (OnAttack, SetButtonsInteractable, EndMatch) e non tutti
/// passano da UpdateHUD. Il polling scrive solo quando un valore cambia davvero.
/// </summary>
public class HudController : MonoBehaviour
{
    [Header("Barra superiore")]
    public TMP_Text turnText;
    public TMP_Text phaseText;
    public Image phaseChip;
    public TMP_Text attackCostText, endTurnLabel, endTurnCostText, rollSummaryText, actionCostsText;

    [Header("Boss")]
    public TMP_Text bossNameText;
    public UiBar bossHpBar;
    public TMP_Text bossHpText;

    [Header("Giocatore")]
    public UiBar playerHpBar;
    public TMP_Text playerHpText;
    public TMP_Text apText;
    public RectTransform apPipsRoot;

    [Header("Mazzo e mano")]
    public TMP_Text deckText;
    public TMP_Text handText;

    [Header("Fine partita")]
    public GameObject endPanel;
    public TMP_Text endTitle;
    public TMP_Text endDetail;

    readonly List<Image> _pips = new List<Image>();

    int _lastTurn = -1, _lastAp = -1, _lastMaxAp = -1;
    int _lastPlayerHp = -1, _lastBossHp = -1;
    int _lastDeck = -1, _lastHand = -1;
    Color? _deckInk;
    string _lastPhase;
    bool _endShown;
    Color _normalBossColor = Color.white;
    bool _bossColorCaptured, _bossPreviewShown;
    static readonly Color PreviewYellow = new Color(.78f, .53f, .02f);

    void LateUpdate()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.player == null || gm.ai == null) return;

        UpdateTurn(gm);
        UpdatePhase(gm);
        UpdateHealth(gm);
        UpdateAp(gm);
        UpdateCounters(gm);
        UpdateEndPanel(gm);
        UpdateCommands(gm);
    }

    void UpdateCommands(GameManager gm)
    {
        SetText(attackCostText, $"{Mathf.Max(1, gm.attackCost)} AP / attacca con le carte in Fronte");
        SetText(endTurnLabel, gm.AwaitingEndTurn ? "GIRA I RULLI" : "DIFENDI E GIRA");
        SetText(endTurnCostText, gm.AwaitingEndTurn ? "0 AP / prepara il prossimo giro" : "0 AP / risponde il nemico, tu non attacchi");
        SetText(rollSummaryText, gm.RollSummary);
        SetText(actionCostsText, $"PESCA {gm.drawCardCost} / GIOCA {gm.playCardCost} / FLIP {gm.flipCardCost} / ATK {Mathf.Max(1, gm.attackCost)} AP");
    }

    static void SetText(TMP_Text label, string value)
    {
        if (label != null && label.text != value) label.text = value;
    }

    void UpdateTurn(GameManager gm)
    {
        if (gm.CurrentTurn == _lastTurn) return;
        _lastTurn = gm.CurrentTurn;
        if (turnText != null)
            turnText.text = $"TURNO <b>{gm.CurrentTurn}</b>";
    }

    void UpdatePhase(GameManager gm)
    {
        string label;
        Color color;

        // Resolving prima di InputLocked: durante la risoluzione l'input e'
        // bloccato anche lui, ma le due attese non sono la stessa cosa e
        // chiamarle con lo stesso nome le renderebbe di nuovo indistinguibili.
        if (gm.MatchEnded)                { label = "PARTITA FINITA";                 color = GamePalette.Danger; }
        else if (gm.Resolving)            { label = "RISOLUZIONE IN CORSO";           color = GamePalette.Fronte; }
        else if (gm.InputLocked)          { label = "NUOVI SLOT IN ARRIVO";           color = GamePalette.Retro; }
        else if (gm.AwaitingEndTurn)      { label = "ATTACCO RISOLTO — CHIUDI IL TURNO"; color = GamePalette.Fronte; }
        else                              { label = "FASE AZIONI";                    color = GamePalette.Good; }

        if (label == _lastPhase) return;
        _lastPhase = label;

        if (phaseText != null) { phaseText.text = label; phaseText.color = color; }
        if (phaseChip != null) phaseChip.color = GamePalette.WithAlpha(color, 0.16f);
    }

    void UpdateHealth(GameManager gm)
    {
        if (gm.player.hp != _lastPlayerHp)
        {
            _lastPlayerHp = gm.player.hp;
            if (playerHpBar != null) playerHpBar.Set(gm.player.hp, gm.player.maxHp);
            if (playerHpText != null) playerHpText.text = $"{gm.player.hp}/{gm.player.maxHp}";
        }

        if (!_bossColorCaptured && bossHpText != null)
        {
            _normalBossColor = bossHpText.color;
            _bossColorCaptured = true;
        }
        var preview = DamagePreviewController.Active;
        bool showPreview = preview != null && preview.BossDamage > 0;
        int displayed = showPreview ? preview.DisplayedBossHp : gm.ai.hp;
        if (gm.ai.hp != _lastBossHp && bossHpBar != null) bossHpBar.Set(gm.ai.hp, gm.ai.maxHp);
        if (gm.ai.hp != _lastBossHp || showPreview || _bossPreviewShown)
        {
            SetText(bossHpText, $"{displayed}/{gm.ai.maxHp}");
            if (bossHpText != null) bossHpText.color = showPreview ? PreviewYellow : _normalBossColor;
        }
        _lastBossHp = gm.ai.hp;
        _bossPreviewShown = showPreview;

        if (bossNameText != null && string.IsNullOrEmpty(bossNameText.text))
            bossNameText.text = gm.ai.name.ToUpperInvariant();
    }

    void UpdateAp(GameManager gm)
    {
        int ap = gm.player.actionPoints;
        int max = gm.MaxPlayerAP;
        if (ap == _lastAp && max == _lastMaxAp) return;
        _lastAp = ap;
        _lastMaxAp = max;

        if (apText != null) apText.text = $"{ap}/{max}";
        BuildPips(max);

        var full = UiSkin.Sprite(UiSkin.ApSegFull);
        var spent = UiSkin.Sprite(UiSkin.ApSegSpent);

        for (int i = 0; i < _pips.Count; i++)
        {
            bool available = i < ap;

            if (full != null && spent != null)
            {
                _pips[i].sprite = available ? full : spent;
                _pips[i].color = Color.white;
            }
            else
            {
                _pips[i].color = available ? GamePalette.Ap : GamePalette.WithAlpha(GamePalette.Ap, 0.16f);
            }
        }
    }

    /// <summary>
    /// Un segmento per AP disponibile: il costo delle azioni e' 1, quindi si
    /// contano a occhio. Con il kit i segmenti hanno la forma del suo
    /// <c>ap_seg_*</c>; senza, restano pallini quadrati. La colonna si dispone da
    /// sola in verticale se il rect e' piu' alto che largo.
    /// </summary>
    void BuildPips(int max)
    {
        if (apPipsRoot == null || _pips.Count == max) return;

        UiBuild.ClearChildren(apPipsRoot);
        _pips.Clear();

        var segment = UiSkin.Sprite(UiSkin.ApSegFull);
        bool vertical = apPipsRoot.rect.height > apPipsRoot.rect.width;
        float gap = segment != null ? 4f : 8f;

        float w, h;
        if (segment != null)
        {
            // Le proporzioni le detta lo sprite; la larghezza si restringe solo
            // se i segmenti non ci stanno.
            h = Mathf.Min(apPipsRoot.rect.height, segment.rect.height);
            w = Mathf.Min(segment.rect.width * (h / segment.rect.height),
                          (apPipsRoot.rect.width - gap * (max - 1)) / max);
        }
        else
        {
            w = h = vertical
                ? Mathf.Min(apPipsRoot.rect.width, (apPipsRoot.rect.height - gap * (max - 1)) / max)
                : Mathf.Min(apPipsRoot.rect.height, (apPipsRoot.rect.width - gap * (max - 1)) / max);
        }

        for (int i = 0; i < max; i++)
        {
            var rt = UiBuild.Rect($"Pip{i}", apPipsRoot);
            if (vertical && segment == null)
                UiBuild.Band(rt, (apPipsRoot.rect.width - w) * 0.5f, i * (h + gap), w, h);
            else
                UiBuild.Band(rt, i * (w + gap), (apPipsRoot.rect.height - h) * 0.5f, w, h);

            var img = UiBuild.Fill(rt, segment != null ? Color.white : GamePalette.Ap);
            if (segment != null) { img.sprite = segment; img.type = Image.Type.Simple; }
            _pips.Add(img);
        }
    }

    void UpdateCounters(GameManager gm)
    {
        var hand = gm.HandManager;
        if (hand == null) return;

        // La carta estratta resta sulla pila finche' i segni del retro non si sono
        // materializzati: il numero scende quando si stacca, non al clic.
        int onDeck = hand.DeckCount + hand.PendingDraws;
        if (onDeck != _lastDeck)
        {
            _lastDeck = onDeck;
            if (deckText != null)
            {
                // Il colore e' quello del builder (inchiostro sul gettone d'ottone);
                // solo a mazzo vuoto diventa l'avviso.
                if (_deckInk == null) _deckInk = deckText.color;
                // Solo il numero: la parola "MAZZO" e' l'etichetta accanto alla
                // pila, scritta una volta dal builder. Scriverla anche qui la
                // stampava due volte sulla stessa riga.
                deckText.text = _lastDeck.ToString();
                deckText.color = _lastDeck > 0 ? _deckInk.Value : GamePalette.Danger;
            }
        }

        if (hand.HandCount != _lastHand)
        {
            _lastHand = hand.HandCount;
            if (handText != null)
            {
                handText.text = $"MANO {_lastHand}/{hand.MaxHandSize}";
                handText.color = _lastHand < hand.MaxHandSize ? GamePalette.TextPrimary : GamePalette.Danger;
            }
        }
    }

    void UpdateEndPanel(GameManager gm)
    {
        if (endPanel == null || _endShown || !gm.MatchEnded) return;
        _endShown = true;

        string result = gm.MatchResult;
        string title = result == "Player wins" ? "VITTORIA" : result == "Boss wins" ? "SCONFITTA" : "PAREGGIO";
        Color color = result == "Player wins" ? GamePalette.Good : result == "Boss wins" ? GamePalette.Danger : GamePalette.TextMuted;

        if (endTitle != null) { endTitle.text = title; endTitle.color = color; }
        if (endDetail != null)
        {
            int played = gm.CurrentTurn;
            endDetail.text = $"Giocatore {gm.player.hp}/{gm.player.maxHp}   ·   Boss {gm.ai.hp}/{gm.ai.maxHp}\n" +
                             (played == 1 ? "1 turno giocato" : $"{played} turni giocati");
        }

        endPanel.SetActive(true);
    }
}
