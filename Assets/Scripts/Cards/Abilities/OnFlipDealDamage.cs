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
            if (t != GameEventType.Flip) return;
            if (ctx.source != Source) return;

            // Se richiesto solo quando passa al fronte, controlla il lato
            if (onlyWhenToFront && Source.side != Side.Fronte) return;

            // Hint quando si attiva
            EventBus.Publish(GameEventType.Info, new EventContext
            {
                owner = Owner,
                opponent = Opponent,
                source = Source,
                phase = "HINT: Flip Damage"
            });

            DoFlipAttack();
        };

        EventBus.Subscribe(GameEventType.Flip, _h);
    }

    /// <summary>
    /// Esegue l'attacco al flip, usando il valore di damage come attacco temporaneo.
    /// Se non c'è un target valido, si affida alla logica già presente in CardInstance / GameManager
    /// per gestire il danno diretto agli HP.
    /// </summary>
    private void DoFlipAttack()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var target = gm.GetOpponentObjInstance(Source);
        if (target == null)
        {
            Opponent.hp -= damage;
        }
        else
        {
            int originalFrontDamage = Source.def.frontDamage;
            Source.def.frontDamage = damage;

            Source.Attack(Owner, Opponent, target);

            // Ripristina il valore originale
            Source.def.frontDamage = originalFrontDamage;
        }
        // Modifica temporaneamente la potenza d'attacco
    }

    protected override void Unregister()
    {
        if (_h != null)
        {
            EventBus.Unsubscribe(GameEventType.Flip, _h);
            _h = null;
        }
    }
}
