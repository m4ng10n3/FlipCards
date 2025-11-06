using UnityEngine;

public class CardInstance
{
    public CardDefinition def;
    public int health;
    public Side side;
    public bool alive => health > 0;

    public CardInstance(CardDefinition def, System.Random rng)
    {
        this.def = def;
        this.health = def.maxHealth;
        // Flip casuale all’inizio: 50/50
        this.side = rng.NextDouble() < 0.5 ? Side.Fronte : Side.Retro;
    }

    public void Flip()
    {
        side = side == Side.Fronte ? Side.Retro : Side.Fronte;
    }

    public override string ToString()
    {
        return $"{def.cardName} ({def.faction}) {side} HP:{health}";
    }
}
