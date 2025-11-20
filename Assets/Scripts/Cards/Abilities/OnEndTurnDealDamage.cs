using UnityEngine;

public class OnEndTurnDealDamage : AbilityBase
{
    [Min(1)] public int damage = 1;
    public bool onlyWhenToFront = true;

    private EventBus.Handler _h;

    protected override void Register()
    {
        _h = (t, ctx) =>
        {
            if (t != GameEventType.TurnEnd) return;
            
            // Se richiesto solo quando passa al fronte, controlla il lato
            if (onlyWhenToFront && Source.side != Side.Fronte) return;

            // Hint quando si attiva
            EventBus.Publish(GameEventType.Info, new EventContext
            {
                owner = Owner,
                opponent = Opponent,
                source = Source,
                phase = "HINT: Turn End Damage"
            });

            AttackAll();
        };

        EventBus.Subscribe(GameEventType.TurnEnd, _h);
    }

    /// <summary>
    /// Esegue l'attacco al flip, usando il valore di damage come attacco temporaneo.
    /// Se non c'è un target valido, si affida alla logica già presente in CardInstance / GameManager
    /// per gestire il danno diretto agli HP.
    /// </summary>
    private void AttackAll()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        SlotView sView = null;
        int slots = gm.aiBoardRoot.childCount;
        for (int si = 0; si < slots; si++)
        {
            sView = gm.aiBoardRoot.GetChild(si).GetComponentInChildren<SlotView>(false);
            if (sView == null)
            {
                gm.ai.hp -= damage;
            }
            else
            {
                sView.instance.health -= damage;
            }
        }
    }

    protected override void Unregister()
    {
        if (_h != null)
        {
            EventBus.Unsubscribe(GameEventType.TurnEnd, _h);
            _h = null;
        }
    }
}
