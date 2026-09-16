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

        int bonus = PreviewBonus(gm, Source);
        if (bonus == 0) return;
        Source.AddAtkBonus(bonus, AbilityCatalog.Name(this));
        Source.PushHint($"+{bonus} class");
    }

    public int PreviewBonus(GameManager gm, CardInstance card)
    {
        if (!IsBound || gm == null || card == null || !card.alive || card.side != Side.Fronte) return 0;
        int lane = gm.GetLaneIndexFor(card);
        if (lane < 0) return 0;
        return MatchesClass(gm.GetPlayerCardAtLane(lane - 1), card) || MatchesClass(gm.GetPlayerCardAtLane(lane + 1), card) ? bonusDamage : 0;
    }

    static bool MatchesClass(CardInstance other, CardInstance card)
    {
        return other != null && other.alive && other.def.cardClass == card.def.cardClass;
    }
}
