using UnityEngine;

public class CardDefinitionInline : MonoBehaviour
{
    [Header("Identity")]
    public string cardName = "Card";
    public Faction faction = Faction.Sangue;

    [Header("Stats")]
    [Min(1)] public int maxHealth = 3;

    [Header("Front")]
    public FrontType frontType = FrontType.Attacco;
    [Min(0)] public int frontDamage = 2;
    [Min(0)] public int frontBlockValue = 0;

    [Header("Back Bonuses")]
    [Min(0)] public int backDamageBonusSameFaction = 0;
    [Min(0)] public int backBlockBonusSameFaction = 0;
    [Min(0)] public int backBonusPAIfTwoRetroSameFaction = 0;

    public CardDefinition BuildRuntimeDefinition()
    {
        var cd = ScriptableObject.CreateInstance<CardDefinition>();
        cd.cardName = cardName;
        cd.faction = faction;
        cd.maxHealth = maxHealth;
        cd.frontType = frontType;
        cd.frontDamage = frontDamage;
        cd.frontBlockValue = frontBlockValue;
        cd.backDamageBonusSameFaction = backDamageBonusSameFaction;
        cd.backBlockBonusSameFaction = backBlockBonusSameFaction;
        cd.backBonusPAIfTwoRetroSameFaction = backBonusPAIfTwoRetroSameFaction;
        return cd;
    }
}
