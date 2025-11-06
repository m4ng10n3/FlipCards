using System.Collections.Generic;
using UnityEngine;

public class PlayerState
{
    public string name;
    public int hp = 20;
    public int actionPoints = 3;
    public List<CardInstance> board = new List<CardInstance>();

    public PlayerState(string name, int baseAP = 3)
    {
        this.name = name;
        this.actionPoints = baseAP;
    }

    public void ResetAP(int baseAP) => actionPoints = baseAP;

    public int CountRetro(Faction f)
    {
        int c = 0;
        foreach (var ci in board)
            if (ci.side == Side.Retro && ci.def.faction == f && ci.alive) c++;
        return c;
    }

    public override string ToString()
    {
        return $"{name} HP:{hp} PA:{actionPoints} | Board:{board.Count}";
    }
}
