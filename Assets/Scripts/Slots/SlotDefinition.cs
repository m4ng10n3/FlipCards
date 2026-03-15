using UnityEngine;

public class SlotDefinition : MonoBehaviour
{
    [System.Serializable]
    public struct Spec
    {
        public string SlotName;
        public Faction faction;
        public int maxHealth;
        public int atkDamage;
        public int blockFront;
        public int blockRetro;
        public Side[] flipPattern;
        public int reelFrameIndex;

        public override string ToString() => $"{SlotName} [{faction}]";
    }

    [Header("Identity")]
    public string SlotName = "Slot";
    public Faction faction = Faction.A;

    [Header("Stats")]
    [Min(1)] public int maxHealth = 5;
    [Min(1)] public int atkDamage = 2;
    [Min(0)] public int blockFront = 1;
    [Min(0)] public int blockRetro = 4;

    [Header("Flip Pattern (AI Behaviour)")]
    [Tooltip("Sequenza di lati che lo slot segue ogni turno. Vuoto = sempre Fronte.")]
    public Side[] flipPattern;

    [Header("Reel Animation")]
    [Tooltip("Indice del frame nello sprite sheet del reel (0 = prima riga dall'alto).")]
    [Min(0)] public int reelFrameIndex = 0;

    public Spec BuildSpec()
    {
        return new Spec
        {
            SlotName   = SlotName,
            faction    = faction,
            maxHealth  = maxHealth,
            atkDamage  = atkDamage,
            blockFront = blockFront,
            blockRetro = blockRetro,
            flipPattern = flipPattern != null && flipPattern.Length > 0
                ? (Side[])flipPattern.Clone()
                : null,
            reelFrameIndex = reelFrameIndex,
        };
    }
}
