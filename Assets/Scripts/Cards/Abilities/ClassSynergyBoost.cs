using UnityEngine;

/// <summary>
/// Assalto — abilità FRONTE:
/// Se una carta adiacente sulla board ha la stessa classe (Assalto),
/// questa carta ottiene +bonusDamage ATK temporaneo prima di attaccare.
/// </summary>
public class ClassSynergyBoost : AbilityBase
{
    [Min(1)] public int bonusDamage = 2;

    protected override void Register()
    {
        EventBus.Subscribe(GameEventType.AttackDeclared, OnEvent);
    }

    protected override void Unregister()
    {
        EventBus.Unsubscribe(GameEventType.AttackDeclared, OnEvent);
    }

    void OnEvent(GameEventType t, EventContext ctx)
    {
        if (ctx.source != Source || Source.side != Side.Fronte) return;

        var gm = GameManager.Instance;
        if (gm == null) return;

        var board = Owner.board;
        int myIdx = board.IndexOf(Source);
        if (myIdx < 0) return;

        bool adjacentSameClass = false;
        if (myIdx > 0 && board[myIdx - 1]?.alive == true && board[myIdx - 1].def.cardClass == Source.def.cardClass)
            adjacentSameClass = true;
        if (myIdx < board.Count - 1 && board[myIdx + 1]?.alive == true && board[myIdx + 1].def.cardClass == Source.def.cardClass)
            adjacentSameClass = true;

        if (adjacentSameClass)
        {
            Source.tempAtkBonus += bonusDamage;
            Source.PushHint($"+{bonusDamage} sinergia");
        }
    }
}
