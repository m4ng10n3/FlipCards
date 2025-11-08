// Scripts/Core/TurnQueue.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public class TurnQueue
{
    public struct Item
    {
        public int seq;               // numero progressivo globale
        public GameEventType type;    // tipo evento
        public EventContext ctx;      // contesto originale
        public DateTime time;         // timestamp
        public string note;           // messaggio umano
    }

    int _nextSeq = 1;
    readonly Queue<Item> _queue = new Queue<Item>();
    public readonly List<Item> history = new List<Item>(256);

    public void Enqueue(GameEventType t, EventContext ctx, string note = null)
    {
        var it = new Item { seq = _nextSeq++, type = t, ctx = ctx, time = DateTime.Now, note = note };
        _queue.Enqueue(it);
        history.Add(it);
    }

    public bool TryDequeue(out Item it)
    {
        if (_queue.Count > 0) { it = _queue.Dequeue(); return true; }
        it = default; return false;
    }

    public void Clear() { _queue.Clear(); history.Clear(); _nextSeq = 1; }
    public int Count => _queue.Count;
}
