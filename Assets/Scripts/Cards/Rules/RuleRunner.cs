using UnityEngine;

public class RuleRunner : MonoBehaviour
{
    public Rule[] rules;
    CardView view;
    CardInstance source;
    GameManagerInteractive gm;

    void Awake()
    {
        view = GetComponent<CardView>();
        gm = GameManagerInteractive.Instance;
        source = view != null ? view.instance : null;

        Subscribe(true);
    }

    void OnDestroy()
    {
        Subscribe(false);
    }

    void Subscribe(bool on)
    {
        foreach (GameEventType t in System.Enum.GetValues(typeof(GameEventType)))
        {
            if (on) EventBus.Subscribe(t, OnEvent);
            else    EventBus.Unsubscribe(t, OnEvent);
        }
    }

    void OnEvent(GameEventType t, EventContext ctx)
    {
        var trig = Map(t);
        if (trig == null || rules == null) return;

        // Enrich ctx if needed
        if (ctx.source == null) ctx.source = source;
        if (ctx.owner == null && gm != null) ctx.owner = gm.player;
        if (ctx.opponent == null && gm != null) ctx.opponent = gm.ai;

        foreach (var r in rules)
        {
            if (r == null) continue;
            if (r.trigger == trig.Value && r.Evaluate(ctx))
                r.Run(ctx);
        }
    }

    RuleTrigger? Map(GameEventType t)
    {
        switch (t)
        {
            case GameEventType.TurnStart: return RuleTrigger.OnTurnStart;
            case GameEventType.TurnEnd: return RuleTrigger.OnTurnEnd;
            case GameEventType.Flip: return RuleTrigger.OnFlip;
            case GameEventType.AttackDeclared: return RuleTrigger.OnAttackDeclared;
            case GameEventType.DamageDealt: return RuleTrigger.OnDamageDealt;
            case GameEventType.CardDestroyed: return RuleTrigger.OnCardDestroyed;
            case GameEventType.PhaseChanged: return RuleTrigger.OnPhaseChanged;
            default: return null;
        }
    }
}
