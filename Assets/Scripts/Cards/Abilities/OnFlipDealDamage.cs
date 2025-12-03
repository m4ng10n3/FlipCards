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
            if (t != GameEventType.Flip || ctx.source != Source) return;
            if (onlyWhenToFront && Source.side != Side.Fronte) return;
            EventBus.Publish(GameEventType.Info, new EventContext { owner = Owner, opponent = Opponent, source = Source, phase = "HINT: Flip Damage" });
            DoFlipAttack();
        };
        EventBus.Subscribe(GameEventType.Flip, _h);
    }
    private void DoFlipAttack()
    {
        var gm = GameManager.Instance;
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
            Source.def.frontDamage = originalFrontDamage;
        }
    }
    protected override void Unregister()
    {
        EventBus.Unsubscribe(GameEventType.Flip, _h);
        _h = null;
    }
}
