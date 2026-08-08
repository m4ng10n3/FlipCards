using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Chrome aggiunto alla cella slot: fascia di lato, ATK sempre visibile, traccia
/// della flipPattern con il passo corrente marcato e contatore di furia.
///
/// La traccia del pattern e' l'informazione piu' importante del tavolo: lo slot
/// non sceglie, segue una sequenza fissa che avanza a ogni fine turno. Senza
/// mostrarla, decidere se flippare adesso o al turno dopo diventa un tiro di dado.
/// </summary>
[DisallowMultipleComponent]
public class SlotOverlay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // Bande della cella slot (LAYOUT_SPEC §6.6), coordinate dall'alto.
    const float NameH = 32f;
    const float ChipY = 258f, ChipH = 30f, ChipW = 68f;
    const float PatternY = 290f, PatternHeight = 22f;
    const float SideBandY = 312f, SideBandHeight = 18f;

    SlotView _view;
    RectTransform _rt;

    Image _sideBand;
    TextMeshProUGUI _sideLabel;
    TextMeshProUGUI _atkLabel;
    RectTransform _patternRoot;
    readonly List<Image> _patternCells = new List<Image>();
    readonly List<TextMeshProUGUI> _patternLabels = new List<TextMeshProUGUI>();
    Image _furyChip;
    TextMeshProUGUI _furyLabel;
    SlotBerserker _berserker;

    Side _lastSide = (Side)(-1);
    int _lastStep = -1, _lastAtk = int.MinValue, _lastFury = int.MinValue;
    bool _built;

    void Awake()
    {
        _view = GetComponent<SlotView>();
        _rt = (RectTransform)transform;
        _berserker = GetComponent<SlotBerserker>();
    }

    void LateUpdate()
    {
        if (_view == null || _view.instance == null) return;
        if (!_built) Build();

        var inst = _view.instance;

        if (inst.side != _lastSide)
        {
            _lastSide = inst.side;
            var color = GamePalette.SideColor(inst.side);
            _sideBand.color = color;
            _sideLabel.text = inst.side == Side.Fronte ? "FRONTE · ATTACCA" : "RETRO · PASSIVO";
        }

        int atk = inst.def.atkDamage + inst.tempAtkBonus;
        if (atk != _lastAtk)
        {
            _lastAtk = atk;
            _atkLabel.text = inst.tempAtkBonus > 0
                ? $"ATK {inst.def.atkDamage} <color=#f04d52>+{inst.tempAtkBonus}</color>"
                : $"ATK {inst.def.atkDamage}";
        }

        if (inst.PatternStep != _lastStep || _patternCells.Count != inst.PatternLength)
        {
            _lastStep = inst.PatternStep;
            RefreshPattern(inst);
        }

        if (_berserker != null && _berserker.FuryStacks != _lastFury)
        {
            _lastFury = _berserker.FuryStacks;
            _furyLabel.text = _berserker.BurstReady
                ? "FURIA x2"
                : $"FURIA {_berserker.FuryStacks}/{_berserker.furyThreshold}";
            _furyChip.color = _berserker.BurstReady
                ? GamePalette.Danger
                : GamePalette.WithAlpha(GamePalette.Danger, 0.45f);
        }
    }

    // ── Ispettore ─────────────────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_view != null && _view.instance != null)
            InspectorPanel.Instance?.ShowSlot(_view);
    }

    public void OnPointerExit(PointerEventData eventData) => InspectorPanel.Instance?.HideFor(_view);

    // ── Costruzione ───────────────────────────────────────────────────────────

    void Build()
    {
        _built = true;

        float w = _rt.rect.width;

        var nameBar = UiBuild.Rect("NameBar", _rt);
        UiBuild.Band(nameBar, 0f, 0f, w, NameH);
        UiBuild.Fill(nameBar, GamePalette.WithAlpha(Color.black, 0.55f));

        var faction = _view.instance.def.faction;
        var facRt = UiBuild.Rect("FactionBadge", _rt);
        UiBuild.Band(facRt, w - 30f, 4f, 26f, 24f);
        UiBuild.Fill(facRt, GamePalette.FactionColor(faction));
        var facLabel = UiBuild.Text("Label", facRt, faction.ToString(), 14f,
                                    new Color(0.05f, 0.06f, 0.09f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);
        UiBuild.Stretch(facLabel.rectTransform);

        // ATK e' sempre a schermo, non solo durante l'attacco: e' il numero su
        // cui il giocatore decide se coprire la corsia o lasciarla scoperta.
        var atkRt = UiBuild.Rect("AtkChip", _rt);
        UiBuild.Band(atkRt, 4f, ChipY, ChipW, ChipH);
        UiBuild.Fill(atkRt, GamePalette.WithAlpha(GamePalette.Danger, 0.85f));
        _atkLabel = UiBuild.Text("Label", atkRt, "ATK", 14f, GamePalette.TextPrimary,
                                 TextAlignmentOptions.Center, FontStyles.Bold);
        UiBuild.Stretch(_atkLabel.rectTransform);

        ChipBg("HpChipBg", 4f + ChipW + 4f, GamePalette.PlayerHp);
        ChipBg("DefChipBg", 4f + (ChipW + 4f) * 2f, GamePalette.Retro);

        _patternRoot = UiBuild.Rect("PatternTrack", _rt);
        UiBuild.Band(_patternRoot, 0f, PatternY, w, PatternHeight);
        UiBuild.Fill(_patternRoot, GamePalette.WithAlpha(Color.black, 0.55f));

        var bandRt = UiBuild.Rect("SideBand", _rt);
        UiBuild.Band(bandRt, 0f, SideBandY, w, SideBandHeight);
        _sideBand = UiBuild.Fill(bandRt, GamePalette.Fronte);

        _sideLabel = UiBuild.Text("Label", bandRt, string.Empty, 11f, new Color(0.05f, 0.06f, 0.09f, 1f),
                                  TextAlignmentOptions.Center, FontStyles.Bold);
        UiBuild.Stretch(_sideLabel.rectTransform);

        if (_berserker != null)
        {
            var furyRt = UiBuild.Rect("FuryChip", _rt);
            UiBuild.Band(furyRt, w - 104f, 226f, 100f, 24f);
            _furyChip = UiBuild.Fill(furyRt, GamePalette.WithAlpha(GamePalette.Danger, 0.45f));
            _furyLabel = UiBuild.Text("Label", furyRt, "FURIA", 12f, GamePalette.TextPrimary,
                                      TextAlignmentOptions.Center, FontStyles.Bold);
            UiBuild.Stretch(_furyLabel.rectTransform);
        }

        // I Text del prefab devono disegnare sopra i fondi appena creati.
        if (_view.nameText != null) _view.nameText.transform.SetAsLastSibling();
        if (_view.hpText != null) _view.hpText.transform.SetAsLastSibling();
        if (_view.defText != null) _view.defText.transform.SetAsLastSibling();

        var hint = _rt.Find("HintText");
        if (hint != null) hint.SetAsLastSibling();
    }

    void ChipBg(string name, float x, Color accent)
    {
        var rt = UiBuild.Rect(name, _rt);
        UiBuild.Band(rt, x, ChipY, ChipW, ChipH);
        UiBuild.Fill(rt, GamePalette.WithAlpha(Color.black, 0.62f));

        var stripe = UiBuild.Rect("Accent", rt);
        UiBuild.Band(stripe, 0f, ChipH - 3f, ChipW, 3f);
        UiBuild.Fill(stripe, accent);
    }

    /// <summary>Una casella per passo del pattern; quella corrente e' piena, le altre spente.</summary>
    void RefreshPattern(SlotInstance inst)
    {
        int len = inst.PatternLength;

        if (_patternCells.Count != len)
        {
            UiBuild.ClearChildren(_patternRoot);
            _patternCells.Clear();
            _patternLabels.Clear();

            if (len == 0)
            {
                var none = UiBuild.Text("NoPattern", _patternRoot, "SEMPRE FRONTE", 11f, GamePalette.TextMuted,
                                        TextAlignmentOptions.Center, FontStyles.Bold);
                UiBuild.Stretch(none.rectTransform);
                return;
            }

            float w = _patternRoot.rect.width;
            const float pad = 6f, gap = 3f;
            float cell = (w - pad * 2f - gap * (len - 1)) / len;

            for (int i = 0; i < len; i++)
            {
                var cellRt = UiBuild.Rect($"Step{i}", _patternRoot);
                UiBuild.Band(cellRt, pad + i * (cell + gap), 4f, cell, PatternHeight - 8f);
                _patternCells.Add(UiBuild.Fill(cellRt, GamePalette.Neutral));

                var label = UiBuild.Text("Label", cellRt, string.Empty, 11f, Color.black,
                                         TextAlignmentOptions.Center, FontStyles.Bold);
                UiBuild.Stretch(label.rectTransform);
                _patternLabels.Add(label);
            }
        }

        for (int i = 0; i < _patternCells.Count; i++)
        {
            var side = inst.PatternSideAt(i);
            bool current = i == inst.PatternStep;
            _patternCells[i].color = current
                ? GamePalette.SideColor(side)
                : GamePalette.WithAlpha(GamePalette.SideColor(side), 0.28f);
            _patternLabels[i].text = side == Side.Fronte ? "F" : "R";
            _patternLabels[i].color = current ? Color.black : GamePalette.WithAlpha(Color.white, 0.65f);
        }
    }
}
