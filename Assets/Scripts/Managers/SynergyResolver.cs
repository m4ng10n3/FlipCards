using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rileva combinazioni di classi sulla board player e applica bonus dopo la risoluzione lane.
///
/// SINERGIE ATTIVE:
///  Devastazione  — 3x Assalto in Fronte       → +4 danni diretti al boss
///  Doppio Colpo  — 2x Assalto + 1x Tecnico    → ri-attacca la lane con lo slot più debole
///  Nexus         — 3x Mistico (qualsiasi lato) → 15 danni diretti al boss
///  Fortezza      — 2x Guardia (qualsiasi)      → blocca il prossimo attacco slot (tempBlockBonus +99 per 1 turno)
///  Benedizione   — 1x Guardia + 1x Mistico adj → cura player 3 HP
///  Ira           — 2x Assalto adj (entrambi Fronte) → +2 ATK a ciascuno (tempAtkBonus)
/// </summary>
public static class SynergyResolver
{
    public static void Resolve(List<CardInstance> board, PlayerState player, PlayerState ai)
    {
        if (board == null || board.Count == 0) return;

        // Snapshot delle carte vive
        var alive = new List<CardInstance>();
        foreach (var ci in board)
            if (ci != null && ci.alive) alive.Add(ci);

        if (alive.Count == 0) return;

        int assalto = 0, tecnico = 0, mistico = 0, guardia = 0;
        int assaltoFront = 0;

        foreach (var ci in alive)
        {
            switch (ci.def.cardClass)
            {
                case CardClass.Assalto: assalto++; if (ci.side == Side.Fronte) assaltoFront++; break;
                case CardClass.Tecnico: tecnico++; break;
                case CardClass.Mistico: mistico++; break;
                case CardClass.Guardia: guardia++; break;
            }
        }

        // ── DEVASTAZIONE: 3 Assalto tutti in Fronte ──────────────────────────
        if (assalto >= 3 && assaltoFront >= 3)
        {
            ai.hp -= 4;
            Logger.Info("✨ SINERGIA — Devastazione: 3 Assalto in Fronte! +4 danni al boss.");
            EventBus.Publish(GameEventType.Info, new EventContext { phase = "SYNERGY:Devastazione" });
        }

        // ── NEXUS: 3 Mistici ─────────────────────────────────────────────────
        if (mistico >= 3)
        {
            ai.hp -= 15;
            Logger.Info("✨ SINERGIA — Nexus: 3 Mistici! 15 danni diretti al boss.");
            EventBus.Publish(GameEventType.Info, new EventContext { phase = "SYNERGY:Nexus" });
        }

        // ── DOPPIO COLPO: 2 Assalto + 1 Tecnico ─────────────────────────────
        if (assalto >= 2 && tecnico >= 1)
        {
            // Trova la lane Assalto con Fronte più forte e ri-applica metà danno
            int bonus = 0;
            foreach (var ci in alive)
                if (ci.def.cardClass == CardClass.Assalto && ci.side == Side.Fronte)
                    bonus = Mathf.Max(bonus, ci.def.frontDamage + ci.flipCharge);
            if (bonus > 0)
            {
                int half = Mathf.Max(1, bonus / 2);
                ai.hp -= half;
                Logger.Info($"✨ SINERGIA — Doppio Colpo: +{half} danni bonus al boss.");
                EventBus.Publish(GameEventType.Info, new EventContext { phase = "SYNERGY:DoppioColpo" });
            }
        }

        // ── IRA: 2 Assalto adiacenti entrambi in Fronte ──────────────────────
        for (int i = 0; i < alive.Count - 1; i++)
        {
            var a = alive[i];
            var b = alive[i + 1];
            if (a.def.cardClass == CardClass.Assalto && b.def.cardClass == CardClass.Assalto
                && a.side == Side.Fronte && b.side == Side.Fronte)
            {
                a.tempAtkBonus += 2;
                b.tempAtkBonus += 2;
                Logger.Info($"✨ SINERGIA — Ira: {a.def.cardName} & {b.def.cardName} +2 ATK questo turno.");
                EventBus.Publish(GameEventType.Info, new EventContext { phase = "SYNERGY:Ira" });
                break; // applica una volta sola
            }
        }

        // ── FORTEZZA: 2 Guardie ───────────────────────────────────────────────
        if (guardia >= 2)
        {
            // Imposta un block massiccio su tutte le Guardie per il prossimo attacco slot
            foreach (var ci in alive)
                if (ci.def.cardClass == CardClass.Guardia)
                    ci.tempBlockBonus += 5;
            Logger.Info("✨ SINERGIA — Fortezza: le Guardie assorbono il prossimo attacco.");
            EventBus.Publish(GameEventType.Info, new EventContext { phase = "SYNERGY:Fortezza" });
        }

        // ── BENEDIZIONE: Guardia + Mistico adiacenti ─────────────────────────
        for (int i = 0; i < alive.Count - 1; i++)
        {
            var a = alive[i];
            var b = alive[i + 1];
            bool adj = (a.def.cardClass == CardClass.Guardia && b.def.cardClass == CardClass.Mistico)
                    || (a.def.cardClass == CardClass.Mistico && b.def.cardClass == CardClass.Guardia);
            if (adj)
            {
                player.hp += 3;
                Logger.Info($"✨ SINERGIA — Benedizione: Guardia+Mistico adiacenti, +3 HP player.");
                EventBus.Publish(GameEventType.Info, new EventContext { phase = "SYNERGY:Benedizione" });
                break;
            }
        }
    }
}
