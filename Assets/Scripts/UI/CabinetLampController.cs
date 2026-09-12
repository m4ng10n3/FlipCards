using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Dims the painted glass itself. No lamp sprites or panels cover the cabinet.</summary>
[RequireComponent(typeof(Image))]
public sealed class CabinetLampController : MonoBehaviour
{
    public CabinetArtDefinition definition;
    Material _material;
    Material _original;
    readonly Vector4[] _states = new Vector4[9];
    readonly Vector4[] _positions = new Vector4[9];
    readonly Vector4[] _sizes = new Vector4[9];
    readonly Vector4[] _windows = new Vector4[3];
    readonly TMP_Text[] _totals = new TMP_Text[9];
    SlotBatchManager _batch;
    public Material LampMaterial => _material;
    public int LampCount => definition != null ? definition.banks.Length * 7 : 0;

    void Start()
    {
        if (definition == null || definition.banks.Length != 9) { enabled = false; return; }
        var image = GetComponent<Image>();
        _original = image.material;
        _material = new Material(_original);
        image.material = _material;
        for (int i = 0; i < 9; i++)
        {
            var bank = definition.banks[i];
            _positions[i] = new Vector4(bank.first.x, bank.first.y, bank.step.x, bank.step.y);
            _sizes[i] = new Vector4(bank.glassRadius.x, bank.glassRadius.y, bank.stat, 0);
            _totals[i] = UiBuild.Text("Total" + bank.lane + "_" + bank.stat, transform, "", 16,
                GamePalette.Paper, TextAlignmentOptions.Center);
            var rect = ((RectTransform)transform).rect;
            var scale = new Vector2(rect.width / definition.sourceSize.x, rect.height / definition.sourceSize.y);
            var r = bank.totalRect;
            UiBuild.Band(_totals[i].rectTransform, r.x * scale.x, r.y * scale.y, r.width * scale.x, r.height * scale.y);
        }
        for (int i = 0; i < 3; i++)
        {
            var r = definition.windowRegions[i];
            _windows[i] = new Vector4(r.x, r.y, r.width, r.height);
        }
        _material.SetVector("_SourceSize", definition.sourceSize);
        _material.SetVectorArray("_GlassPositions", _positions);
        _material.SetVectorArray("_GlassSizes", _sizes);
        _material.SetVectorArray("_WindowRegions", _windows);
        RefreshIndicators();
    }

    void LateUpdate() => RefreshIndicators();

    public void RefreshIndicators()
    {
        if (_material == null) return;
        var gm = GameManager.Instance;
        if (_batch == null) _batch = FindAnyObjectByType<SlotBatchManager>();
        bool rolling = _batch != null && _batch.IsRolling;
        for (int i = 0; i < 9; i++)
        {
            var bank = definition.banks[i];
            var slot = gm != null ? gm.GetEnemySlotAtLane(bank.lane) : null;
            int value = 0;
            if (slot != null && slot.alive)
                value = bank.stat == 0 ? slot.health : bank.stat == 1
                    ? (slot.side == Side.Fronte ? slot.def.atkDamage + slot.tempAtkBonus : 0)
                    : (SynergyResolver.Resonates(gm, bank.lane) ? 0 : slot.ComputeSelfBlock());
            value = Mathf.Max(0, value);
            _states[i] = new Vector4(value, rolling ? (Mathf.FloorToInt(Time.unscaledTime * 11) + bank.lane) % 7 : -1, 0, 0);
            _totals[i].text = !rolling && value > 7 ? value.ToString() : "";
        }
        _material.SetVectorArray("_BankStates", _states);
    }

    void OnDestroy()
    {
        if (_material == null) return;
        var image = GetComponent<Image>();
        if (image != null) image.material = _original;
        Destroy(_material);
    }
}
