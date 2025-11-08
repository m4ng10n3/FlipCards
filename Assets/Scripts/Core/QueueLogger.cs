// Core/QueueLogger.cs
using UnityEngine;

public class QueueLogger : MonoBehaviour
{
    public static QueueLogger Instance;
    public TurnQueue turnQueue = new TurnQueue();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        foreach (GameEventType t in System.Enum.GetValues(typeof(GameEventType)))
            EventBus.Subscribe(t, OnEvent);
    }

    string SafeName(CardInstance c) => c == null ? "null" : $"#{c.id} {c.def.cardName}";

    string Format(GameEventType t, EventContext ctx)
    {
        switch (t)
        {
            case GameEventType.TurnStart:     return $"[TURN] Inizio turno di {ctx.owner?.name} | PA:{ctx.owner?.actionPoints}";
            case GameEventType.TurnEnd:       return $"[TURN] Fine turno di {ctx.owner?.name}";
            case GameEventType.PhaseChanged:  return $"[PHASE] {ctx.phase}";
            case GameEventType.CardPlayed:    return $"[PLAY] {SafeName(ctx.source)}";
            case GameEventType.Flip:          return $"[FLIP]  {SafeName(ctx.source)}";
            case GameEventType.AttackDeclared:return $"[ATTACK] {SafeName(ctx.source)} -> {SafeName(ctx.target)}";
            case GameEventType.DamageDealt:
                return ctx.target != null
                    ? $"[DAMAGE] {SafeName(ctx.target)} -{ctx.amount}"
                    : $"[DAMAGE] Player {ctx.opponent?.name} -{ctx.amount}";
            case GameEventType.CardDestroyed: return $"[DESTROY] {SafeName(ctx.target)}";
            default: return $"[{t}]";
        }
    }

    void OnEvent(GameEventType t, EventContext ctx)
    {
        Logger.Info(Format(t, ctx));

        switch (t)
        {
            case GameEventType.TurnStart:
                turnQueue.Enqueue(t, ctx, $"Inizio turno {ctx.owner?.name}", "System/Turn");
                Logger.Info("[HINT] Azioni: [Flip:1PA] [Attacca:1PA] [Fine Turno]");
                break;
            case GameEventType.Flip:
                turnQueue.Enqueue(t, ctx, $"Flip {SafeName(ctx.source)}", SafeName(ctx.source));
                break;
            case GameEventType.AttackDeclared:
                turnQueue.Enqueue(t, ctx, $"Attacco {SafeName(ctx.source)} → {SafeName(ctx.target)}", SafeName(ctx.source));
                break;
            case GameEventType.DamageDealt:
                if (ctx.target != null)
                    turnQueue.Enqueue(t, ctx, $"Danno {ctx.amount} a {SafeName(ctx.target)}", SafeName(ctx.source));
                else
                    turnQueue.Enqueue(t, ctx, $"Danno {ctx.amount} a Player {ctx.opponent?.name}", SafeName(ctx.source));
                break;
            case GameEventType.CardDestroyed:
                turnQueue.Enqueue(t, ctx, $"Distrutta {SafeName(ctx.target)}", SafeName(ctx.source));
                break;
        }
    }

    public static TurnQueue Queue => Instance?.turnQueue;
}
