using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Sistema di guida in-game: intercetta gli eventi del bus e scrive messaggi
/// contestuali nel log a schermo, guidando il player durante lo sviluppo.
/// Aggiungilo allo stesso GameObject di GameManager.
/// </summary>
public class TutorialLogger : MonoBehaviour
{
    [SerializeField] private bool enableTutorialTips = false;

    // Flags "prima volta" per messaggi di introduzione
    private bool _shownFlipTip;
    private bool _shownChargeTip;
    private bool _shownMaxChargeTip;
    private bool _shownRetroSlotTip;
    private bool _shownStandoffTip;
    private bool _shownBerserkerTip;

    private EventBus.Handler _h;

    void OnEnable()
    {
        _h = OnGameEvent;
        EventBus.Subscribe(GameEventType.TurnStart,      _h);
        EventBus.Subscribe(GameEventType.TurnEnd,        _h);
        EventBus.Subscribe(GameEventType.Flip,           _h);
        EventBus.Subscribe(GameEventType.AttackResolved, _h);
        EventBus.Subscribe(GameEventType.CardPlayed,     _h);
        EventBus.Subscribe(GameEventType.Custom,         _h);
        EventBus.Subscribe(GameEventType.Info,           _h);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe(GameEventType.TurnStart,      _h);
        EventBus.Unsubscribe(GameEventType.TurnEnd,        _h);
        EventBus.Unsubscribe(GameEventType.Flip,           _h);
        EventBus.Unsubscribe(GameEventType.AttackResolved, _h);
        EventBus.Unsubscribe(GameEventType.CardPlayed,     _h);
        EventBus.Unsubscribe(GameEventType.Custom,         _h);
        EventBus.Unsubscribe(GameEventType.Info,           _h);
        _h = null;
    }

    void OnGameEvent(GameEventType t, EventContext ctx)
    {
        if (!enableTutorialTips) return;

        var gm = GameManager.Instance;
        if (gm == null) return;

        switch (t)
        {
            // ── INIZIO TURNO ────────────────────────────────────────────────
            case GameEventType.TurnStart:
                Sep();
                Log($"═══ {ctx.phase} ═══  ❤ {gm.player.hp} vs {gm.ai.hp}  |  AP {gm.player.actionPoints}/{gm.playerBaseAP}");
                break;

            case GameEventType.TurnEnd:
                break;

            // ── FLIP ────────────────────────────────────────────────────────
            case GameEventType.Flip:
                if (ctx.source is CardInstance ci)
                {
                    string newSide = ci.side == Side.Fronte ? "▶ FRONTE" : "◀ RETRO";
                    string chargeInfo = ci.flipCharge > 0 ? $"  ⚡{ci.flipCharge}/3" : "";
                    Log($"↻ {ci.def.cardName} → {newSide}{chargeInfo}");

                    if (!_shownFlipTip)
                    {
                        _shownFlipTip = true;
                        Tip("FRONTE attacca | RETRO accumula Charge e blocca di più");
                    }
                }
                break;

            case GameEventType.AttackResolved:
                string src = ctx.source is CardInstance c  ? c.def.cardName  :
                             ctx.source is SlotInstance  s ? s.def.SlotName  : "?";
                string tgt = ctx.target is CardInstance ct ? ct.def.cardName :
                             ctx.target is SlotInstance  st ? st.def.SlotName : "—";

                if (ctx.amount > 0)
                    Log($"  ⚔ {src} → {tgt}  -{ctx.amount} HP");
                else
                    Log($"  🛡 {src} → {tgt}  bloccato");

                if (ctx.phase == "DirectToEnemy")  Log("  ↳ lane vuota — danno diretto al nemico!");
                if (ctx.phase == "DirectToPlayer") Log("  ↳ lane vuota — danno diretto a te!");
                break;

            case GameEventType.CardPlayed:
                if (ctx.source is CardInstance played)
                    Log($"+ {played.def.cardName} piazzata");
                break;

            case GameEventType.Custom:
                if (ctx.phase == "SlotRetroEffect" && ctx.source is SlotInstance rs)
                {
                    Log($"  ◀ {rs.def.SlotName} — effetto passivo");
                    if (!_shownRetroSlotTip) { _shownRetroSlotTip = true; Tip("Slot RETRO: applica effetti passivi e ha block alto"); }
                }
                break;

            case GameEventType.Info:
                if (ctx.phase == null) break;

                if (ctx.phase.Contains("Charge") && ctx.source is CardInstance chCI)
                {
                    if (!_shownChargeTip && chCI.flipCharge >= 1)   { _shownChargeTip    = true; Tip("Charge accumulata — flippa in FRONTE per usarla"); }
                    if (!_shownMaxChargeTip && chCI.flipCharge >= 3) { _shownMaxChargeTip = true; Tip("★ MAX CHARGE — attacco potenziato al prossimo flip!"); }
                }
                if (ctx.phase.Contains("BURST") && !_shownBerserkerTip)
                    { _shownBerserkerTip = true; Tip("FURIA slot! Danno doppio — metti una carta RETRO davanti"); }
                if (ctx.phase.Contains("Standoff") && !_shownStandoffTip)
                    { _shownStandoffTip  = true; Tip("STALLO — entrambi in RETRO, la tua carta carica"); }
                break;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    void Log(string msg)  => Logger.Info(msg);
    void Tip(string msg)  => Logger.Info($"  💡 {msg}");
    void Sep()            => Logger.Info("──────────────────────────────────────");
}
