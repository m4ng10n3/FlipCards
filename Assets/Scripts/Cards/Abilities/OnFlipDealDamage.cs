using UnityEngine;

public class OnFlipDealDamage : AbilityBase
{
    [Min(0)] public int damage = 1;
    public bool onlyWhenToFront = true;

    private EventBus.Handler _h;

    protected override void Register()
    {
        _h = OnEvent;
        EventBus.Subscribe(GameEventType.Flip, _h);
    }

    void OnEvent(GameEventType t, EventContext ctx)
    {
        if (Source == null || !Source.alive) return;
        if (ctx.source != Source || damage <= 0) return;
        if (onlyWhenToFront && Source.side != Side.Fronte) return;

        var gm = GameManager.Instance;
        if (gm == null) return;

        int lane = gm.GetLaneIndexFor(Source);
        if (lane < 0) return;

        var slot = gm.GetEnemySlotAtLane(lane);
        if (slot != null)
        {
            slot.health = Mathf.Max(0, slot.health - damage);
            slot.PushHint($"Flip -{damage}");
            Logger.Info($"Flip strike: {Source.def.cardName} hits {slot.def.SlotName} for {damage}");
        }
        else
        {
            Opponent.TakeDamage(damage);
            gm.UpdateHUD();
            Logger.Info($"Flip strike: {Source.def.cardName} hits boss for {damage}");
        }

        Source.PushHint($"Flip {damage}");
    }

    protected override void Unregister()
    {
        EventBus.Unsubscribe(GameEventType.Flip, _h);
        _h = null;
    }
}
