using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Chrome della casella nemica: barra nome, ATK sempre visibile, traccia della
/// flipPattern con il passo corrente marcato e contatore di furia.
///
/// La traccia del pattern e' l'informazione piu' importante del tavolo: lo slot
/// non sceglie, segue una sequenza fissa che avanza a ogni fine turno. Senza
/// mostrarla, decidere se flippare adesso o al turno dopo diventa un tiro di dado.
/// </summary>
[DisallowMultipleComponent]
public class SlotOverlay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // ── Anatomia della casella del rullo ──────────────────────────────────────
    //
    // Coordinate banda in scala 2x: sono i numeri di `layouts.reel_cell` in
    // flipcards_ui_manifest.json moltiplicati per 2, cioe' la casella 176x144 del
    // kit portata a 352x288. La casella nemica e' orizzontale — e' un rullo da
    // slot machine, non una carta — e questo e' cio' che la distingue a colpo
    // d'occhio dalle corsie del giocatore.

    public const float CellW = 352f, CellH = 288f;

    public const float NameX = 44f, NameY = 6f, NameW = 184f, NameH = 28f;
    public const float FactionX = 12f, FactionY = 7f, FactionSize = 26f;

    public const float ArtX = 80f, ArtY = 40f, ArtSize = 192f;

    public const float ChipY = 236f, ChipH = 32f, ChipW = 108f;
    public static float ChipX(int index) => 10f + index * (ChipW + 4f);

    const float PatternX = 232f, PatternTrackY = 5f, PatternW = 112f, PatternH = 26f;
    const float SideBandY = 270f, SideBandHeight = 16f;

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
            _sideBand.color = GamePalette.WithAlpha(color, 0.85f);
            _sideLabel.text = inst.side == Side.Fronte ? "FRONTE · ATTACCA" : "RETRO · PASSIVO";
        }

        int atk = inst.def.atkDamage + inst.tempAtkBonus;
        if (atk != _lastAtk)
        {
            _lastAtk = atk;
            _atkLabel.text = inst.tempAtkBonus > 0
                ? $"{inst.def.atkDamage} <color=#FF8A8A>+{inst.tempAtkBonus}</color>"
                : inst.def.atkDamage.ToString();
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
        UiBuild.Band(nameBar, 4f, 4f, w - 8f, NameH + 2f);
        UiBuild.Fill(nameBar, GamePalette.WithAlpha(Color.black, 0.5f));

        var faction = _view.instance.def.faction;
        var facRt = UiBuild.Rect("FactionBadge", _rt);
        UiBuild.Band(facRt, FactionX, FactionY, FactionSize, FactionSize);
        UiBuild.Fill(facRt, GamePalette.FactionColor(faction));
        var facLabel = UiBuild.Text("Label", facRt, faction.ToString(), 15f,
                                    GamePalette.Background, TextAlignmentOptions.Center, FontStyles.Bold);
        UiBuild.Stretch(facLabel.rectTransform);

        // ATK e' sempre a schermo, non solo durante l'attacco: e' il numero su
        // cui il giocatore decide se coprire la corsia o lasciarla scoperta.
        var atkRt = ChipBg("AtkChip", ChipX(0), GamePalette.Danger);
        _atkLabel = UiBuild.Text("Label", atkRt, "0", 20f, GamePalette.TextPrimary,
                                 TextAlignmentOptions.Center, FontStyles.Bold);
        UiBuild.Stretch(_atkLabel.rectTransform, 0f, 0f, 0f, 3f);

        ChipBg("HpChipBg", ChipX(1), GamePalette.PlayerHp);
        ChipBg("DefChipBg", ChipX(2), GamePalette.Retro);

        _patternRoot = UiBuild.Rect("PatternTrack", _rt);
        UiBuild.Band(_patternRoot, PatternX, PatternTrackY, PatternW, PatternH);

        var bandRt = UiBuild.Rect("SideBand", _rt);
        UiBuild.Band(bandRt, 0f, SideBandY, w, SideBandHeight);
        _sideBand = UiBuild.Fill(bandRt, GamePalette.Fronte);

        _sideLabel = UiBuild.Text("Label", bandRt, string.Empty, 11f, GamePalette.Background,
                                  TextAlignmentOptions.Center, FontStyles.Bold);
        UiBuild.Stretch(_sideLabel.rectTransform);

        if (_berserker != null)
        {
            var furyRt = UiBuild.Rect("FuryChip", _rt);
            UiBuild.Band(furyRt, 12f, ArtY + 4f, 130f, 24f);
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

    RectTransform ChipBg(string name, float x, Color accent)
    {
        var rt = UiBuild.Rect(name, _rt);
        UiBuild.Band(rt, x, ChipY, ChipW, ChipH);
        UiBuild.Fill(rt, GamePalette.WithAlpha(Color.black, 0.55f));

        var stripe = UiBuild.Rect("Accent", rt);
        UiBuild.Band(stripe, 0f, ChipH - 3f, ChipW, 3f);
        UiBuild.Fill(stripe, accent);
        return rt;
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
                var none = UiBuild.Text("NoPattern", _patternRoot, "FISSO", 11f, GamePalette.TextMuted,
                                        TextAlignmentOptions.Center, FontStyles.Bold);
                UiBuild.Stretch(none.rectTransform);
                return;
            }

            const float gap = 4f;
            float cell = (PatternW - gap * (len - 1)) / len;

            for (int i = 0; i < len; i++)
            {
                var cellRt = UiBuild.Rect($"Step{i}", _patternRoot);
                UiBuild.Band(cellRt, i * (cell + gap), 2f, cell, PatternH - 4f);
                _patternCells.Add(UiBuild.Fill(cellRt, GamePalette.Neutral));

                var label = UiBuild.Text("Label", cellRt, string.Empty, 13f, GamePalette.Background,
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
                : GamePalette.WithAlpha(GamePalette.SideColor(side), 0.22f);
            _patternLabels[i].text = side == Side.Fronte ? "F" : "R";
            _patternLabels[i].color = current ? GamePalette.Background : GamePalette.WithAlpha(Color.white, 0.7f);
        }
    }
}
