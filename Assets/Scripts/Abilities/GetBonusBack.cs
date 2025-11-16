using UnityEngine;
using static Unity.VisualScripting.Metadata;


public class GetBonusBack : AbilityBase
{
    //[Min(1)] public int damage = 1;

    private EventBus.Handler _h;
    protected override void Register()
    {
        int originalFrontDamage = Source.def.frontDamage;
        _h = (t, ctx) =>
        {
            if (t != GameEventType.Flip) return;

            // Se richiesto solo quando passa al fronte, controlla il lato
            if (Source.side != Side.Fronte && ctx.source == Source)
            {
                Source.def.frontDamage = originalFrontDamage;
                return;
            }
            // Hint quando si attiva
            

            UpdateFrontAttack(originalFrontDamage);
        };

        EventBus.Subscribe(GameEventType.Flip, _h);
    }

    /// <summary>
    /// Esegue l'attacco al flip, usando il valore di damage come attacco temporaneo.
    /// Se non c'è un target valido, si affida alla logica già presente in CardInstance / GameManager
    /// per gestire il danno diretto agli HP.
    /// </summary>
    private void UpdateFrontAttack(int ogDamage)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        
        // Modifica temporaneamente la potenza d'attacco
        
        int damage = ogDamage;

        for (int i = 0; i < gm.playerBoardRoot.childCount; i++)
        {
            var ci = gm.playerBoardRoot.GetChild(i).GetComponentInChildren<CardView>().instance;
            if (ci.side == Side.Retro && ci!=Source && ci.def.faction == Source.def.faction)
            {
                damage += 1;
            }
        }

        if (damage != ogDamage) {

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
        if (_h != null)
        {
            EventBus.Unsubscribe(GameEventType.Flip, _h);
            _h = null;
        }
    }
}
