// EventLogger.cs
using UnityEngine;

public class EventLogger : MonoBehaviour
{
    public static EventLogger Instance;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        foreach (GameEventType t in System.Enum.GetValues(typeof(GameEventType)))
            EventBus.Subscribe(t, OnEvent);
    }

    void OnDestroy()
    {
        if (Instance == this)
            foreach (GameEventType t in System.Enum.GetValues(typeof(GameEventType)))
                EventBus.Unsubscribe(t, OnEvent);
    }

    void OnEvent(GameEventType t, EventContext ctx)
    {
        var msg = Format(t, ctx);
        Logger.Info(msg); // passa sempre dal logger
    }

    public static string SafeName(CardInstance c) => c == null ? "null" : $"#{c.id} {c.def.cardName}";

    public static string Format(GameEventType t, EventContext ctx)
    {
        switch (t)
        {
            case GameEventType.TurnStart: return $"[TURN START] owner:{ctx.owner?.name}";
            case GameEventType.TurnEnd: return $"[TURN END]   owner:{ctx.owner?.name}";
            case GameEventType.PhaseChanged: return $"[PHASE] {ctx.phase}";
            case GameEventType.CardPlayed: return $"[PLAY]  {SafeName(ctx.source)}";
            case GameEventType.Flip: return $"[FLIP]  {SafeName(ctx.source)}";
            case GameEventType.AttackDeclared: return $"[ATTACK] {SafeName(ctx.source)} -> {SafeName(ctx.target)}";
            case GameEventType.DamageDealt:
                return ctx.target != null
                    ? $"[DAMAGE] {SafeName(ctx.target)} -{ctx.amount}"
                    : $"[DAMAGE] Player {ctx.opponent?.name} -{ctx.amount}";
            case GameEventType.CardDestroyed: return $"[DESTROY] {SafeName(ctx.target)}";
            default: return $"[{t}]";
        }
    }

}
