using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Physical lamp banks fixed to the cabinet; numbers never baked into artwork.</summary>
public sealed class MedallionInstruments : MonoBehaviour
{
    public int lane;
    public RectTransform laneRoot;
    Image[] _hp, _atk, _guard;
    TMP_Text _hpOverflow, _atkOverflow, _guardOverflow;
    SlotBatchManager _batch;

    // Le sedi sono dimensionate sui valori che le caselle hanno davvero, non a
    // sette per statistica come nella specifica del kit:
    //   vita     3..5  -> 7 sedi (la fila rossa del riferimento, con margine)
    //   attacco  1..3  -> 3 sedi: con cinque, due restavano sempre spente e la
    //                    fila ambra non diceva piu' niente
    //   guardia  0..5  -> 5 sedi, e sono quelle che si allargano: il tubo a
    //                    scarica e' l'asset piu' ricco del kit e a 22px
    //                    schiacciati non si leggeva.
    // Oltre le sedi disponibili compare il totale numerico, come prima.
    void Start()
    {
        // Quote dal centro della colonna: la colonna e' larga quanto la faccia
        // del rullo, e quella segue la finestra della cassa, non una costante.
        float c = ((RectTransform)transform).rect.width * .5f;
        _hp = Bank("Health", c - 126, 140, 252, 62, 7, "round", out _hpOverflow);
        _atk = Bank("Attack", c - 132, 478, 72, 84, 3, "spear", out _atkOverflow);
        _guard = Bank("Guard", c - 46, 518, 182, 44, 5, "shield", out _guardOverflow);
    }

    Image[] Bank(string name, float x, float y, float width, float height, int capacity, string shape, out TMP_Text overflow)
    {
        var root = UiBuild.Rect(name, transform);
        UiBuild.Band(root, x, y, width, height);
        var images = new Image[capacity];
        for (int i = 0; i < capacity; i++)
        {
            var rt = UiBuild.Rect("Lamp" + i, root);
            UiBuild.Band(rt, i * width / capacity, 0, width / capacity - 3, height);
            images[i] = UiBuild.Fill(rt, Color.white);
            images[i].sprite = UiSkin.Sprite("lamp_" + shape + "_off");
            // Il tubo a scarica e' quasi quadrato: senza preserveAspect veniva
            // stirato in una scheggia verticale e le griglie sparivano.
            images[i].preserveAspect = true;
        }
        overflow = UiBuild.Text(name + "Total", root, "", 17, GamePalette.Paper, TextAlignmentOptions.Right);
        UiBuild.Band(overflow.rectTransform, width - 35, height - 8, 35, 22);
        return images;
    }

    void LateUpdate()
    {
        var gm = GameManager.Instance;
        if (gm == null || _hp == null) return;
        if (laneRoot != null && lane < laneRoot.childCount)
        {
            var rt = (RectTransform)transform;
            var cell = (RectTransform)laneRoot.GetChild(lane);
            var parent = (RectTransform)transform.parent;
            var center = parent.InverseTransformPoint(cell.TransformPoint(cell.rect.center));
            rt.anchoredPosition = new Vector2(center.x - parent.rect.xMin - rt.rect.width * .5f, 0);
        }
        if (_batch == null) _batch = FindAnyObjectByType<SlotBatchManager>();
        bool rolling = _batch != null && _batch.IsRolling;
        var slot = gm.GetEnemySlotAtLane(lane);
        bool alive = slot != null && slot.alive;
        Set(_hp, _hpOverflow, alive ? slot.health : 0, "round", rolling);
        Set(_atk, _atkOverflow, alive && slot.side == Side.Fronte ? slot.def.atkDamage + slot.tempAtkBonus : 0, "spear", rolling);
        Set(_guard, _guardOverflow, alive && !SynergyResolver.Resonates(gm, lane) ? slot.ComputeSelfBlock() : 0, "shield", rolling);
    }

    void Set(Image[] bank, TMP_Text overflow, int value, string shape, bool rolling)
    {
        int chase = Mathf.FloorToInt(Time.unscaledTime * 11) + lane;
        for (int i = 0; i < bank.Length; i++)
        {
            bool lit = rolling ? (chase + i) % bank.Length < 2 : i < value;
            bank[i].sprite = UiSkin.Sprite("lamp_" + shape + (lit ? "_on" : "_off"));
        }
        overflow.text = !rolling && value > bank.Length ? value.ToString() : "";
    }
}
