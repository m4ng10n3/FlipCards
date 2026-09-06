using UnityEngine;

public class SlotStrikeOnAct : AbilityBase
{
    public enum SlotSignature
    {
        None,
        PressureFront,
        ArmorFront,
        BerserkFront,
        GuardAuraRetro,
        RegenRetro
    }

    public SlotSignature signature = SlotSignature.None;
    [Min(0)] public int power = 1;
    [Min(1)] public int threshold = 2;

    private EventBus.Handler _h;
    private SlotView _slotView;
    private SlotInstance _slot;
    private int _frontTurns;
    private bool _rageReady;

    protected override void Register()
    {
        _slotView = GetComponent<SlotView>();
        _slot = _slotView ? _slotView.instance : null;
        _frontTurns = 0;
        _rageReady = false;

        _h = OnEvent;
        EventBus.Subscribe(GameEventType.TurnStart, _h);
        EventBus.Subscribe(GameEventType.AttackDeclared, _h);
        EventBus.Subscribe(GameEventType.Custom, _h);
    }

    void OnEvent(GameEventType t, EventContext ctx)
    {
        if (_slot == null || !_slot.alive) return;

        switch (t)
        {
            case GameEventType.TurnStart:
                HandleTurnStart();
                break;

            case GameEventType.AttackDeclared:
                if (ReferenceEquals(ctx.target, _slot) && _slot.side == Side.Fronte && signature == SlotSignature.ArmorFront)
                {
                    _slot.tempBlockBonus += power;
                    _slot.PushHint($"Armor +{power}");
                }
                break;

            case GameEventType.Custom:
                if (ctx.phase == "PreSlotAttack" && ReferenceEquals(ctx.source, _slot))
                    HandlePreSlotAttack(ctx);
                else if (ctx.phase == "SlotRetroEffect" && ReferenceEquals(ctx.source, _slot))
                    HandleRetroEffect();
                break;
        }
    }

    void HandleTurnStart()
    {
        if (signature != SlotSignature.BerserkFront) return;

        if (_slot.side != Side.Fronte)
        {
            _frontTurns = 0;
            _rageReady = false;
            return;
        }

        // Slots respawn every roll: fury comes from matching symbols, not age.
        _frontTurns = GameManager.Instance != null ? GameManager.Instance.CountEnemyFaction(_slot.def.faction) : 1;
        if (_frontTurns >= threshold)
        {
            _rageReady = true;
            _slot.tempAtkBonus += power;
            _slot.PushHint("Rage ready");
        }
    }

    void HandlePreSlotAttack(EventContext ctx)
    {
        if (_slot.side != Side.Fronte) return;

        switch (signature)
        {
            case SlotSignature.PressureFront:
                if (ctx.target == null)
                {
                    _slot.tempAtkBonus += power;
                    _slot.PushHint($"Pressure +{power}");
                }
                break;

            case SlotSignature.BerserkFront:
                if (_rageReady)
                {
                    _slot.PushHint($"Rage +{power}");
                    _rageReady = false;
                }
                break;
        }
    }

    void HandleRetroEffect()
    {
        if (_slot.side != Side.Retro) return;

        switch (signature)
        {
            case SlotSignature.GuardAuraRetro:
                ApplyGuardAura();
                break;

            case SlotSignature.RegenRetro:
                ApplyRegen();
                break;
        }
    }

    void ApplyGuardAura()
    {
        var gm = GameManager.Instance;
        if (gm == null || power <= 0) return;

        int lane = gm.GetLaneIndexFor(_slot);
        int buffed = 0;

        for (int delta = -1; delta <= 1; delta += 2)
        {
            var other = gm.GetEnemySlotAtLane(lane + delta);
            if (other == null) continue;
            other.tempBlockBonus += power;
            other.PushHint($"Aura +{power}");
            buffed++;
        }

        if (buffed > 0)
        {
            _slot.PushHint($"Aura {buffed}");
            Logger.Info($"Slot aura: {_slot.def.SlotName} shields {buffed} neighbors");
        }
    }

    void ApplyRegen()
    {
        if (power <= 0) return;

        int before = _slot.health;
        _slot.health = Mathf.Min(_slot.def.maxHealth, _slot.health + power);
        int healed = _slot.health - before;
        if (healed <= 0) return;

        _slot.PushHint($"+{healed} HP");
        if (_slotView != null) _slotView.Refresh();
        Logger.Info($"Slot regen: {_slot.def.SlotName} heals {healed}");
    }

    protected override void Unregister()
    {
        EventBus.Unsubscribe(GameEventType.TurnStart, _h);
        EventBus.Unsubscribe(GameEventType.AttackDeclared, _h);
        EventBus.Unsubscribe(GameEventType.Custom, _h);
        _h = null;
        _slotView = null;
        _slot = null;
    }
}
