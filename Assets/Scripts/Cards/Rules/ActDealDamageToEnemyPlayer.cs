using UnityEngine;

[CreateAssetMenu(menuName="Cards/Act/DealDamageToEnemyPlayer")]
public class ActDealDamageToEnemyPlayer : ScriptableObject, IGameAction
{
    [Min(1)] public int amount = 1;
    public string Execute(EventContext ctx)
    {
        GameRules.DealDamageToPlayer(ctx.owner, ctx.opponent, ctx.source, amount, "RuleAction");
        return $"[RULE] Danno {amount} al player nemico";
    }
}
