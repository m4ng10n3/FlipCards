using UnityEngine;

public static class SynergyResolver
{
    public static void Resolve(GameManager gm, PlayerState player, PlayerState ai)
    {
        if (gm == null) return;

        ApplyBladePairs(gm);
        ApplyGuardLinks(gm);
        ApplyMysticPulse(gm, player);
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

            CardInstance mystic = left.def.cardClass == CardClass.Mistico ? left :
                                  right.def.cardClass == CardClass.Mistico ? right : null;
            CardInstance retro = left.side == Side.Retro ? left :
                                 right.side == Side.Retro ? right : null;

            if (mystic == null || retro == null) continue;
            if (mystic == retro) continue;

            int gained = retro.GainCharge(1);
            int healed = triggered ? 0 : player.Heal(1);

            if (gained > 0) retro.PushHint($"+{gained} charge");
            if (healed > 0) mystic.PushHint($"+{healed} HP");

            Logger.Info($"Combo: Mystic Pulse on lanes {lane + 1}-{lane + 2}");
            triggered = true;
        }
    }
}
