using UnityEngine;

/// <summary>
/// La staffetta: due carte della stessa fazione coperte insieme restituiscono
/// punti azione, una volta per turno.
///
/// **Qui dentro non c'e' piu' l'aura di fazione.** Prima questa abilita'
/// applicava <c>backDamageBonusSameFaction</c> e <c>backBlockBonusSameFaction</c>
/// a **tutte** le carte della sua fazione in campo, e stava su tutte e dieci le
/// carte. Erano due difetti in uno:
///
///  - **il numero era doppio.** <see cref="SynergyResolver"/> da' lo stesso
///    bonus alle vicine, quindi una carta adiacente lo incassava due volte e il
///    pronostico di corsia — che conta solo l'adiacenza — diceva una cifra e la
///    risoluzione ne applicava un'altra;
///  - **la posizione non contava.** Un'aura che raggiunge tutto il tavolo
///    rende lo spostamento delle carte una mossa inutile, ed e' esattamente la
///    decisione su cui il gioco si regge: l'insegna vale solo per chi le sta
///    accanto, quindi a fine turno, quando il caos rimescola la fila, rimetterla
///    al posto giusto e' il lavoro del turno.
///
/// L'insegna e' quindi una **regola**, non un'abilita': la risolve
/// SynergyResolver per tutte le carte, e la carta la dichiara col simbolo
/// stampato sul retro. Quello che resta qui e' l'unica cosa che l'aura non era —
/// una ricompensa per essere rimasti coperti, che vive su
/// <c>backBonusPAIfTwoRetroSameFaction</c> e ce l'hanno solo le carte pensate
/// per farlo.
/// </summary>
public class GetBonusBack : AbilityBase
{
    private EventBus.Handler _h;
    int _rewardTurn = -1;

    protected override void Register()
    {
        _rewardTurn = -1;
        _h = OnEvent;
        EventBus.Subscribe(GameEventType.Flip, _h);
    }

    void OnEvent(GameEventType t, EventContext ctx)
    {
        if (Source == null || !Source.alive) return;

        if (t == GameEventType.Flip && ctx.source == Source && Source.side == Side.Retro)
            TryGrantRetroAP();
    }

    void TryGrantRetroAP()
    {
        int bonusAP = Source.def.backBonusPAIfTwoRetroSameFaction;
        if (bonusAP <= 0) return;

        int retroCount = Owner.CountRetro(Source.def.faction);
        if (retroCount < 2) return;

        var gm = GameManager.Instance;
        if (gm == null) return;

        if (!gm.CanAct || _rewardTurn == gm.CurrentTurn) return;
        int gained = gm.GainPlayerAP(bonusAP, $"{Source.def.cardName} staffetta");
        if (gained > 0)
        {
            _rewardTurn = gm.CurrentTurn;
            Source.PushHint($"STAFFETTA +{gained} AP");
        }
    }

    protected override void Unregister()
    {
        EventBus.Unsubscribe(GameEventType.Flip, _h);
        _h = null;
    }
}
