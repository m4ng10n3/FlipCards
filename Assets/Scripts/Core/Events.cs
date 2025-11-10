using System;
using System.Collections.Generic;
using UnityEngine;

public enum GameEventType
{
    TurnStart, TurnEnd, Flip, AttackDeclared,
    DamageDealt, CardDestroyed, CardPlayed, PhaseChanged
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

    // Subscriber per tipo evento
    static readonly Dictionary<GameEventType, List<Handler>> _subs = new();

#if UNITY_EDITOR
    // In Editor (Enter Play Mode senza domain reload), azzera lo stato statico tra sessioni.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _subs.Clear();
    }
#endif

    // Iscrive un handler a un tipo di evento.
    public static void Subscribe(GameEventType t, Handler h)
    {
        if (h == null) return;
        if (!_subs.TryGetValue(t, out var list))
        {
            list = new List<Handler>();
            _subs[t] = list;
        }
        if (!list.Contains(h)) list.Add(h);
    }

    // Disiscrive un handler dal tipo di evento.</summary>
    public static void Unsubscribe(GameEventType t, Handler h)
    {
        if (h == null) return;
        if (_subs.TryGetValue(t, out var list)) list.Remove(h);
    }

    // Pubblica un evento: logga sempre (formatter integrato) e poi notifica i subscriber.
    public static void Publish(GameEventType t, EventContext ctx)
    {
        // 1) Log formattato (sostituisce il vecchio EventLogger)
        Logger.Info(Format(t, ctx));

        // 2) Notifica i subscriber (snapshot per sicurezza durante modifiche a runtime)
        if (_subs.TryGetValue(t, out var list))
        {
            var snapshot = list.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                try { snapshot[i]?.Invoke(t, ctx); }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    }

    // Formatter centralizzato per tutti i log degli eventi.
    public static string Format(GameEventType t, EventContext ctx)
    {
        static string SafeName(CardInstance c) => c == null ? "null" : $"#{c.id} {c.def.cardName}";

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
            default: return $"[{t}] {ctx}";
        }
    }
}
