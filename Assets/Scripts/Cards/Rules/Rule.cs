using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName="Cards/Rule")]
public class Rule : ScriptableObject
{
    public RuleTrigger trigger;
    public List<ScriptableObject> conditions = new List<ScriptableObject>(); // ICondition
    public List<ScriptableObject> actions    = new List<ScriptableObject>();  // IGameAction

    public bool Evaluate(EventContext ctx)
    {
        foreach (var c in conditions)
            if (c is ICondition cond && !cond.Evaluate(ctx)) return false;
        return true;
    }

    public void Run(EventContext ctx)
    {
        foreach (var a in actions)
            if (a is IGameAction act)
            {
                var note = act.Execute(ctx);
                if (!string.IsNullOrEmpty(note)) Logger.Info(note);
            }
    }
}
