using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Regole centrali:
/// - Bonus retro = moltiplicatori di fazione
/// - Blocco = riduzione danni al player quando si colpisce una carta in retro
/// + Hook eventi per abilità (AttackDeclared, DamageDealt, CardDestroyed)
/// </summary>
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
                // prendi il bonus più alto disponibile per quella fazione tra le carte retro
                int max = 0;
                foreach (var ci in p.board)
                    if (ci.alive && ci.side == Side.Retro && ci.def.faction == f)
                        if (ci.def.backBonusPAIfTwoRetroSameFaction > max)
                            max = ci.def.backBonusPAIfTwoRetroSameFaction;
                bonus += max;
            }
        }
        return bonus;
    }

    /// Effettua un attacco:
    /// - se targetCard è in Fronte -> danneggia la carta
    /// - se targetCard è in Retro  -> danneggia il Player avversario (ridotto dal blocco cumulativo)
    /// + Publish: AttackDeclared, DamageDealt, CardDestroyed
    public static void Attack(PlayerState attacker, PlayerState defender, CardInstance attackerCard, CardInstance targetCard)
    {
        if (!attackerCard.alive) return;

        // Event: dichiarazione attacco (utile per abilità che modificano il danno proposto)
        EventBus.Publish(GameEventType.AttackDeclared, new EventContext
        {
            owner = attacker,
            opponent = defender,
            source = attackerCard,
            target = targetCard,
            phase = "Combat"
        });

        int dmg = attackerCard.def.frontDamage + DamageBonusFromRetro(attacker, attackerCard.def.faction);
        dmg = Mathf.Max(0, dmg);

        if (targetCard.side == Side.Fronte)
        {
            targetCard.health -= dmg;
            Debug.Log($"{attacker.name} attacca {targetCard.def.cardName} per {dmg} (fronte). HP carta: {targetCard.health}");

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
                Debug.Log($"{targetCard.def.cardName} è stata distrutta!");
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
            Debug.Log($"{attacker.name} colpisce il PLAYER {defender.name} per {final} (dmg {dmg} - block {block}) bersagliando {targetCard.def.cardName} in retro.");

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
}
