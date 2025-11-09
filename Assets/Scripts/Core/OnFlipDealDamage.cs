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

            // Il tipo evento  il parametro 't', non un campo di EventContext
            if (t == GameEventType.Flip)
            {
                if (!onlyWhenToFront || Source.side == Side.Fronte)
                {

                    //GameManagerInteractive.Logf("[Ability Flip] {0} deals {1} to PLAYER {2}", Source.def.cardName, damage, Opponent.name);
                    var view = Source != null ? GameManagerInteractive.Instance?.GetComponentInChildren<CardView>() : null; // se non hai un accessor diretto
                    // Applica danno diretto al player avversario
                    Opponent.hp -= damage;

                    // Notifica che  stato inflitto danno
                    EventBus.Publish(
                        GameEventType.DamageDealt,
                        new EventContext
                        {
                            owner = Owner,
                            opponent = Opponent,
                            source = Source,
                            target = null,
                            amount = damage,
                            phase = ctx.phase
                        }
                    );
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
