using UnityEngine;

/// <summary>Measured glass positions in a single authored cabinet illustration.</summary>
public sealed class CabinetArtDefinition : ScriptableObject
{
    public string bundleId;
    public Texture2D illustration;
    public Texture2D silhouette;
    public Vector2 sourceSize;
    public Bank[] banks;
    public MeasuredRect[] windowRegions;

    // JsonUtility serializes UnityEngine.Rect's private backing fields, not its
    // x/y/width/height properties. The external manifest needs public fields.
    [System.Serializable]
    public struct MeasuredRect { public float x, y, width, height; }

    [System.Serializable]
    public struct Bank
    {
        public int lane, stat; // health, attack, guard
        public Vector2 first, step, glassRadius;
        public MeasuredRect totalRect;
    }
}
