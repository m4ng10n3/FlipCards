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
    string _lastPhase;
    bool _endShown;

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
    }

    void UpdateTurn(GameManager gm)
    {
        if (gm.CurrentTurn == _lastTurn) return;
        _lastTurn = gm.CurrentTurn;
        if (turnText != null)
            turnText.text = $"TURNO <b>{Mathf.Min(gm.CurrentTurn, gm.turns)}</b> / {gm.turns}";
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

        if (gm.ai.hp != _lastBossHp)
        {
            _lastBossHp = gm.ai.hp;
            if (bossHpBar != null) bossHpBar.Set(gm.ai.hp, gm.ai.maxHp);
            if (bossHpText != null) bossHpText.text = $"{gm.ai.hp}/{gm.ai.maxHp}";
        }

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

        for (int i = 0; i < _pips.Count; i++)
            _pips[i].color = i < ap ? GamePalette.Ap : GamePalette.WithAlpha(GamePalette.Ap, 0.16f);
    }

    /// <summary>
    /// Un pallino per AP disponibile: il costo delle azioni e' 1, quindi si
    /// contano a occhio. La colonna si dispone da sola in verticale se il rect e'
    /// piu' alto che largo.
    /// </summary>
    void BuildPips(int max)
    {
        if (apPipsRoot == null || _pips.Count == max) return;

        UiBuild.ClearChildren(apPipsRoot);
        _pips.Clear();

        bool vertical = apPipsRoot.rect.height > apPipsRoot.rect.width;
        const float gap = 8f;
        float size = vertical
            ? Mathf.Min(apPipsRoot.rect.width, (apPipsRoot.rect.height - gap * (max - 1)) / max)
            : Mathf.Min(apPipsRoot.rect.height, (apPipsRoot.rect.width - gap * (max - 1)) / max);

        for (int i = 0; i < max; i++)
        {
            var rt = UiBuild.Rect($"Pip{i}", apPipsRoot);
            if (vertical)
                UiBuild.Band(rt, (apPipsRoot.rect.width - size) * 0.5f, i * (size + gap), size, size);
            else
                UiBuild.Band(rt, i * (size + gap), (apPipsRoot.rect.height - size) * 0.5f, size, size);
            _pips.Add(UiBuild.Fill(rt, GamePalette.Ap));
        }
    }

    void UpdateCounters(GameManager gm)
    {
        var hand = gm.HandManager;
        if (hand == null) return;

        if (hand.DeckCount != _lastDeck)
        {
            _lastDeck = hand.DeckCount;
            if (deckText != null)
            {
                deckText.text = $"MAZZO {_lastDeck}";
                deckText.color = _lastDeck > 0 ? GamePalette.TextPrimary : GamePalette.Danger;
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
        string title = result == "Player ahead" ? "VITTORIA" : result == "Boss ahead" ? "SCONFITTA" : "PAREGGIO";
        Color color = result == "Player ahead" ? GamePalette.Good : result == "Boss ahead" ? GamePalette.Danger : GamePalette.TextMuted;

        if (endTitle != null) { endTitle.text = title; endTitle.color = color; }
        if (endDetail != null)
        {
            int played = Mathf.Min(gm.CurrentTurn, gm.turns);
            endDetail.text = $"Giocatore {gm.player.hp}/{gm.player.maxHp}   ·   Boss {gm.ai.hp}/{gm.ai.maxHp}\n" +
                             (played == 1 ? $"1 turno giocato su {gm.turns}" : $"{played} turni giocati su {gm.turns}");
        }

        endPanel.SetActive(true);
    }
}
