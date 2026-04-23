using UnityEngine;

public class ChargeBoost : AbilityBase
{
    [Min(1)] public int chargeThreshold = 2;
    [Min(1)] public int bonusDamage = 1;
    [Min(0)] public int splashDamage = 1;

    private EventBus.Handler _h;

    protected override void Register()
    {
        _h = OnEvent;
        EventBus.Subscribe(GameEventType.Custom, _h);
    }

    void OnEvent(GameEventType t, EventContext ctx)
    {
        if (t != GameEventType.Custom || ctx.phase != "PreCardAttack") return;
        if (ctx.source != Source || Source == null || !Source.alive) return;
        if (Source.side != Side.Fronte || Source.flipCharge < chargeThreshold) return;

        int boost = bonusDamage + Mathf.Max(0, Source.flipCharge - chargeThreshold);
        Source.tempAtkBonus += boost;
        Source.PushHint($"Charge +{boost}");

        if (splashDamage <= 0 || Source.flipCharge < CardInstance.MaxFlipCharge) return;

        var gm = GameManager.Instance;
        if (gm == null) return;

        for (int lane = 0; lane < gm.aiBoardRoot.childCount; lane++)
        {
            var slot = gm.GetEnemySlotAtLane(lane);
            if (slot == null || ReferenceEquals(slot, ctx.target)) continue;
            slot.health = Mathf.Max(0, slot.health - splashDamage);
            slot.PushHint($"Burst -{splashDamage}");
        }

        Logger.Info($"Charge burst: {Source.def.cardName} splashes for {splashDamage}");
    }

    protected override void Unregister()
    {
        EventBus.Unsubscribe(GameEventType.Custom, _h);
        _h = null;
    }
}
