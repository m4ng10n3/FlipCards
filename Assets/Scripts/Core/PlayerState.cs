using System.Collections.Generic;
using UnityEngine;

public class PlayerState
{
    public string name;
    public int maxHp = 20;
    public int hp = 20;
    public int actionPoints = 3;
    public List<CardInstance> board = new List<CardInstance>();

    public PlayerState(string name, int maxHp = 20, int baseAP = 3)
    {
        this.name = name;
        this.maxHp = Mathf.Max(1, maxHp);
        hp = this.maxHp;
        this.actionPoints = baseAP;
    }

    public void ResetAP(int baseAP) => actionPoints = baseAP;

    public int Heal(int amount)
    {
        int before = hp;
        hp = Mathf.Min(maxHp, hp + Mathf.Max(0, amount));
        return hp - before;
    }

    public int TakeDamage(int amount)
    {
        int before = hp;
        hp = Mathf.Max(0, hp - Mathf.Max(0, amount));
        return before - hp;
    }

    public int CountRetro(Faction f)
    {
        int c = 0;
        foreach (var ci in board)
            if (ci.side == Side.Retro && ci.def.faction == f && ci.alive) c++;
        return c;
    }

    public override string ToString()
    {
        return $"{name} HP:{hp}/{maxHp} PA:{actionPoints} | Board:{board.Count}";
    }
}
