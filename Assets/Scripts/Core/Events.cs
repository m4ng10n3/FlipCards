using System;
using System.Collections.Generic;
using UnityEngine;

public enum GameEventType
{
    TurnStart, TurnEnd, Flip, AttackDeclared,
    IncomingAttack,            // <-- rinomina da ResolveIncomingDamage
    CombatResolved,            // <-- nuovo evento unico di esito
    CardPlayed, /*PhaseChanged,*/ // (opzionale: se non lo usi, puoi rimuoverlo)
    Info
}

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

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _subs.Clear();
#endif

    public static void Subscribe(GameEventType t, Handler h)
    {
        if (h == null) return;
        if (!_subs.TryGetValue(t, out var list)) _subs[t] = list = new List<Handler>();
        if (!list.Contains(h)) list.Add(h);
    }

    public static void Unsubscribe(GameEventType t, Handler h)
    {
        if (h == null) return;
        if (_subs.TryGetValue(t, out var list)) list.Remove(h);
    }

    public static void Publish(GameEventType t, EventContext ctx)
    {
        Logger.Info(Format(t, ctx));   // logging centralizzato

        if (_subs.TryGetValue(t, out var list))
        {
            var snapshot = list.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                try { snapshot[i]?.Invoke(t, ctx); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }

    public static string Format(GameEventType t, EventContext ctx)
    {
        static string SafeName(CardInstance c) => c == null ? "null" : $"#{c.id} {c.def.cardName}";
        switch (t)
        {
            case GameEventType.TurnStart: return $"[TURN START] owner:{ctx.owner?.name}";
            case GameEventType.TurnEnd: return $"[TURN END]   owner:{ctx.owner?.name}";
            // (se non usi più le fasi, rimuovi questo case)
            case GameEventType.CardPlayed: return $"[PLAY]  {SafeName(ctx.source)}";
            case GameEventType.Flip: return $"[FLIP]  {SafeName(ctx.source)}";
            case GameEventType.AttackDeclared: return $"[ATTACK] {SafeName(ctx.source)} -> {SafeName(ctx.target)} ({ctx.amount})";
            case GameEventType.IncomingAttack: return $"[INCOMING] {SafeName(ctx.source)} -> {SafeName(ctx.target)} ({ctx.amount})";
            case GameEventType.CombatResolved:
                // amount = danno finale applicato; stato morte ricavabile da ctx.target.alive
                return $"[COMBAT] {SafeName(ctx.target)} took {ctx.amount} (HP:{ctx.target?.health})";
            case GameEventType.Info: return $"[INFO] {ctx.phase}";
            default: return $"[{t}] {ctx}";
        }
    }
}