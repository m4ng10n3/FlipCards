using System;
using System.Collections.Generic;
using UnityEngine;

public enum GameEventType
{
    TurnStart, TurnEnd, Flip,
    AttackDeclared,       // attaccante dichiara colpo e importo proposto
    AttackResolved,       // il difensore ha calcolato e applicato il danno
    CardPlayed,
    Info,
    Custom
}

[Serializable]
public struct EventContext
{
    public PlayerState owner;
    public PlayerState opponent;

    // ATTENZIONE: ora generici (CardInstance o SlotInstance o null)
    public object source;
    public object target;

    public int amount;
    public string phase;

    public override string ToString()
    {
        string ownerName = owner?.name ?? "null";
        string oppName = opponent?.name ?? "null";
        string src = Label(source);
        string tgt = Label(target);
        string ph = string.IsNullOrEmpty(phase) ? "none" : phase;
        return $"owner:{ownerName} opponent:{oppName} source:{src} target:{tgt} amount:{amount} phase:{ph}";
    }

    static string Label(object o)
    {
        if (o == null) return "null";
        if (o is CardInstance c) return $"Card#{c.id} {c.def.cardName}";
        if (o is SlotInstance s) return $"Slot#{s.id} {s.def.SlotName}";
        return o.ToString();
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
        static string L(object o)
        {
            if (o == null) return "null";
            if (o is CardInstance c) return $"#{c.id} {c.def.cardName}";
            if (o is SlotInstance s) return $"Slot#{s.id} {s.def.SlotName}";
            return o.ToString();
        }

        static string Hp(object o)
        {
            if (o is CardInstance c) return c.health.ToString();
            if (o is SlotInstance s) return s.health.ToString();
            return "-";
        }

        switch (t)
        {
            case GameEventType.TurnStart: return $"[TURN START] owner:{ctx.owner?.name}";
            case GameEventType.TurnEnd: return $"[TURN END]   owner:{ctx.owner?.name}";
            case GameEventType.CardPlayed: return $"[PLAY]  {L(ctx.source)}";
            case GameEventType.Flip: return $"[FLIP]  {L(ctx.source)}";
            case GameEventType.AttackDeclared:
                return $"[ATTACK] {L(ctx.source)} -> {L(ctx.target)} (proposed:{ctx.amount})";
            case GameEventType.AttackResolved:
                return $"[RESOLVED] {L(ctx.source)} -> {L(ctx.target)} (final:{ctx.amount}) HP:{Hp(ctx.target)}";
            case GameEventType.Info: return $"[INFO] {ctx.phase}";
            default: return $"[{t}] owner:{ctx.owner?.name} src:{L(ctx.source)} tgt:{L(ctx.target)} amount:{ctx.amount} phase:{ctx.phase}";
        }
    }
}
