using UnityEngine;

public class SlotDefinition : MonoBehaviour
{
    [System.Serializable]
    public struct Spec
    {
        public string SlotName;
        public Faction faction;
        public int maxHealth;

        // Statistiche di combattimento
        public int atkDamage;       // danno base che lo slot infligge
        public int blockFront;      // block quando è in Fronte
        public int blockRetro;      // block quando è in Retro (più alto)

        // Pattern di flip AI: sequenza di Side che lo slot segue ogni turno.
        // Se null o vuoto, lo slot resta sempre in Fronte.
        public Side[] flipPattern;

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
        };
    }
}
