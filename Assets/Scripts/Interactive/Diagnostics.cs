// Scripts/Core/Diagnostics.cs
using System.Linq;
using UnityEngine;

public static class Diagnostics
{
    public static void Run(GameManager gm)
    {
        if (gm == null) return;

        // 1) ID unici
        var all = gm.player.board.Concat(gm.ai.board).ToList();
        var dupIds = all.GroupBy(c => c.id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (dupIds.Any()) gm.AppendLog($"[WARN] ID duplicati: {string.Join(",", dupIds)}");

        // 2) HP coerenti
        foreach (var c in all)
        {
            if (c.health < 0) { gm.AppendLog($"[WARN] HP negativi in {c}"); c.health = 0; }
            if (c.health > c.def.maxHealth) { gm.AppendLog($"[FIX] HP > max in {c} -> clamp"); c.health = c.def.maxHealth; }
        }

        // 3) Selezioni inconsistenti
        var so = SelectionManager.Instance?.SelectedOwned?.instance;
        if (so != null && !gm.player.board.Contains(so))
        {
            gm.AppendLog("[FIX] SelectedOwned fuori dal board -> clear");
            SelectionManager.Instance.SelectOwned(null);
        }

        var se = SelectionManager.Instance?.SelectedEnemy?.instance;
        if (se != null && !gm.ai.board.Contains(se))
        {
            gm.AppendLog("[FIX] SelectedEnemy fuori dal board -> clear");
            SelectionManager.Instance.SelectEnemy(null);
        }

        // 4) AP negativi / out-of-range
        if (gm.player.actionPoints < 0) { gm.AppendLog("[FIX] AP player < 0 -> 0"); gm.player.actionPoints = 0; }
        if (gm.ai.actionPoints < 0) { gm.AppendLog("[FIX] AP ai < 0 -> 0"); gm.ai.actionPoints = 0; }
    }
}
