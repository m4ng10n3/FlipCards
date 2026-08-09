using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Chrome della casella nemica, montato sull'anatomia del kit *Arcade Horror CRT*
/// (`layouts.reel_cell` del manifest, ×2).
///
/// **Il nemico non e' una carta e non ha due lati.** E' la casella di un rullo da
/// slot machine: non si gira, non si trascina, non si scambia, e quello che fara'
/// non lo decide nessuno al tavolo — lo decide il programma del rullo, che
/// avanza da solo a ogni fine turno. Presentarlo come una carta faceva provare al
/// giocatore tre azioni che non esistono.
///
/// Quindi al posto della fascia FRONTE/RETRO ci sono due stati del rullo:
/// <list type="bullet">
/// <item><b>carica</b> — <c>reel_cell_{fazione}</c>: questo giro colpisce, e la
/// colonna si accende (<see cref="ReelChrome"/>).</item>
/// <item><b>trattenuta</b> — <c>reel_cell_locked</c>: questo giro non colpisce,
/// para e basta. E' l'"hold" della slot machine.</item>
/// </list>
///
/// I tre pip in cima sono il **programma**: cosa fara' nei giri successivi, con
/// il giro in corso marcato. E' l'informazione piu' importante del tavolo — senza,
/// decidere se coprire una corsia adesso o al turno dopo e' un tiro di dado.
///
/// A differenza della carta, la casella mostra **tutti e tre i numeri insieme**
/// (ATK, HP, DEF): non potendola girare, il giocatore deve poter fare il conto in
/// una sola occhiata, e non c'e' una seconda faccia su cui distribuirli.
/// </summary>
[DisallowMultipleComponent]
public class SlotOverlay : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    // ── Anatomia della casella del rullo ──────────────────────────────────────
    //
    // Coordinate banda in scala 2x: i numeri di `layouts.reel_cell` moltiplicati
    // per 2, cioe' la casella 176x144 del kit portata a 352x288. La casella e'
    // orizzontale — e' un rullo, non una fila di carte — e la forma e' cio' che
    // lo dice prima di qualunque etichetta.

    public const float CellW = 352f, CellH = 288f;

    public const float NameX = 44f, NameY = 8f, NameW = 206f, NameH = 28f;
    public const float FactionX = 12f, FactionY = 10f, FactionSize = 26f;

    public const float ArtX = 80f, ArtY = 44f, ArtSize = 192f;

    // Tre caselle statistica in fondo: ATK, HP, DEF. Il badge del kit ha l'icona
    // a sinistra, quindi il numero parte dopo di lei.
    public const float ChipY = 244f, ChipH = 36f, ChipW = 108f, ChipGap = 4f;
    public const float ChipTextInset = 26f;
    public static float ChipX(int index) => 10f + index * (ChipW + ChipGap);
    public static float ChipTextX(int index) => ChipX(index) + ChipTextInset;
    public const float ChipTextW = ChipW - ChipTextInset - 6f;

    // Pip del programma, in coda alla striscia del nome.
    const float PipRight = 344f, PipY = 14f, PipSize = 16f, PipPitch = 26f;
    const float PipMarkSize = 24f;

    SlotView _view;
    RectTransform _rt;

    Image _frame;            // cornice della casella: carica o trattenuta
    Image _medallion;
    TextMeshProUGUI _atkLabel;
    RectTransform _pipRoot;
    readonly List<Image> _pips = new List<Image>();
    readonly List<Image> _pipMarks = new List<Image>();
    Image _furyChip;
    TextMeshProUGUI _furyLabel;
    SlotBerserker _berserker;

    Side _lastSide = (Side)(-1);
    int _lastStep = -1, _lastAtk = int.MinValue, _lastFury = int.MinValue;
    bool _built;

    /// <summary>La casella colpisce in questo giro. La legge <see cref="ReelChrome"/>.</summary>
    public bool Armed => _view != null && _view.instance != null && _view.instance.side == Side.Fronte;

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
            ApplyReelState(inst.side);
        }

        int atk = inst.def.atkDamage + inst.tempAtkBonus;
        if (atk != _lastAtk)
        {
            _lastAtk = atk;
            _atkLabel.text = inst.tempAtkBonus > 0
                ? $"{inst.def.atkDamage} <color=#FF8A8A>+{inst.tempAtkBonus}</color>"
                : inst.def.atkDamage.ToString();
        }

        if (inst.PatternStep != _lastStep || _pips.Count != PipCount(inst))
        {
            _lastStep = inst.PatternStep;
            RefreshProgram(inst);
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

    /// <summary>
    /// Carica o trattenuta. Senza kit la differenza la fa il colore della cornice:
    /// il ripiego deve restare leggibile, e' l'unico segnale dello stato.
    /// </summary>
    void ApplyReelState(Side side)
    {
        bool armed = side == Side.Fronte;

        var sprite = UiSkin.Sprite(armed
            ? UiSkin.ReelCell(_view.instance.def.faction)
            : UiSkin.ReelCellLocked);

        if (sprite != null)
        {
            _frame.sprite = sprite;
            _frame.color = Color.white;
        }
        else
        {
            _frame.color = GamePalette.WithAlpha(armed ? GamePalette.Fronte : GamePalette.Retro, 0.22f);
        }

        // Trattenuta, la casella si spegne: il simbolo resta leggibile ma non
        // chiama l'attenzione, che deve andare alle colonne che colpiscono.
        if (_medallion != null)
            _medallion.color = GamePalette.WithAlpha(Color.white, armed ? 1f : 0.45f);
    }

    // ── Ispettore ─────────────────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_view != null && _view.instance != null)
            InspectorPanel.Instance?.ShowSlot(_view);
    }

    public void OnPointerExit(PointerEventData eventData) => InspectorPanel.Instance?.HideFor(_view);

    /// <summary>
    /// L'unica interazione possibile con un nemico: agganciare la sua scheda
    /// nell'ispettore. Non si gira e non si sposta, quindi il clic e' libero per
    /// fare l'unica cosa che serve — poter leggere ATK, DEF e programma senza
    /// tenere il puntatore fermo sulla casella mentre si guarda dall'altra parte.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (_view != null && _view.instance != null)
            InspectorPanel.Instance?.TogglePinSlot(_view);
    }

    // ── Costruzione ───────────────────────────────────────────────────────────

    void Build()
    {
        _built = true;

        var def = _view.instance.def;

        // Ordine di disegno della casella (recipes.reel_cell del manifest):
        // simbolo → medaglione → cornice → tag, pip, badge → nome.
        // Il simbolo e' il figlio "Sprite" del prefab e deve stare sotto tutto:
        // la cornice del kit ha la finestra trasparente ed e' lei a incorniciarlo.
        var art = _rt.Find("Sprite") as RectTransform;
        if (art != null) art.SetAsFirstSibling();

        BuildMedallion(def);
        BuildFrame();

        if (!Skinned)
        {
            var nameBar = UiBuild.Rect("NameBar", _rt);
            UiBuild.Band(nameBar, 4f, 4f, CellW - 8f, NameH + 2f);
            UiBuild.Fill(nameBar, GamePalette.WithAlpha(Color.black, 0.5f));
        }

        BuildFactionTag(def);

        // ATK, HP e DEF sempre a schermo: sono i numeri su cui il giocatore
        // decide se coprire la corsia o lasciarla scoperta, e la casella non ha
        // una seconda faccia dove nasconderne uno.
        var atkRt = Chip("AtkChip", 0, UiSkin.MicroAtk, GamePalette.Danger);
        _atkLabel = UiBuild.Text("Label", atkRt, "0", 20f, GamePalette.TextPrimary,
                                 TextAlignmentOptions.Center, FontStyles.Bold);
        UiBuild.Band(_atkLabel.rectTransform, ChipTextInset, 0f, ChipTextW, ChipH);
        _atkLabel.alignment = TextAlignmentOptions.Center;

        Chip("HpChipBg", 1, UiSkin.MicroHp, GamePalette.PlayerHp);
        Chip("DefChipBg", 2, UiSkin.MicroDef, GamePalette.Retro);

        _pipRoot = UiBuild.Rect("Program", _rt);
        UiBuild.Band(_pipRoot, 0f, 0f, CellW, NameH + 16f);

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

    static bool Skinned => UiSkin.Sprite(UiSkin.ReelFrame) != null;

    void BuildMedallion(SlotDefinition.Spec def)
    {
        var sprite = UiSkin.Sprite(UiSkin.EnemyMedallion(def.faction));
        if (sprite == null) return;

        var rt = UiBuild.Rect("Medallion", _rt);
        UiBuild.Band(rt, ArtX, ArtY, ArtSize, ArtSize);
        _medallion = UiBuild.Fill(rt, Color.white);
        _medallion.sprite = sprite;
        _medallion.type = Image.Type.Simple;
        rt.SetSiblingIndex(1);   // sopra il simbolo, sotto la cornice
    }

    /// <summary>
    /// La cornice della casella: e' lei a dire se il rullo e' carico o trattenuto.
    /// Non puo' essere l'Image della radice — quella disegna per prima e finirebbe
    /// sotto il simbolo, mentre la finestra della cornice del kit e' trasparente
    /// apposta per incorniciarlo.
    /// </summary>
    void BuildFrame()
    {
        var rt = UiBuild.Rect("CellFrame", _rt);
        UiBuild.Stretch(rt);

        var sprite = UiSkin.Sprite(UiSkin.ReelCell(_view.instance.def.faction));
        _frame = UiBuild.Fill(rt, sprite != null ? Color.white : GamePalette.WithAlpha(GamePalette.Fronte, 0.22f));
        if (sprite != null)
        {
            _frame.sprite = sprite;
            _frame.type = Image.Type.Simple;
        }

        rt.SetSiblingIndex(_medallion != null ? 2 : 1);
    }

    void BuildFactionTag(SlotDefinition.Spec def)
    {
        var rt = UiBuild.Rect("FactionTag", _rt);
        UiBuild.Band(rt, FactionX, FactionY, FactionSize, FactionSize);

        var sprite = UiSkin.Sprite(UiSkin.FactionTag(def.faction));
        if (sprite != null)
        {
            var img = UiBuild.Fill(rt, Color.white);
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            return;
        }

        UiBuild.Fill(rt, GamePalette.FactionColor(def.faction));
        var label = UiBuild.Text("Label", rt, def.faction.ToString(), 15f,
                                 GamePalette.Background, TextAlignmentOptions.Center, FontStyles.Bold);
        UiBuild.Stretch(label.rectTransform);
    }

    RectTransform Chip(string name, int index, string key, Color accent)
    {
        var rt = UiBuild.Rect(name, _rt);
        UiBuild.Band(rt, ChipX(index), ChipY, ChipW, ChipH);

        var sprite = UiSkin.Sprite(key);
        if (sprite != null)
        {
            var img = UiBuild.Fill(rt, Color.white);
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            return rt;
        }

        UiBuild.Fill(rt, GamePalette.WithAlpha(Color.black, 0.55f));
        var stripe = UiBuild.Rect("Accent", rt);
        UiBuild.Band(stripe, 0f, ChipH - 3f, ChipW, 3f);
        UiBuild.Fill(stripe, accent);
        return rt;
    }

    // ── Programma del rullo ───────────────────────────────────────────────────

    static int PipCount(SlotInstance inst) => Mathf.Max(1, inst.PatternLength);

    /// <summary>
    /// Un pip per giro programmato, con il giro in corso marcato. Non sono lati da
    /// girare: sono i colpi che arriveranno, in ordine. Ambra = colpisce,
    /// ciano = trattiene.
    /// </summary>
    void RefreshProgram(SlotInstance inst)
    {
        int count = PipCount(inst);

        if (_pips.Count != count)
        {
            UiBuild.ClearChildren(_pipRoot);
            _pips.Clear();
            _pipMarks.Clear();

            for (int i = 0; i < count; i++)
            {
                float x = PipRight - (count - i) * PipPitch + (PipPitch - PipSize);

                var markRt = UiBuild.Rect($"Mark{i}", _pipRoot);
                UiBuild.Band(markRt, x - (PipMarkSize - PipSize) * 0.5f,
                                     PipY - (PipMarkSize - PipSize) * 0.5f, PipMarkSize, PipMarkSize);
                var mark = Sprited(markRt, UiSkin.ReelPipCurrent, GamePalette.WithAlpha(Color.white, 0.85f));
                mark.enabled = false;
                _pipMarks.Add(mark);

                var pipRt = UiBuild.Rect($"Pip{i}", _pipRoot);
                UiBuild.Band(pipRt, x, PipY, PipSize, PipSize);
                _pips.Add(Sprited(pipRt, UiSkin.ReelPip(Side.Fronte), GamePalette.Fronte));
            }
        }

        bool fixedProgram = inst.PatternLength == 0;

        for (int i = 0; i < _pips.Count; i++)
        {
            var side = fixedProgram ? Side.Fronte : inst.PatternSideAt(i);
            bool current = fixedProgram || i == inst.PatternStep;

            var sprite = UiSkin.Sprite(UiSkin.ReelPip(side));
            if (sprite != null)
            {
                _pips[i].sprite = sprite;
                _pips[i].color = GamePalette.WithAlpha(Color.white, current ? 1f : 0.55f);
            }
            else
            {
                _pips[i].color = GamePalette.WithAlpha(GamePalette.SideColor(side), current ? 1f : 0.35f);
            }

            _pipMarks[i].enabled = current;
        }
    }

    static Image Sprited(RectTransform rt, string key, Color fallback)
    {
        var sprite = UiSkin.Sprite(key);
        var img = UiBuild.Fill(rt, sprite != null ? Color.white : fallback);
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = Image.Type.Simple;
        }
        return img;
    }
}
