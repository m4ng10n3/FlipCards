using System;
using System.Collections.Generic;

public static class Logger
{
    // ----- Sink assegnato dal GameManager -----
    static Action<string> _sink;

    // Buffer di bootstrap: conserva i log emessi prima che il sink sia pronto (es. scene load)
    static readonly List<string> _buffer = new List<string>(256);

    // (Opzionale) anche in Console di Unity? utile in Editor
    public static bool MirrorToUnityConsole = false;

    // Call una volta in GameManager.Awake()
    public static void SetSink(Action<string> sink)
    {
        _sink = sink;
        if (_sink != null && _buffer.Count > 0)
        {
            // scarica tutto ciò che è stato loggato prima dell'Awake
            foreach (var m in _buffer) _sink(m);
            _buffer.Clear();
        }
    }

    // ---------- API comode da usare ovunque ----------
    public static void Info(string msg) => Emit(msg);
    public static void Warn(string msg) => Emit("[WARN] " + msg);
    public static void Error(string msg) => Emit("[ERROR] " + msg);

    // Overload con formato (usi stile string.Format)
    public static void Info(string fmt, params object[] args) => Emit(string.Format(fmt, args));
    public static void Warn(string fmt, params object[] args) => Emit("[WARN] " + string.Format(fmt, args));
    public static void Error(string fmt, params object[] args) => Emit("[ERROR] " + string.Format(fmt, args));

    // (Opzionale) categorie/tag: Logger.Cat("AI").Info("...") ecc.
    public static Category Cat(string name) => new Category(name);

    // ---------- Internals ----------
    static void Emit(string msg)
    {
        if (MirrorToUnityConsole)
            UnityEngine.Debug.Log(msg); // non sostituisce il sink, solo mirroring in Editor

        if (_sink != null) _sink(msg);
        else _buffer.Add(msg);
    }

    // Helper per categorie (facoltativo)
    public readonly struct Category
    {
        readonly string _name;
        internal Category(string name) => _name = name;
        public void Info(string msg) => Emit($"[{_name}] {msg}");
        public void Warn(string msg) => Emit($"[WARN][{_name}] {msg}");
        public void Error(string msg) => Emit($"[ERROR][{_name}] {msg}");

        public void Info(string fmt, params object[] a) => Emit($"[{_name}] " + string.Format(fmt, a));
        public void Warn(string fmt, params object[] a) => Emit($"[WARN][{_name}] " + string.Format(fmt, a));
        public void Error(string fmt, params object[] a) => Emit($"[ERROR][{_name}] " + string.Format(fmt, a));
    }
}
