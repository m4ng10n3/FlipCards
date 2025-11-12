using UnityEngine;

public class SlotDefinition : MonoBehaviour
{
    [System.Serializable]
    public struct Spec
    {
        public string SlotName;
        public Faction faction;
        public int maxHealth;

        public override string ToString() => $"{SlotName} [{faction}]";
    }

    [Header("Identity")]
    public string SlotName = "Slot";
    public Faction faction = Faction.A;

    [Header("Stats")]
    [Min(1)] public int maxHealth = 3;

    public Spec BuildSpec()
    {
        return new Spec
        {
            SlotName = SlotName,
            faction = faction,
            maxHealth = maxHealth,
        };
    }
}
