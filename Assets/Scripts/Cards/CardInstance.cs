// CardInstance.cs
using UnityEngine;
using System.Linq;

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
        this.side = rng.NextDouble() < 0.5 ? Side.Fronte : Side.Retro;
        this.id = _nextId++;
    }

    public void Flip()
    {
        side = side == Side.Fronte ? Side.Retro : Side.Fronte;
    }

    public override string ToString() => $"#{id} {def.cardName} ({def.faction}) {side} HP:{health}";

    // === Nuova LOGICA locale alla carta ===

    // Bonus danno dai retro alleati della stessa fazione
    int DamageBonusFromAlliedRetro(PlayerState owner)
    {
        int bonus = 0;
        foreach (var ci in owner.board)
            if (ci.alive && ci.side == Side.Retro && ci.def.faction == def.faction)
                bonus += ci.def.backDamageBonusSameFaction;
        return bonus;
    }

    // Bonus blocco dai retro alleati della stessa fazione (lato difensore)
    static int RetroBlockBonusForFaction(PlayerState owner, Faction faction)
    {
        int b = 0;
        foreach (var ci in owner.board)
            if (ci.alive && ci.side == Side.Retro && ci.def.faction == faction)
                b += ci.def.backBlockBonusSameFaction;
        return b;
    }

    // Blocco cumulativo del difensore (somma carte in fronte di tipo Blocco + bonus da retro di pari fazione)
    static int TotalFrontBlock(PlayerState defender)
    {
        int total = 0;
        foreach (var ci in defender.board)
        {
            if (!ci.alive) continue;
            if (ci.side == Side.Fronte && ci.def.frontType == FrontType.Blocco)
                total += ci.def.frontBlockValue + RetroBlockBonusForFaction(defender, ci.def.faction);
        }
        return total;
    }

    // Danno base di attacco della carta (con i propri bonus da retro alleato)
    public int ComputeFrontDamage(PlayerState owner)
    {
        int dmg = def.frontDamage + DamageBonusFromAlliedRetro(owner);
        return Mathf.Max(0, dmg);
    }

    // Effettua un attacco: pubblica gli stessi eventi che prima arrivavano da GameRules.Attack
    public void Attack(PlayerState owner, PlayerState defender, CardInstance target)
    {
        if (!alive || target == null) return;

        // Evento: dichiarazione attacco
        EventBus.Publish(GameEventType.AttackDeclared, new EventContext
        {
            owner = owner,
            opponent = defender,
            source = this,
            target = target,
            phase = "Combat"
        });

        int dmg = ComputeFrontDamage(owner);

        if (target.side == Side.Fronte || target.side == Side.Retro)
        {
            DealDamageToCard(owner, defender, target, dmg, "Combat");
        }
        else // bersaglio in Retro -> danni al player
        {
            int block = TotalFrontBlock(defender);
            int final = Mathf.Max(0, dmg - block);
            DealDamageToPlayer(owner, defender, final, "Combat");
        }
    }

    // Applica danno a una carta (e spara gli eventi)
    public void DealDamageToCard(PlayerState owner, PlayerState opponent, CardInstance target, int amount, string phase = null)
    {
        if (target == null || !target.alive) return;
        int final = Mathf.Max(0, amount);
        target.health -= final;

        EventBus.Publish(GameEventType.DamageDealt, new EventContext
        {
            owner = owner,
            opponent = opponent,
            source = this,
            target = target,
            amount = final,
            phase = phase ?? "Damage"
        });

        if (!target.alive)
        {
            target.health = 0;
            EventBus.Publish(GameEventType.CardDestroyed, new EventContext
            {
                owner = opponent,           // la carta distrutta era dell'avversario
                opponent = owner,
                source = target,
                target = target,
                amount = 0,
                phase = phase ?? "Damage"
            });
        }
    }

    // Applica danno al player (e spara gli eventi)
    public void DealDamageToPlayer(PlayerState owner, PlayerState opponent, int amount, string phase = null)
    {
        int final = Mathf.Max(0, amount);
        opponent.hp -= final;

        EventBus.Publish(GameEventType.DamageDealt, new EventContext
        {
            owner = owner,
            opponent = opponent,
            source = this,
            target = null,
            amount = final,
            phase = phase ?? "Damage"
        });
    }
}
