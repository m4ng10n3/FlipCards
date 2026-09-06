using UnityEngine;

public class ClassSynergyBoost : AbilityBase
{
    [Min(1)] public int bonusDamage = 1;

    protected override void Register()
    {
        EventBus.Subscribe(GameEventType.Custom, OnEvent);
    }

    protected override void Unregister()
    {
        EventBus.Unsubscribe(GameEventType.Custom, OnEvent);
    }

    void OnEvent(GameEventType t, EventContext ctx)
    {
        if (t != GameEventType.Custom || ctx.phase != "PrepareBattle") return;
        if (Source == null || !Source.alive || Source.side != Side.Fronte) return;

        var gm = GameManager.Instance;
        if (gm == null) return;

        int lane = gm.GetLaneIndexFor(Source);
        if (lane < 0) return;

        bool adjacentSameClass =
            MatchesClass(gm.GetPlayerCardAtLane(lane - 1)) ||
            MatchesClass(gm.GetPlayerCardAtLane(lane + 1));

        if (!adjacentSameClass) return;

        Source.AddAtkBonus(bonusDamage, AbilityCatalog.Name(this));
        Source.PushHint($"+{bonusDamage} class");
    }

    bool MatchesClass(CardInstance other)
    {
        return other != null && other.alive && other.def.cardClass == Source.def.cardClass;
    }
}
