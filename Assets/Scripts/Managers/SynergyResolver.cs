using UnityEngine;

public static class SynergyResolver
{
    public static void Resolve(GameManager gm, PlayerState player, PlayerState ai)
    {
        if (gm == null) return;

        ApplyBladePairs(gm);
        ApplyGuardLinks(gm);
        ApplyMysticPulse(gm, player);
        for (int lane = 0; lane < gm.playerBoardRoot.childCount; lane++)
        {
            var card = gm.GetPlayerCardAtLane(lane);
            if (card == null || !Resonates(gm, lane)) continue;
            if (card.side == Side.Fronte) card.tempAtkBonus++;
            else card.tempBlockBonus++;
            card.PushHint(card.side == Side.Fronte ? "RISONANZA +1 ATK" : "RISONANZA +1 BLOCCO");
        }
    }

    static void ApplyBladePairs(GameManager gm)
    {
        for (int lane = 0; lane < gm.playerBoardRoot.childCount - 1; lane++)
        {
            var left = gm.GetPlayerCardAtLane(lane);
            var right = gm.GetPlayerCardAtLane(lane + 1);
            if (left == null || right == null) continue;
            if (left.side != Side.Fronte || right.side != Side.Fronte) continue;
            if (left.def.cardClass != CardClass.Assalto || right.def.cardClass != CardClass.Assalto) continue;

            left.tempAtkBonus += 1;
            right.tempAtkBonus += 1;
            left.PushHint("Blade Pair +1");
            right.PushHint("Blade Pair +1");
            Logger.Info($"Combo: Blade Pair on lanes {lane + 1}-{lane + 2}");
        }
    }

    static void ApplyGuardLinks(GameManager gm)
    {
        for (int lane = 0; lane < gm.playerBoardRoot.childCount - 1; lane++)
        {
            var left = gm.GetPlayerCardAtLane(lane);
            var right = gm.GetPlayerCardAtLane(lane + 1);
            if (left == null || right == null) continue;

            bool hasGuard = left.def.cardClass == CardClass.Guardia || right.def.cardClass == CardClass.Guardia;
            bool hasRetro = left.side == Side.Retro || right.side == Side.Retro;
            if (!hasGuard || !hasRetro) continue;

            left.tempBlockBonus += 1;
            right.tempBlockBonus += 1;
            left.PushHint("Guard Link +1");
            right.PushHint("Guard Link +1");
            Logger.Info($"Combo: Guard Link on lanes {lane + 1}-{lane + 2}");
        }
    }

    static void ApplyMysticPulse(GameManager gm, PlayerState player)
    {
        bool triggered = false;

        for (int lane = 0; lane < gm.playerBoardRoot.childCount - 1; lane++)
        {
            var left = gm.GetPlayerCardAtLane(lane);
            var right = gm.GetPlayerCardAtLane(lane + 1);
            if (left == null || right == null) continue;

            var retro = MysticTarget(left, right);
            if (retro == null) continue;
            var mystic = retro == left ? right : left;

            int gained = retro.GainCharge(1);
            int healed = triggered ? 0 : player.Heal(1);

            if (gained > 0) retro.PushHint($"+{gained} charge");
            if (healed > 0) mystic.PushHint($"+{healed} HP");

            Logger.Info($"Combo: Mystic Pulse on lanes {lane + 1}-{lane + 2}");
            triggered = true;
        }
    }
    public static CardInstance MysticTarget(CardInstance left, CardInstance right)
    {
        if (left == null || right == null) return null;
        if (left.def.cardClass == CardClass.Mistico && right.side == Side.Retro) return right;
        if (right.def.cardClass == CardClass.Mistico && left.side == Side.Retro) return left;
        return null;
    }

    public static bool Resonates(GameManager gm, int lane)
    {
        var card = gm.GetPlayerCardAtLane(lane);
        var slot = gm.GetEnemySlotAtLane(lane);
        return card != null && slot != null && card.def.faction == slot.def.faction;
    }

    // Shared by the forecast and combat. These are the visible adjacency and
    // lane bonuses; event-driven abilities are still resolved during battle.
    public static int AttackBonus(GameManager gm, int lane)
    {
        var card = gm.GetPlayerCardAtLane(lane);
        if (card == null || card.side != Side.Fronte) return 0;
        int bonus = Resonates(gm, lane) ? 1 : 0;
        for (int i = lane - 1; i <= lane + 1; i += 2)
        {
            var other = gm.GetPlayerCardAtLane(i);
            if (other != null && other.side == Side.Fronte &&
                other.def.cardClass == CardClass.Assalto && card.def.cardClass == CardClass.Assalto) bonus++;
        }
        return bonus;
    }

    public static int BlockBonus(GameManager gm, int lane)
    {
        var card = gm.GetPlayerCardAtLane(lane);
        if (card == null) return 0;
        int bonus = card.side == Side.Retro && Resonates(gm, lane) ? 1 : 0;
        for (int i = lane - 1; i <= lane + 1; i += 2)
        {
            var other = gm.GetPlayerCardAtLane(i);
            if (other != null && (other.side == Side.Retro || card.side == Side.Retro) &&
                (other.def.cardClass == CardClass.Guardia || card.def.cardClass == CardClass.Guardia)) bonus++;
        }
        return bonus;
    }

}
