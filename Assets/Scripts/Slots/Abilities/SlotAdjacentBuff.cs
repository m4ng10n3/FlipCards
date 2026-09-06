using UnityEngine;

/// <summary>
/// ABILITÀ SLOT: Aura Difensiva
/// Quando questo slot è in Retro, i due slot adiacenti ricevono +blockBonus di block
/// per il prossimo colpo che subiscono (tramite tempBlockBonus).
/// Crea cluster difensivi difficili da sfondare.
/// TRIGGER: GameEventType.Custom con phase "SlotRetroEffect"
/// </summary>
public class SlotAdjacentBuff : AbilityBase
{
    [Min(1)] public int blockBonus = 1;

    private EventBus.Handler _h;
    private SlotView _slotView;
    private SlotInstance _slot;

    protected override void Register()
    {
        _slotView = GetComponent<SlotView>();
        _slot     = _slotView ? _slotView.instance : null;

        _h = (t, ctx) =>
        {
            if (t != GameEventType.Custom) return;
            if (ctx.phase != "SlotRetroEffect") return;
            if (!ReferenceEquals(ctx.source, _slot)) return;
            if (_slot == null || !_slot.alive) return;

            var gm = GameManager.Instance;
            if (gm == null) return;

            int myLane = _slotView.transform.GetSiblingIndex();
            int buffed  = 0;

            // Applica il bonus alle lane adiacenti
            int[] neighbors = { myLane - 1, myLane + 1 };
            foreach (int ln in neighbors)
            {
                if (ln < 0 || ln >= gm.aiBoardRoot.childCount) continue;
                var sv = gm.aiBoardRoot.GetChild(ln).GetComponentInChildren<SlotView>(false);
                if (sv == null || !sv.instance.alive) continue;

                sv.instance.AddBlockBonus(blockBonus, $"{AbilityCatalog.Name(this)} di {_slot.def.SlotName}");
                sv.instance.PushHint($"AuraDef +{blockBonus}");
                buffed++;
            }

            if (buffed > 0)
            {
                _slot.PushHint($"Aura →{buffed} slot");
                Logger.Info($"[SlotAdjacentBuff] {_slot.def.SlotName} buffa {buffed} slot adiacenti (+{blockBonus} block)");
            }
        };

        EventBus.Subscribe(GameEventType.Custom, _h);
    }

    protected override void Unregister()
    {
        EventBus.Unsubscribe(GameEventType.Custom, _h);
        _h = null;
        _slotView = null;
        _slot     = null;
    }
}
