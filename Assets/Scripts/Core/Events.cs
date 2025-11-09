using System;
using System.Collections.Generic;
using UnityEngine;

public enum GameEventType { TurnStart, TurnEnd, Flip, AttackDeclared, DamageDealt, CardDestroyed, CardPlayed, PhaseChanged }

[Serializable]
public struct EventContext
{
    public PlayerState owner;
    public PlayerState opponent;
    public CardInstance source;
    public CardInstance target;
    public int amount;
    public string phase;

    static string SafeName(CardInstance c)
        => c == null ? "null" : $"#{c.id} {c.def.cardName}";

    public override string ToString()
    {
        var ownerName = owner?.name ?? "null";
        var oppName = opponent?.name ?? "null";
        var src = SafeName(source);
        var tgt = SafeName(target);
        var ph = string.IsNullOrEmpty(phase) ? "none" : phase;

        return $"owner:{ownerName} opponent:{oppName} source:{src} target:{tgt} amount:{amount} phase:{ph}";
    }
}

public static class EventBus
{
    public delegate void Handler(GameEventType type, EventContext ctx);
    static readonly Dictionary<GameEventType, List<Handler>> _subs = new();

    public static void Subscribe(GameEventType t, Handler h)
    {
        if (!_subs.TryGetValue(t, out var list)) { list = new List<Handler>(); _subs[t] = list; }
        if (!list.Contains(h)) list.Add(h);
    }

    public static void Unsubscribe(GameEventType t, Handler h)
    {
        if (_subs.TryGetValue(t, out var list)) list.Remove(h);
    }

    public static void Publish(GameEventType t, EventContext ctx)
    {
        GameManagerInteractive.Logf("[EVENT] {0} | {1}", t, ctx.ToString());
        if (_subs.TryGetValue(t, out var list))
            foreach (var h in list.ToArray()) h?.Invoke(t, ctx);
    }
}