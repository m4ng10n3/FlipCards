using UnityEngine;

/// <summary>
/// ABILITÀ SLOT: Armatura in Prima Linea
/// Quando questo slot è in Fronte e viene attaccato, riduce il danno in arrivo
/// di armorValue. Si sovrappone al blockFront base dello slot.
/// Rende alcuni slot frontali molto più tenaci.
/// </summary>
public class SlotArmorFront : AbilityBase
{
    [Min(1)] public int armorValue = 1;

    private EventBus.Handler _h;
    private SlotView _slotView;
    private SlotInstance _slot;

    protected override void Register()
    {
        _slotView = GetComponent<SlotView>();
        _slot     = _slotView ? _slotView.instance : null;

        _h = (t, ctx) =>
        {
            if (t != GameEventType.AttackDeclared) return;
            if (!ReferenceEquals(ctx.target, _slot)) return;
            if (_slot == null || !_slot.alive) return;
            if (_slot.side != Side.Fronte) return;  // solo in Fronte

            _slot.tempBlockBonus += armorValue;
            _slot.PushHint($"Armor +{armorValue}");
        };

        EventBus.Subscribe(GameEventType.AttackDeclared, _h);
    }

    protected override void Unregister()
    {
        EventBus.Unsubscribe(GameEventType.AttackDeclared, _h);
        _h = null;
        _slotView = null;
        _slot     = null;
    }
}
