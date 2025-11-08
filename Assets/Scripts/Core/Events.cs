using System;
using System.Collections.Generic;
using UnityEngine;

public enum GameEventType { TurnStart, TurnEnd, Flip, AttackDeclared, DamageDealt, CardDestroyed, CardPlayed, PhaseChanged }

public struct EventContext
{
    public PlayerState owner;
    public PlayerState opponent;
    public CardInstance source;
    public CardInstance target;
    public int amount;
    public string phase;
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
        if (_subs.TryGetValue(t, out var list))
            // copia per sicurezza se le abilit modificano le sottoscrizioni
            foreach (var h in list.ToArray()) h?.Invoke(t, ctx);
    }
}