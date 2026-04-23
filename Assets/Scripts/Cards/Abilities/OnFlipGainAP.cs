using UnityEngine;

public class OnFlipGainAP : AbilityBase
{
    [Min(0)] public int apGain = 1;

    protected override void Register()
    {
        EventBus.Subscribe(GameEventType.Flip, OnEvent);
    }

    protected override void Unregister()
    {
        EventBus.Unsubscribe(GameEventType.Flip, OnEvent);
    }

    void OnEvent(GameEventType t, EventContext ctx)
    {
        if (Source == null || ctx.source != Source || apGain <= 0) return;

        var gm = GameManager.Instance;
        if (gm == null) return;

        int gained = gm.GainPlayerAP(apGain, $"{Source.def.cardName} flip");
        if (gained > 0)
            Source.PushHint($"+{gained} AP");
    }
}
