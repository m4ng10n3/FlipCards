// Assets/Scripts/Core/EventLogger.cs
using UnityEngine;
using System;

public class EventLogger : MonoBehaviour
{
    public static EventLogger Instance;
    public TurnQueue turnQueue = new TurnQueue();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        foreach (GameEventType t in Enum.GetValues(typeof(GameEventType)))
            EventBus.Subscribe(t, OnEvent);
    }

    void OnDestroy()
    {
        foreach (GameEventType t in Enum.GetValues(typeof(GameEventType)))
            EventBus.Unsubscribe(t, OnEvent);
    }

    void OnEvent(GameEventType t, EventContext ctx)
    {
        string msg = Format(t, ctx);
        turnQueue.Enqueue(t, ctx, msg);
        Debug.Log(msg);
        GameManagerInteractive.TryAppendLogStatic(msg);
    }

    // EventLogger.cs
    string Name(CardInstance c)
        => c == null
            ? "-"
            : $"#{c.id} {(!string.IsNullOrEmpty(c.def.cardName) ? c.def.cardName : "Card")} HP:{c.health}";


    // Evitiamo proprietà ignote di PlayerState: mostriamo solo se è presente
    string OwnerPresent(object p) => p == null ? "null" : "set";

    string Format(GameEventType t, EventContext ctx)
    {
        switch (t)
        {
            case GameEventType.TurnStart: return $"[TURN START] owner:{OwnerPresent(ctx.owner)}";
            case GameEventType.TurnEnd: return $"[TURN END] owner:{OwnerPresent(ctx.owner)}";
            case GameEventType.PhaseChanged: return $"[PHASE] {ctx.phase}";
            case GameEventType.CardPlayed: return $"[PLAY] {Name(ctx.source)}";
            case GameEventType.Flip: return $"[FLIP] {Name(ctx.source)}";
            case GameEventType.AttackDeclared: return $"[ATTACK] {Name(ctx.source)} -> {Name(ctx.target)}";
            case GameEventType.DamageDealt: return $"[DAMAGE] {Name(ctx.target)} -{ctx.amount}";
            case GameEventType.CardDestroyed: return $"[DESTROY] {Name(ctx.source)}";
            default: return $"[EVENT {t}]";
        }
    }
}
