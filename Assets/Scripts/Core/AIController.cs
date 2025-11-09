using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public static class AIController
{
    /// Semplice euristica:
    /// 1) Se pu, spende 1 PA per forzare il flip di una sua carta in modo da massimizzare sinergia retro
    /// 2) Attacca: preferisce bersagliare una carta in retro per danno al player,
    ///    altrimenti elimina la carta fronte con HP pi basso
    
    public static void ExecuteTurn(System.Random rng, PlayerState ai, PlayerState player)
    {
        //GameManagerInteractive.Log("[AI] Begin turn");
        Logger.Info("[AI] Begin turn");
        // IA avvantaggiata: +1 PA sempre
        ai.actionPoints += 1;

        // Flip casuale delle proprie carte (50%)
        foreach (var c in ai.board)
            if (c.alive && rng.NextDouble() < 0.5) c.Flip();

        // Tenta un flip mirato per ottenere pi retro di una fazione
        if (ai.actionPoints > 0)
        {
            var bestFaction = ChooseBestRetroFaction(ai);
            var candidate = ai.board
                .Where(c => c.alive && c.side == Side.Fronte && c.def.faction == bestFaction)
                .OrderByDescending(c => c.def.backDamageBonusSameFaction + c.def.backBlockBonusSameFaction + c.def.backBonusPAIfTwoRetroSameFaction)
                .FirstOrDefault();
            //Logger.Info("[AI] Plan flip for faction {0}", bestFaction);
            Logger.Info("[AI] Plan flip for faction {0}", bestFaction);
            if (candidate != null)
            {
                candidate.Flip();
                ai.actionPoints -= 1;
                Logger.Info("[AI] Flip {0} -> {1}", candidate.def.cardName, candidate.side);
            }
        }

        // Attacchi fino a esaurire PA (azioni = attacchi)
        while (ai.actionPoints > 0)
        {
            var attackers = ai.board.Where(c => c.alive && c.side == Side.Fronte && c.def.frontType == FrontType.Attacco).ToList();
            if (attackers.Count == 0) break;

            var atk = attackers[rng.Next(attackers.Count)];

            // Target preferito: retro per colpire player
            CardInstance target = player.board.FirstOrDefault(c => c.alive && c.side == Side.Retro);
            if (target == null)
            {
                // Altrimenti carta fronte pi debole
                target = player.board
                    .Where(c => c.alive && c.side == Side.Fronte)
                    .OrderBy(c => c.health)
                    .FirstOrDefault();
            }
            if (target == null) break; // niente da attaccare
            Logger.Info("[AI] Choose attack: {0} -> {1}", atk.def.cardName, target.def.cardName);

            atk.Attack(player, ai, target);
            ai.actionPoints -= 1;
        }
    }

    static Faction ChooseBestRetroFaction(PlayerState ai)
    {
        // Seleziona la fazione con pi carte gi in retro (per massimizzare i moltiplicatori)
        var groups = ai.board.Where(c => c.alive && c.side == Side.Retro)
            .GroupBy(c => c.def.faction)
            .OrderByDescending(g => g.Count());
        return groups.Any() ? groups.First().Key : (Faction)Random.Range(0, 3);
    }
}
