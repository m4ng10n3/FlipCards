using UnityEngine;

/// <summary>
/// ABILITÀ: Colpo Avanguardia
/// Se la carta è in Fronte e le lane adiacenti sono VUOTE (nessuna carta player),
/// il suo attacco infligge bonusDamage aggiuntivo.
/// Premia chi rischia tenendo lane scoperte.
/// </summary>
public class VanguardStrike : AbilityBase
{
    [Min(1)] public int bonusDamage = 2;

    private EventBus.Handler _h;

    protected override void Register()
    {
        _h = (t, ctx) =>
        {
            if (t != GameEventType.AttackDeclared) return;
            if (ctx.source != Source || !Source.alive) return;
            if (Source.side != Side.Fronte) return;

            var gm = GameManager.Instance;
            if (gm == null) return;

            // Trova la lane di questa carta
            if (!FindMyLane(gm, out int myLane)) return;

            int emptyNeighbors = 0;
            // Controlla lane sinistra
            if (myLane > 0)
            {
                var left = gm.playerBoardRoot.GetChild(myLane - 1);
                if (left.GetComponentInChildren<CardView>(false) == null) emptyNeighbors++;
            }
            // Controlla lane destra
            if (myLane < gm.playerBoardRoot.childCount - 1)
            {
                var right = gm.playerBoardRoot.GetChild(myLane + 1);
                if (right.GetComponentInChildren<CardView>(false) == null) emptyNeighbors++;
            }

            if (emptyNeighbors == 0) return;

            // Applica il bonus direttamente sull'HP del target o del nemico
            int bonus = bonusDamage * emptyNeighbors;
            if (ctx.target is SlotInstance si)
                si.health = Mathf.Max(0, si.health - bonus);
            else
                Owner.actionPoints += 0; // no target: il danno è già su ai.hp via DirectToEnemy

            Source.PushHint($"Vanguard +{bonus}!");
            Logger.Info($"[VanguardStrike] {Source.def.cardName} +{bonus} dmg ({emptyNeighbors} lane vuote adiacenti)");
        };

        EventBus.Subscribe(GameEventType.AttackDeclared, _h);
    }

    bool FindMyLane(GameManager gm, out int lane)
    {
        for (int i = 0; i < gm.playerBoardRoot.childCount; i++)
        {
            var cv = gm.playerBoardRoot.GetChild(i).GetComponentInChildren<CardView>(false);
            if (cv?.instance == Source) { lane = i; return true; }
        }
        lane = -1;
        return false;
    }

    protected override void Unregister()
    {
        EventBus.Unsubscribe(GameEventType.AttackDeclared, _h);
        _h = null;
    }
}
