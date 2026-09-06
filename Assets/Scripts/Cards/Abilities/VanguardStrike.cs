using UnityEngine;

public class VanguardStrike : AbilityBase
{
    [Min(1)] public int bonusDamage = 1;

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
        if (Source.side != Side.Fronte) return;

        var gm = GameManager.Instance;
        if (gm == null) return;

        int lane = gm.GetLaneIndexFor(Source);
        if (lane < 0) return;

        int emptyNeighbors = 0;
        if (gm.GetPlayerCardAtLane(lane - 1) == null) emptyNeighbors++;
        if (gm.GetPlayerCardAtLane(lane + 1) == null) emptyNeighbors++;
        if (emptyNeighbors <= 0) return;

        int bonus = bonusDamage * emptyNeighbors;
        Source.AddAtkBonus(bonus, AbilityCatalog.Name(this));
        Source.PushHint($"Vanguard +{bonus}");
    }

    protected override void Unregister()
    {
        EventBus.Unsubscribe(GameEventType.Custom, _h);
        _h = null;
    }
}
