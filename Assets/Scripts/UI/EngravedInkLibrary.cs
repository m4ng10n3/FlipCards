using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Cut-out geometry of the original generated ink, stored with the UI assets.</summary>
public class EngravedInkLibrary : ScriptableObject
{
    [Serializable] public class Entry
    {
        public string key;
        public Vector2[] vertices;
        public Vector2[] uv;
        public int[] triangles;
    }
    public List<Entry> entries = new();
    static EngravedInkLibrary _active;
    public static Entry Get(string key)
    {
        if (_active == null) _active = Resources.Load<EngravedInkLibrary>("EngravedInkLibrary");
        return _active != null ? _active.entries.Find(e => e.key == key) : null;
    }
}
