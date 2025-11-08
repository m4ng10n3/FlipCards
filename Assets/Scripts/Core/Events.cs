using System;
using UnityEngine;

public enum GameEventType { TurnStart, TurnEnd, Flip, AttackDeclared, DamageDealt, CardDestroyed, CardPlayed, PhaseChanged }

public struct EventContext
{
    public PlayerState owner;
    public PlayerState opponent;
    public CardInstance source;
    public CardInstance target;
    public int amount;
    public string phase;
}
