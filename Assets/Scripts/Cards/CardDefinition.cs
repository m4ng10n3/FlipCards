using UnityEngine;

public class CardDefinition : MonoBehaviour
{
    [System.Serializable]
    public struct Spec
    {
        // Identit
        public string cardName;
        public Faction faction;

        // Stats base
        public int maxHealth;

        // Fronte
        public int frontDamage;
        public int frontBlockValue;

        // Retro (passivi)
        public int backDamageBonusSameFaction;
        public int backBlockBonusSameFaction;
        public int backBonusPAIfTwoRetroSameFaction;
        // dentro Spec
        public float endTurnFlipChance;

        public override string ToString() => $"{cardName} [{faction}]";
    }

    [Header("Identity")]
    public string cardName = "Card";
    public Faction faction = Faction.Sangue;

    [Header("Stats")]
    [Min(1)] public int maxHealth = 3;

    [Header("Front")]
    [Min(0)] public int frontDamage = 2;
    [Min(0)] public int frontBlockValue = 0;

    [Header("Back (passive)")]
    [Min(0)] public int backDamageBonusSameFaction = 0;
    [Min(0)] public int backBlockBonusSameFaction = 0;
    [Min(0)] public int backBonusPAIfTwoRetroSameFaction = 0;

    [Header("Behaviour")]
    [Range(0, 1f)] public float endTurnFlipChance = 0.3f;

    // Ex-BuildRuntimeDefinition: ora ritorna la Spec senza creare ScriptableObject
    public Spec BuildSpec()
    {
        return new Spec
        {
            cardName = cardName,
            faction = faction,
            maxHealth = maxHealth,
            frontDamage = frontDamage,
            frontBlockValue = frontBlockValue,
            backDamageBonusSameFaction = backDamageBonusSameFaction,
            backBlockBonusSameFaction = backBlockBonusSameFaction,
            backBonusPAIfTwoRetroSameFaction = backBonusPAIfTwoRetroSameFaction,
            endTurnFlipChance = endTurnFlipChance
        };
    }
}
