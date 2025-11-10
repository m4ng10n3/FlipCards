using UnityEngine;

public class OnFlipDealDamage : AbilityBase
{
    [Min(1)] public int damage = 1;
    public bool onlyWhenToFront = true;

    private EventBus.Handler _h;

    protected override void Register()
    {
        _h = (t, ctx) =>
        {
            if (ctx.source != Source) return;

            if (t == GameEventType.Flip)
            {
                if (!onlyWhenToFront || Source.side == Side.Fronte)
                {
                    var gm = GameManager.Instance;
                    var target = gm.GetOpposingCardInstance(Source);
                    if (target != null)
                    {
                        // Delego alla pipeline di Attack per log/eventi/chain corretti
                        Source.Attack(ctx.owner, ctx.opponent, target);
                    }
                    // else: nessuno di fronte -> non fare nulla (o gestisci un fallback se lo vuoi)
                }
            }
        };

        EventBus.Subscribe(GameEventType.Flip, _h);
    }


    protected override void Unregister()
    {
        if (_h != null) EventBus.Unsubscribe(GameEventType.Flip, _h);
    }
}
