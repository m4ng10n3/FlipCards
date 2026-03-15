using UnityEngine;

/// <summary>
/// ABILITÀ: Pulsazione Vitale
/// Ogni volta che la carta flippа (in qualsiasi direzione), recupera healAmount HP.
/// Crea sinergia con i flip ripetuti: più si flippa, più si sopravvive.
/// </summary>
public class PulseHeal : AbilityBase
{
    [Min(1)] public int healAmount = 1;

    private EventBus.Handler _h;

    protected override void Register()
    {
        _h = (t, ctx) =>
        {
            if (t != GameEventType.Flip) return;
            if (ctx.source != Source || !Source.alive) return;

            int healed = Mathf.Min(healAmount, Source.def.maxHealth - Source.health);
            if (healed <= 0) return;

            Source.health = Mathf.Min(Source.def.maxHealth, Source.health + healAmount);
            Source.PushHint($"+{healed}HP");
            Logger.Info($"[PulseHeal] {Source.def.cardName} recupera {healed}HP");
        };

        EventBus.Subscribe(GameEventType.Flip, _h);
    }

    protected override void Unregister()
    {
        EventBus.Unsubscribe(GameEventType.Flip, _h);
        _h = null;
    }
}
