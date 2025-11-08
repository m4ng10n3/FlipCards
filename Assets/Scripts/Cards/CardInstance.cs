using UnityEngine;

public class CardInstance
{
    public CardDefinitionInline.Spec def;
    public int health;
    public Side side;
    public bool alive => health > 0;
    public readonly int id;
    static int _nextId = 1;

    public CardInstance(CardDefinitionInline.Spec def, System.Random rng)
    {
        this.def = def;
        this.health = def.maxHealth;
        // Flip casuale all'inizio: 50/50
        this.side = rng.NextDouble() < 0.5 ? Side.Fronte : Side.Retro;
        this.id = _nextId++;
    }

    public void Flip()
    {
        side = side == Side.Fronte ? Side.Retro : Side.Fronte;
    }

    public override string ToString() => $"#{id} {def.cardName} ({def.faction}) {side} HP:{health}";

}
