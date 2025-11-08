using UnityEngine;

public interface ICondition
{
    bool Evaluate(EventContext ctx);
}

public interface IGameAction
{
    /// Return a short note for the log (optional). Null/empty means no note.
    string Execute(EventContext ctx);
}

public enum RuleTrigger { OnTurnStart, OnTurnEnd, OnFlip, OnAttackDeclared, OnDamageDealt, OnCardDestroyed, OnPhaseChanged }
