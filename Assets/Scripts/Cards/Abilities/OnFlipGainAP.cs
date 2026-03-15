using UnityEngine;

/// <summary>
/// Tecnico — abilità FLIP:
/// Ogni volta che questa carta viene girata (in qualsiasi direzione),
/// il proprietario guadagna apGain Action Points, fino al massimo.
/// </summary>
public class OnFlipGainAP : AbilityBase
{
    [Min(1)] public int apGain = 1;

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
        if (ctx.source != Source) return;

        var gm = GameManager.Instance;
        if (gm == null) return;

        int before = Owner.actionPoints;
        Owner.actionPoints = Mathf.Min(Owner.actionPoints + apGain, gm.playerBaseAP);

        int gained = Owner.actionPoints - before;
        if (gained > 0)
        {
            Source.PushHint($"+{gained} AP");
            Logger.Info($"[Tecnico] {Source.def.cardName} flip → +{gained} AP");
        }
    }
}
