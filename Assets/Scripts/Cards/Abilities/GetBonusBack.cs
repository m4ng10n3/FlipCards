using UnityEngine;

public class GetBonusBack : AbilityBase
{
    private EventBus.Handler _h;

    protected override void Register()
    {
        int originalFrontDamage = Source.def.frontDamage;
        _h = (t, ctx) =>
        {
            if (t != GameEventType.Flip) return;
            if (Source.side != Side.Fronte && ctx.source == Source)
            {
                Source.def.frontDamage = originalFrontDamage;
                return;
            }

            UpdateFrontAttack(originalFrontDamage);
        };

        EventBus.Subscribe(GameEventType.Flip, _h);
    }

    private void UpdateFrontAttack(int baseDamage)
    {
        var gm = GameManager.Instance;
        int damage = baseDamage;

        for (int i = 0; i < gm.playerBoardRoot.childCount; i++)
        {
            var cardView = gm.playerBoardRoot.GetChild(i).GetComponentInChildren<CardView>();
            if (cardView == null) continue;

            var ci = cardView.instance;
            if (ci.side == Side.Retro && ci != Source && ci.def.faction == Source.def.faction)
            {
                damage += 1;
            }
        }

        if (damage != baseDamage)
        {
            EventBus.Publish(GameEventType.Info, new EventContext
            {
                owner = Owner,
                opponent = Opponent,
                source = Source,
                phase = "HINT: Back Bonus"
            });
        }

        Source.def.frontDamage = damage;
    }

    protected override void Unregister()
    {
        EventBus.Unsubscribe(GameEventType.Flip, _h);
        _h = null;
    }
}
