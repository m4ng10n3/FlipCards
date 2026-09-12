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

    void Start()
    {
        // The integrated artwork is driven once on the cabinet material itself.
        if (Resources.Load<CabinetArtDefinition>("ActiveCabinetArt") != null) { enabled = false; return; }
        // Quote dal centro della colonna: la colonna e' larga quanto la faccia
        // del rullo, e quella segue la finestra della cassa, non una costante.
        float c = ((RectTransform)transform).rect.width * .5f;
        _hp = BankHealth(c - 126, 140, 252, 62, 7, out _hpOverflow);
        _atk = BankAttack(c - 178, 207, 26, 245, 7, out _atkOverflow);
        _guard = BankGuard(c - 126, 482, 252, 68, 7, out _guardOverflow);
    }

    Image[] BankHealth(float x, float y, float width, float height, int capacity, out TMP_Text overflow)
    {
        return Bank("Health", "round", transform, x, y, width, height, capacity, 36, 62, 32, 40, false, out overflow);
    }

    Image[] BankAttack(float x, float y, float width, float height, int capacity, out TMP_Text overflow)
    {
        return Bank("Attack", "spear", transform, x, y, width, height, capacity, 26, 35, 22, 32, true, out overflow);
    }

    Image[] BankGuard(float x, float y, float width, float height, int capacity, out TMP_Text overflow)
    {
        return Bank("Guard", "shield", transform, x, y, width, height, capacity, 36, 68, 32, 44, false, out overflow);
    }

    Image[] Bank(string name, string shape, Transform parent, float x, float y, float width, float height, int capacity, int cellWidth, int cellHeight, int imageWidth, int imageHeight, bool vertical, out TMP_Text overflow)
    {
        var root = UiBuild.Rect(name, parent);
        UiBuild.Band(root, x, y, width, height);
        // Dark sockets separate the indicators from the illustrated reel.
        UiBuild.Fill(root, new Color32(111, 83, 36, 255));
        var recess = UiBuild.Rect("Recess", root);
        UiBuild.Band(recess, 1, 1, width - 2, height - 2);
        UiBuild.Fill(recess, new Color32(5, 13, 14, 255));

        var images = new Image[capacity];
        for (int i = 0; i < capacity; i++)
        {
            var rt = UiBuild.Rect("Lamp" + i, root);
            float left = vertical ? (cellWidth - imageWidth) * .5f : i * cellWidth + (cellWidth - imageWidth) * .5f;
            float top = vertical ? i * cellHeight + (cellHeight - imageHeight) * .5f : 3;
            UiBuild.Band(rt, left, top, imageWidth, imageHeight);
            images[i] = UiBuild.Fill(rt, Color.white);
            images[i].sprite = UiSkin.Sprite("lamp_v3_" + shape + "_off");
            images[i].preserveAspect = true;
        }

        overflow = UiBuild.Text(name + "Total", root, "", 17, GamePalette.Paper, TextAlignmentOptions.Right);
        // Totals stay clear of both the lamps and the reel window.
        UiBuild.Band(overflow.rectTransform, vertical ? -6 : width - 38,
            vertical ? height + 2 : height - 18, 38, 18);
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
            bank[i].sprite = UiSkin.Sprite("lamp_v3_" + shape + (lit ? "_on" : "_off"));
        }
        overflow.text = !rolling && value > bank.Length ? value.ToString() : "";
    }
}
