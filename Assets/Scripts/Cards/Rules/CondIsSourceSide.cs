using UnityEngine;

[CreateAssetMenu(menuName="Cards/Cond/IsSourceSide")]
public class CondIsSourceSide : ScriptableObject, ICondition
{
    public Side side;
    public bool Evaluate(EventContext ctx) => ctx.source != null && ctx.source.side == side;
}
