using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Attach this script to an empty GameObject in the scene.
/// Optionally assign CardDefinition assets to playerDeck and aiDeck in the Inspector.
/// If left empty, demo decks are created at runtime.
/// Press Play to simulate N turns and see logs in the Console.
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("Setup cards (optional if you use CreateDemoDecks)")]
    public List<CardDefinition> playerDeck;
    public List<CardDefinition> aiDeck;

    [Header("Match parameters")]
    public int turns = 10;
    public int playerBaseAP = 3;
    public int aiBaseAP = 3; // AI then gets +1 extra AP inside its turn
    public int initialBoardCount = 3; // how many cards per side on board at start
    public int seed = 12345;

    System.Random rng;
    PlayerState player, ai;

    void Start()
    {
        rng = new System.Random(seed);
        player = new PlayerState("Player", playerBaseAP);
        ai = new PlayerState("AI", aiBaseAP);

        if ((playerDeck == null || playerDeck.Count == 0) || (aiDeck == null || aiDeck.Count == 0))
        {
            CreateDemoDecks(); // create 10 standard cards per side
        }

        // Populate initial boards
        for (int i = 0; i < initialBoardCount && i < playerDeck.Count; i++)
            player.board.Add(new CardInstance(playerDeck[i], rng));
        for (int i = 0; i < initialBoardCount && i < aiDeck.Count; i++)
            ai.board.Add(new CardInstance(aiDeck[i], rng));

        Debug.Log("=== MATCH START ===");
        Debug.Log(StateString());

        for (int t = 1; t <= turns; t++)
        {
            Debug.Log("\n--- TURN " + t + " - PLAYER ---");
            PlayerTurn();
            if (IsGameOver()) break;

            Debug.Log("\n--- TURN " + t + " - AI ---");
            AITurn();
            if (IsGameOver()) break;
        }

        Debug.Log("\n=== MATCH END ===");
        int diff = (ai.hp - player.hp);
        string result = diff > 0 ? "AI AHEAD" : diff < 0 ? "PLAYER AHEAD" : "TIE";
        Debug.Log("Score: PlayerHP " + player.hp + " vs AIHP " + ai.hp + " | Diff (AI-Player) = " + diff + " -> " + result);
    }

    void PlayerTurn()
    {
        // Reset AP + passive PA from retro synergies
        player.ResetAP(playerBaseAP + GameRules.PassiveBonusPA(player));

        // Random flip 50% of own cards
        foreach (var c in player.board)
            if (c.alive && rng.NextDouble() < 0.5) c.Flip();

        // Simple auto policy:
        // 1) If we have exactly 1 retro of a faction, force flip to reach 2 retros (to enable PA bonus)
        if (player.actionPoints > 0)
        {
            var needFaction = NeedSecondRetroFaction(player);
            if (needFaction != null)
            {
                var candidate = player.board
                    .Where(c => c.alive && c.side == Side.Fronte && c.def.faction == needFaction.Value)
                    .OrderByDescending(c => c.def.backDamageBonusSameFaction + c.def.backBlockBonusSameFaction + c.def.backBonusPAIfTwoRetroSameFaction)
                    .FirstOrDefault();
                if (candidate != null)
                {
                    candidate.Flip();
                    player.actionPoints -= 1;
                    Debug.Log("Player forces flip on " + candidate.def.cardName + " -> " + candidate.side);
                }
            }
        }

        // 2) Attack while AP remains using front/attack cards
        while (player.actionPoints > 0)
        {
            var attackers = player.board.Where(c => c.alive && c.side == Side.Fronte && c.def.frontType == FrontType.Attacco).ToList();
            if (attackers.Count == 0) break;
            var atk = attackers[rng.Next(attackers.Count)];

            // Preferred target: a retro card to damage the enemy player
            CardInstance target = ai.board.FirstOrDefault(c => c.alive && c.side == Side.Retro);
            if (target == null)
            {
                // otherwise front card with lowest HP
                target = ai.board.Where(c => c.alive && c.side == Side.Fronte).OrderBy(c => c.health).FirstOrDefault();
            }
            if (target == null) break;

            GameRules.Attack(player, ai, atk, target);
            player.actionPoints -= 1;
        }

        Debug.Log(StateString());
    }

    void AITurn()
    {
        // Reset AP + passive PA from retro synergies
        ai.ResetAP(aiBaseAP + GameRules.PassiveBonusPA(ai));
        AIController.ExecuteTurn(rng, ai, player);
        Debug.Log(StateString());
    }

    bool IsGameOver()
    {
        if (player.hp <= 0 || ai.hp <= 0)
            return true;
        return false;
    }

    string StateString()
    {
        string P(PlayerState s)
        {
            var boardStr = string.Join(" | ", s.board.Where(c => c.alive).Select(c => c.ToString()).ToArray());
            return s.name + " HP:" + s.hp + " PA:" + s.actionPoints + " | " + boardStr;
        }

        return "[PLAYER] " + P(player) + "\n[AI]     " + P(ai);
    }

    Faction? NeedSecondRetroFaction(PlayerState p)
    {
        foreach (Faction f in System.Enum.GetValues(typeof(Faction)))
        {
            int cnt = p.CountRetro(f);
            if (cnt == 1) return f;
        }
        return null;
    }

    // Creates 10 demo cards aligned with the 3-faction design
    void CreateDemoDecks()
    {
        playerDeck = new List<CardDefinition>();
        aiDeck = new List<CardDefinition>();

        // Local utility to create in-memory ScriptableObjects (not saved on disk)
        CardDefinition Make(string name, Faction f, int hp, FrontType ft, int fDmg, int fBlk, int bDmg, int bBlk, int bPA)
        {
            var cd = ScriptableObject.CreateInstance<CardDefinition>();
            cd.cardName = name; cd.faction = f; cd.maxHealth = hp;
            cd.frontType = ft; cd.frontDamage = fDmg; cd.frontBlockValue = fBlk;
            cd.backDamageBonusSameFaction = bDmg; cd.backBlockBonusSameFaction = bBlk; cd.backBonusPAIfTwoRetroSameFaction = bPA;
            return cd;
        }

        // PLAYER (6 cards)
        playerDeck.Add(Make("Lama del Culto", Faction.Sangue, 3, FrontType.Attacco, 3, 0, 1, 0, 0));
        playerDeck.Add(Make("Cantico Profano", Faction.Sangue, 3, FrontType.Attacco, 2, 0, 1, 0, 0));
        playerDeck.Add(Make("Scudo dell'Abisso", Faction.Ombra, 4, FrontType.Blocco, 0, 2, 0, 1, 0));
        playerDeck.Add(Make("Simbolo dell'Eclissi", Faction.Ombra, 4, FrontType.Blocco, 0, 1, 0, 1, 0));
        playerDeck.Add(Make("Spirale della Cenere", Faction.Fiamma, 3, FrontType.Attacco, 2, 0, 1, 0, 1));
        playerDeck.Add(Make("Portale Vermiglio", Faction.Fiamma, 3, FrontType.Attacco, 2, 0, 1, 0, 1));

        // AI (5 cards)
        aiDeck.Add(Make("Predatore Cremisi", Faction.Sangue, 3, FrontType.Attacco, 3, 0, 1, 0, 0));
        aiDeck.Add(Make("Mano del Silenzio", Faction.Ombra, 3, FrontType.Blocco, 0, 2, 0, 1, 0));
        aiDeck.Add(Make("Custode dell'Abisso", Faction.Ombra, 4, FrontType.Blocco, 0, 1, 0, 1, 0));
        aiDeck.Add(Make("Eremita della Cenere", Faction.Fiamma, 2, FrontType.Attacco, 2, 0, 1, 0, 1));
        aiDeck.Add(Make("Veggente del Nulla", Faction.Fiamma, 3, FrontType.Attacco, 2, 0, 1, 0, 1));
    }
}
