using UnityEngine;

/// <summary>
/// Guardia — abilità FRONTE:
/// Quando questa carta è in Fronte e un attacco slot colpisce una carta adiacente,
/// trasferisce shieldValue block bonus a quella carta adiacente.
/// </summary>
public class AdjacencyShield : AbilityBase
{
    [Min(1)] public int shieldValue = 2;

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
        if (Source == null || Source.side != Side.Fronte) return;
        if (!(ctx.source is SlotInstance)) return;

        // ctx.target è la carta adiacente che sta per essere colpita
        if (!(ctx.target is CardInstance targetCard)) return;

        var board = Owner.board;
        int myIdx  = board.IndexOf(Source);
        int tgtIdx = board.IndexOf(targetCard);
        if (myIdx < 0 || tgtIdx < 0) return;

        if (Mathf.Abs(myIdx - tgtIdx) == 1)
        {
            targetCard.tempBlockBonus += shieldValue;
            Source.PushHint($"→ scudo {shieldValue}");
        }
    }
}
