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

            if (!onlyWhenToFront || Source.side == Side.Fronte)
            {
                // Hint quando si attiva
                EventBus.Publish(GameEventType.Info, new EventContext
                {
                    owner = Owner,
                    opponent = Opponent,
                    source = Source,
                    phase = "HINT: Flip Damage"
                });

                TryAttackImmediateOrNextFrame();
            }
        };

        EventBus.Subscribe(GameEventType.Flip, _h);
    }

    void TryAttackImmediateOrNextFrame()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var target = gm.GetOpponentObjInstance(Source);
        if (target != null)
        {
            // Attack accetta object come target
            Source.Attack(Owner, Opponent, target);
        }
        else
        {
            // Target non ancora disponibile (slot in rebuild): riprova al frame successivo
            StartCoroutine(RetryNextFrame());
        }
    }

    System.Collections.IEnumerator RetryNextFrame()
    {
        yield return null; // aspetta un frame (dopo RebuildEnemySlotsToMatchPlayer)
        var gm = GameManager.Instance;
        if (gm == null) yield break;

        var target = gm.GetOpponentObjInstance(Source);
        if (target != null)
        {
            Source.Attack(Owner, Opponent, target);
        }
        // se è ancora null, non insistere per evitare spam: sarà stato davvero assente
    }


    protected override void Unregister()
    {
        if (_h != null) EventBus.Unsubscribe(GameEventType.Flip, _h);
    }
}
