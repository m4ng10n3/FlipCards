using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics; // per Conditional

/// <summary>
/// Regole centrali:
/// - Bonus retro = moltiplicatori di fazione
/// - Blocco = riduzione danni al player quando si colpisce una carta in retro
/// + Hook eventi per abilit (AttackDeclared, DamageDealt, CardDestroyed)
/// </summary>
/// 

public static class GameRules
{
    /// Calcola bonus danno per una carta in fronte derivante da carte retro alleate della stessa fazione.
    public static int DamageBonusFromRetro(PlayerState owner, Faction faction)
    {
        int bonus = 0;
        foreach (var ci in owner.board)
        {
            if (!ci.alive) continue;
            if (ci.side == Side.Retro && ci.def.faction == faction)
                bonus += ci.def.backDamageBonusSameFaction;
        }
        GameManagerInteractive.Logf($"DamageBonusFromRetro(owner:{owner.name}, faction:{faction}) = {bonus}");
        return bonus;
    }

    /// Calcola blocco cumulativo lato fronte del difensore (sopprime danno al player quando si colpisce una carta in retro)
    public static int TotalFrontBlock(PlayerState defender)
    {
        int total = 0;
        foreach (var ci in defender.board)
        {
            if (!ci.alive) continue;
            if (ci.side == Side.Fronte && ci.def.frontType == FrontType.Blocco)
            {
                // Anche i bonus da retro (stessa fazione) si applicano alle carte di blocco
                int retroBonus = BlockBonusFromRetro(defender, ci.def.faction);
                total += ci.def.frontBlockValue + retroBonus;
            }
        }
        GameManagerInteractive.Logf($"TotalFrontBlock(defender:{defender.name}) = {total}");
        return total;
    }

    static int BlockBonusFromRetro(PlayerState owner, Faction faction)
    {
        int b = 0;
        foreach (var ci in owner.board)
        {
            if (!ci.alive) continue;
            if (ci.side == Side.Retro && ci.def.faction == faction)
                b += ci.def.backBlockBonusSameFaction;
        }
        GameManagerInteractive.Logf($"BlockBonusFromRetro(owner:{owner.name}, faction:{faction}) = {b}");
        return b;
    }

    /// Applica PA bonus (retro: se hai almeno 2 carte retro della stessa fazione)
    public static int PassiveBonusPA(PlayerState p)
    {
        int bonus = 0;
        foreach (Faction f in System.Enum.GetValues(typeof(Faction)))
        {
            int count = p.CountRetro(f);
            if (count >= 2)
            {
                // prendi il bonus pi alto disponibile per quella fazione tra le carte retro
                int max = 0;
                foreach (var ci in p.board)
                    if (ci.alive && ci.side == Side.Retro && ci.def.faction == f)
                        if (ci.def.backBonusPAIfTwoRetroSameFaction > max)
                            max = ci.def.backBonusPAIfTwoRetroSameFaction;
                bonus += max;
            }
        }
        GameManagerInteractive.Logf($"PassiveBonusPA({p.name}) = {bonus}");
        return bonus;
    }

    /// Effettua un attacco:
    /// - se targetCard  in Fronte -> danneggia la carta
    /// - se targetCard  in Retro  -> danneggia il Player avversario (ridotto dal blocco cumulativo)
    /// + Publish: AttackDeclared, DamageDealt, CardDestroyed
    public static void Attack(PlayerState attacker, PlayerState defender, CardInstance attackerCard, CardInstance targetCard)
    {
        if (!attackerCard.alive) return;

        // Event: dichiarazione attacco (utile per abilit che modificano il danno proposto)
        EventBus.Publish(GameEventType.AttackDeclared, new EventContext
        {
            owner = attacker,
            opponent = defender,
            source = attackerCard,
            target = targetCard,
            phase = "Combat"
        });

        int dmg = attackerCard.def.frontDamage + DamageBonusFromRetro(attacker, attackerCard.def.faction);
        GameManagerInteractive.Logf($"Attack(proposed dmg) attacker:{attacker.name} card:{attackerCard.def.cardName} dmg:{dmg}");

        dmg = Mathf.Max(0, dmg);

        if (targetCard.side == Side.Fronte)
        {
            targetCard.health -= dmg;
            GameManagerInteractive.Logf($"Attack->Card target:{targetCard.def.cardName} newHP:{targetCard.health}");

            // Event: danno a carta
            EventBus.Publish(GameEventType.DamageDealt, new EventContext
            {
                owner = attacker,
                opponent = defender,
                source = attackerCard,
                target = targetCard,
                amount = dmg,
                phase = "Combat"
            });

            if (targetCard.health <= 0)
            {
                Logger.Info($"{targetCard.def.cardName}  stata distrutta!");
                targetCard.health = 0;

                // Event: carta distrutta
                EventBus.Publish(GameEventType.CardDestroyed, new EventContext
                {
                    owner = defender,          // la carta era del difensore
                    opponent = attacker,
                    source = targetCard,
                    phase = "Combat"
                });
            }
        }
        else // target in Retro -> danno al player, mitigato da blocco
        {
            int block = TotalFrontBlock(defender);
            int final = Mathf.Max(0, dmg - block);
            defender.hp -= final;
            GameManagerInteractive.Logf($"Attack->Player target:{defender.name} final:{final} (raw:{dmg} - block:{block}) newHP:{defender.hp}");

            // Event: danno al player
            EventBus.Publish(GameEventType.DamageDealt, new EventContext
            {
                owner = attacker,
                opponent = defender,
                source = attackerCard,
                target = null, // danno diretto al player
                amount = final,
                phase = "Combat"
            });
        }
    }


    // === Helper methods for generic damage application (used by Rule actions) ===


    public static void DealDamageToPlayer(PlayerState owner, PlayerState opponent, CardInstance source, int amount, string phase = null)
{
    int final = Mathf.Max(0, amount);
    opponent.hp -= final;
        GameManagerInteractive.Logf($"DealDamageToPlayer owner:{owner.name} -> {opponent.name} amount:{final} newHP:{opponent.hp} ({phase})");

        EventBus.Publish(GameEventType.DamageDealt, new EventContext
    {
        owner = owner,
        opponent = opponent,
        source = source,
        target = null,
        amount = final,
        phase = phase ?? "Damage"
    });

    if (opponent.hp <= 0)
    {
        Logger.Info($"Player {opponent.name} sconfitto!");
        EventBus.Publish(GameEventType.CardDestroyed, new EventContext { owner = owner, opponent = opponent, source = source, target = null, amount = 0, phase = phase ?? "Damage" });
    }
}

public static void DealDamageToCard(PlayerState owner, PlayerState opponent, CardInstance source, CardInstance target, int amount, string phase = null)
{
    if (target == null || !target.alive) return;
    int final = Mathf.Max(0, amount);
    target.health -= final;
    GameManagerInteractive.Logf($"DealDamageToCard owner:{owner.name} -> {target.def.cardName} amount:{final} newHP:{target.health} ({phase})");

    EventBus.Publish(GameEventType.DamageDealt, new EventContext
    {
        owner = owner,
        opponent = opponent,
        source = source,
        target = target,
        amount = final,
        phase = phase ?? "Damage"
    });

    if (!target.alive)
    {
        EventBus.Publish(GameEventType.CardDestroyed, new EventContext
        {
            owner = owner,
            opponent = opponent,
            source = source,
            target = target,
            amount = 0,
            phase = phase ?? "Damage"
        });
    }
}
}
