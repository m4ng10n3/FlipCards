using UnityEngine;

public class BlockAllAttacks : AbilityBase
{
    EventBus.Handler _h;

    protected override void Register()
    {
        _h = (t, ctx) =>
        {
            if (Source == null || !Source.alive) return;

            // Se un attacco è dichiarato contro questa carta, azzera il danno in arrivo per quel colpo
            if (t == GameEventType.AttackDeclared && ctx.target == Source)
            {
                Source.incomingDamageOverride = 0;
                Source.PushHint("Shield: blocked");
            }
        };

        EventBus.Subscribe(GameEventType.AttackDeclared, _h);
    }

    protected override void Unregister()
    {
        if (_h != null)
        {
            EventBus.Unsubscribe(GameEventType.AttackDeclared, _h);
            _h = null;
        }
    }
}
