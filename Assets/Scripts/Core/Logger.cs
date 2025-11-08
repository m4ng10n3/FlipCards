// Core/Logger.cs
using System;
using UnityEngine;

public static class Logger
{
    public static Action<string> Sink;

    [RuntimeInitializeOnLoadMethod]
    static void Boot() { Sink = DefaultSink; }

    static void DefaultSink(string msg)
    {
        var gm = GameManagerInteractive.Instance;
        if (gm != null) gm.AppendLog(msg);
        // NO Debug.Log: logs must go only to UI
    }

    public static void Info(string msg)   => Sink?.Invoke(msg);
    public static void Warn(string msg)   => Sink?.Invoke("[WARN] " + msg);
    public static void Error(string msg)  => Sink?.Invoke("[ERROR] " + msg);
}
