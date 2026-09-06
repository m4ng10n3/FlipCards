using UnityEngine;

public class OnFlipGainAP : AbilityBase
{
    [Min(0)] public int apGain = 1;
    int _rewardTurn = -1;

    protected override void Register()
    {
        _rewardTurn = -1;
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
        if (gm == null || !gm.CanAct || _rewardTurn == gm.CurrentTurn) return;

        int gained = gm.GainPlayerAP(apGain, $"{Source.def.cardName} flip");
        if (gained > 0)
        {
            _rewardTurn = gm.CurrentTurn;
            Source.PushHint($"+{gained} AP");
        }
    }
}
