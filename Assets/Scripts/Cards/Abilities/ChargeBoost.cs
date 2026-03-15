using UnityEngine;

/// <summary>
/// ABILITÀ: Carica Potenziata
/// Se la carta viene flippata verso Fronte con 2+ charge, infligge 1 danno bonus
/// a TUTTI gli slot nemici (oltre al danno normale dell'attacco in lane).
/// Con 3 charge: 2 danni bonus a tutti.
/// </summary>
public class ChargeBoost : AbilityBase
{
    private EventBus.Handler _h;

    protected override void Register()
    {
        _h = (t, ctx) =>
        {
            if (t != GameEventType.Flip) return;
            if (ctx.source != Source) return;
            if (Source.side != Side.Fronte) return;  // solo quando flippa verso Fronte
            // Le charge sono già state consumate da Attack(), ma le leggiamo
            // nell'evento Flip che arriva PRIMA che la carta attacchi nel tick corrente.
            // Usiamo flipCharge ancora disponibile: Flip avviene → ChargeBoost legge → Attack consuma
        };

        // Il vero trigger è su AttackDeclared da questa carta con carica
        _h = (t, ctx) =>
        {
            if (t != GameEventType.AttackDeclared) return;
            if (ctx.source != Source) return;
            // Le charge sono state già incluse in ctx.amount da CardInstance.Attack()
            // Noi reagiamo se il bonus fu significativo (amount > baseDamage)
            int bonus = ctx.amount - Source.def.frontDamage; // charge consumate
            if (bonus < 2) return;

            // Bonus: colpisce tutti gli slot
            var gm = GameManager.Instance;
            if (gm == null) return;
            int splash = (bonus >= 3) ? 2 : 1;
            for (int i = 0; i < gm.aiBoardRoot.childCount; i++)
            {
                var sv = gm.aiBoardRoot.GetChild(i).GetComponentInChildren<SlotView>(false);
                if (sv == null || !sv.instance.alive) continue;
                if (ReferenceEquals(ctx.target, sv.instance)) continue; // già colpito dal main attack

                sv.instance.health = Mathf.Max(0, sv.instance.health - splash);
                sv.instance.PushHint($"Splash -{splash}");
            }

            Source.PushHint($"Charge Burst! Splash {splash}");
            Logger.Info($"[ChargeBoost] {Source.def.cardName} splash {splash} dmg a tutti i slot");
        };

        EventBus.Subscribe(GameEventType.AttackDeclared, _h);
    }

    protected override void Unregister()
    {
        EventBus.Unsubscribe(GameEventType.AttackDeclared, _h);
        _h = null;
    }
}
