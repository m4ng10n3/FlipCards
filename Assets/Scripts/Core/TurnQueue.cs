// Core/TurnQueue.cs
using System;
using System.Collections.Generic;

public class TurnQueue
{
    public enum ItemState { Enqueued, Executing, Done, Cancelled }

    public struct Item
    {
        public int seq;
        public GameEventType type;
        public EventContext ctx;
        public DateTime time;
        public string note;
        public string producer;
        public string consumer;
        public ItemState state;
    }

    readonly Queue<Item> _queue = new Queue<Item>();
    readonly List<Item> history = new List<Item>();
    int _nextSeq = 1;

    public IReadOnlyList<Item> History => history;

    public int Enqueue(GameEventType t, EventContext ctx, string note, string producer = null)
    {
        var it = new Item {
            seq = _nextSeq++,
            type = t,
            ctx = ctx,
            time = DateTime.Now,
            note = note,
            producer = producer,
            state = ItemState.Enqueued
        };
        _queue.Enqueue(it);
        history.Add(it);
        Logger.Info($"[QUEUE] +{it.seq} {note}" + (string.IsNullOrEmpty(producer) ? "" : $" <{producer}>"));
        return it.seq;
    }

    public bool TryDequeue(out Item it)
    {
        if (_queue.Count > 0)
        {
            it = _queue.Dequeue();
            it.state = ItemState.Executing;
            UpdateHistory(it);
            Logger.Info($"[QUEUE] >{it.seq} EXEC {it.note}");
            return true;
        }
        it = default;
        return false;
    }

    public void MarkDone(int seq, string consumer = null)
    {
        for (int i = history.Count - 1; i >= 0; --i)
        {
            if (history[i].seq == seq)
            {
                var it = history[i];
                it.state = ItemState.Done;
                if (!string.IsNullOrEmpty(consumer)) it.consumer = consumer;
                history[i] = it;
                Logger.Info($"[QUEUE] ✓{seq} DONE {it.note}" + (string.IsNullOrEmpty(consumer) ? "" : $" by {consumer}"));
                return;
            }
        }
    }

    void UpdateHistory(Item it)
    {
        for (int i = history.Count - 1; i >= 0; --i)
            if (history[i].seq == it.seq) { history[i] = it; return; }
    }

    public void Clear() { _queue.Clear(); history.Clear(); _nextSeq = 1; }
    public int Count => _queue.Count;
}
