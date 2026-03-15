using UnityEngine;

/// <summary>
/// ABILITÀ: Scudo Retro
/// Quando la carta è in Retro e viene attaccata, aggiunge +shieldBonus al block
/// di quel colpo (sopra al backBlockValue base della carta).
/// Rende questa carta un muro ancora più resistente in posizione difensiva.
/// </summary>
public class RetroShield : AbilityBase
{
    [Min(1)] public int shieldBonus = 2;

    private EventBus.Handler _h;

    protected override void Register()
    {
        _h = (t, ctx) =>
        {
            if (t != GameEventType.AttackDeclared) return;
            if (ctx.target != Source || !Source.alive) return;
            if (Source.side != Side.Retro) return; // solo in Retro

            Source.tempBlockBonus += shieldBonus;
            Source.PushHint($"Shield +{shieldBonus}");
        };

        EventBus.Subscribe(GameEventType.AttackDeclared, _h);
    }

    protected override void Unregister()
    {
        EventBus.Unsubscribe(GameEventType.AttackDeclared, _h);
        _h = null;
    }
}
