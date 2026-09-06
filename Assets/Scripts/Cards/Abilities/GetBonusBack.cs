using UnityEngine;

public class GetBonusBack : AbilityBase
{
    private EventBus.Handler _h;
    int _rewardTurn = -1;

    protected override void Register()
    {
        _rewardTurn = -1;
        _h = OnEvent;
        EventBus.Subscribe(GameEventType.Custom, _h);
        EventBus.Subscribe(GameEventType.Flip, _h);
    }

    void OnEvent(GameEventType t, EventContext ctx)
    {
        if (Source == null || !Source.alive) return;

        if (t == GameEventType.Custom && ctx.phase == "PrepareBattle")
            ApplyRetroSupport();

        if (t == GameEventType.Flip && ctx.source == Source && Source.side == Side.Retro)
            TryGrantRetroAP();
    }

    void ApplyRetroSupport()
    {
        if (Source.side != Side.Retro) return;
        if (Source.def.backDamageBonusSameFaction <= 0 && Source.def.backBlockBonusSameFaction <= 0) return;

        var gm = GameManager.Instance;
        if (gm == null) return;

        int supported = 0;
        foreach (var card in gm.GetOrderedPlayerCards())
        {
            if (card == null || card == Source || !card.alive) continue;
            if (card.def.faction != Source.def.faction) continue;

            if (card.side == Side.Fronte && Source.def.backDamageBonusSameFaction > 0)
                card.tempAtkBonus += Source.def.backDamageBonusSameFaction;

            if (Source.def.backBlockBonusSameFaction > 0)
                card.tempBlockBonus += Source.def.backBlockBonusSameFaction;

            supported++;
        }

        if (supported > 0)
        {
            Source.PushHint($"+{Source.def.backDamageBonusSameFaction}/+{Source.def.backBlockBonusSameFaction} support");
            Logger.Info($"Passive: {Source.def.cardName} supports {supported} ally cards");
        }
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
        int gained = gm.GainPlayerAP(bonusAP, $"{Source.def.cardName} relay");
        if (gained > 0)
        {
            _rewardTurn = gm.CurrentTurn;
            Source.PushHint($"+{gained} AP");
        }
    }

    protected override void Unregister()
    {
        EventBus.Unsubscribe(GameEventType.Custom, _h);
        EventBus.Unsubscribe(GameEventType.Flip, _h);
        _h = null;
    }
}
