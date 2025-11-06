using UnityEngine;

[CreateAssetMenu(fileName = "CardDefinition", menuName = "Bifronte/Card Definition")]
public class CardDefinition : ScriptableObject
{
    [Header("Identità")]
    public string cardName;
    public Faction faction;

    [Header("Statistiche base")]
    [Min(1)] public int maxHealth = 3;

    [Header("FRONTE (azione tattica)")]
    public FrontType frontType = FrontType.Attacco;
    [Tooltip("Danno inflitto quando si attacca in fronte")]
    [Min(0)] public int frontDamage = 2;
    [Tooltip("Valore di blocco cumulativo quando è in fronte")]
    [Min(0)] public int frontBlockValue = 0;

    [Header("RETRO (passivo / moltiplicatore)")]
    [Tooltip("+danno alle carte della stessa fazione mentre questa carta è in retro")]
    [Min(0)] public int backDamageBonusSameFaction = 0;
    [Tooltip("+blocco alle carte della stessa fazione mentre questa carta è in retro")]
    [Min(0)] public int backBlockBonusSameFaction = 0;
    [Tooltip("PA bonus se hai almeno 2 carte di questa fazione in retro")]
    [Min(0)] public int backBonusPAIfTwoRetroSameFaction = 0;

    public override string ToString() => $"{cardName} [{faction}]";
}
