using UnityEngine;

/// <summary>
/// ABILITÀ SLOT: Rigenerazione Retro
/// Quando lo slot avanza il suo pattern verso Retro, recupera regenAmount HP.
/// Rende gli slot con pattern difensivi più difficili da abbattere se non interrotti.
/// TRIGGER: GameEventType.Custom con phase "SlotRetroEffect"
/// </summary>
public class SlotRetroRegen : AbilityBase
{
    [Min(1)] public int regenAmount = 2;

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

            int healed = Mathf.Min(regenAmount, _slot.def.maxHealth - _slot.health);
            if (healed <= 0) return;

            _slot.health = Mathf.Min(_slot.def.maxHealth, _slot.health + regenAmount);
            _slot.PushHint($"Regen +{healed}HP");
            Logger.Info($"[SlotRetroRegen] {_slot.def.SlotName} recupera {healed}HP");
            if (_slotView != null) _slotView.Refresh();
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
