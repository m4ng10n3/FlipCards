using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Sistema di guida in-game: intercetta gli eventi del bus e scrive messaggi
/// contestuali nel log a schermo, guidando il player durante lo sviluppo.
/// Aggiungilo allo stesso GameObject di GameManager.
/// </summary>
public class TutorialLogger : MonoBehaviour
{
    // Flags "prima volta" per messaggi di introduzione
    private bool _shownFlipTip;
    private bool _shownChargeTip;
    private bool _shownMaxChargeTip;
    private bool _shownRetroSlotTip;
    private bool _shownEmptyLaneTip;
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
        var gm = GameManager.Instance;
        if (gm == null) return;

        switch (t)
        {
            // ── INIZIO TURNO ────────────────────────────────────────────────
            case GameEventType.TurnStart:
                Sep();
                Log($"═══ {ctx.phase} ═══  HP Player: {gm.player.hp}  |  HP Nemico: {gm.ai.hp}");
                Log($"AP disponibili: {gm.player.actionPoints}/{gm.playerBaseAP}");
                Log("Azioni: [Doppio click] Flip carta  |  [Drag] Swap  |  [ATTACK] Risolvi  |  [END TURN] Fine turno");
                DescribeBoardState(gm);
                DescribeSlotState(gm);
                break;

            // ── FINE TURNO ──────────────────────────────────────────────────
            case GameEventType.TurnEnd:
                Log("— Fine turno. I pattern degli slot avanzano.");
                break;

            // ── FLIP ────────────────────────────────────────────────────────
            case GameEventType.Flip:
                if (ctx.source is CardInstance ci)
                {
                    string newSide = ci.side == Side.Fronte ? "▶ FRONTE" : "◀ RETRO";
                    Log($"↻ Flip: {ci.def.cardName} → {newSide}");

                    if (!_shownFlipTip)
                    {
                        _shownFlipTip = true;
                        Tip("FRONTE = attacca in questa lane. RETRO = non attacca, ma accumula Charge ogni turno e assorbe i colpi con block potenziato.");
                    }

                    if (ci.side == Side.Retro)
                        Log($"  Charge corrente: {ci.flipCharge}/3 — cresce di 1 ogni fine turno in Retro.");
                    else if (ci.flipCharge > 0)
                        Log($"  ★ Torna in Fronte con {ci.flipCharge} charge → +{ci.flipCharge} dmg extra all'attacco!");
                }
                break;

            // ── ATTACCO RISOLTO ──────────────────────────────────────────────
            case GameEventType.AttackResolved:
                string src = ctx.source is CardInstance c  ? c.def.cardName  :
                             ctx.source is SlotInstance  s ? s.def.SlotName  : "?";
                string tgt = ctx.target is CardInstance ct ? ct.def.cardName :
                             ctx.target is SlotInstance  st ? st.def.SlotName : "(direct)";

                if (ctx.amount > 0)
                    Log($"  ⚔ {src} → {tgt}: {ctx.amount} dmg");
                else
                    Log($"  🛡 {src} → {tgt}: bloccato!");

                if (ctx.phase == "DirectToEnemy")
                {
                    Log($"  Lane nemica vuota! Danno diretto al nemico.");
                    if (!_shownEmptyLaneTip)
                    {
                        _shownEmptyLaneTip = true;
                        Tip("Se una lane nemica è vuota, la tua carta FRONTE colpisce l'HP nemico direttamente.");
                    }
                }
                if (ctx.phase == "DirectToPlayer")
                    Log($"  Lane player vuota! Lo slot colpisce direttamente i tuoi HP.");
                break;

            // ── CARTA GIOCATA ────────────────────────────────────────────────
            case GameEventType.CardPlayed:
                if (ctx.source is CardInstance played)
                {
                    string sideLabel = played.side == Side.Fronte ? "FRONTE" : "RETRO";
                    Log($"+ Carta piazzata: {played.def.cardName} [{played.def.faction}] in {sideLabel}");
                }
                break;

            // ── EVENTI CUSTOM (effetti passivi slot) ─────────────────────────
            case GameEventType.Custom:
                if (ctx.phase == "SlotRetroEffect" && ctx.source is SlotInstance rs)
                {
                    Log($"  ◀ {rs.def.SlotName} in RETRO — effetto passivo attivo");
                    if (!_shownRetroSlotTip)
                    {
                        _shownRetroSlotTip = true;
                        Tip("Slot in RETRO: non attacca, ma applica effetti passivi (regen, buff adiacenti, debuff). " +
                            "Il loro block è molto più alto in questa posizione.");
                    }
                }
                break;

            // ── INFO GENERALI (intercetta hint significativi) ─────────────────
            case GameEventType.Info:
                if (ctx.phase == null) break;

                if (ctx.phase.Contains("Charge") && ctx.source is CardInstance chCI)
                {
                    if (!_shownChargeTip && chCI.flipCharge >= 1)
                    {
                        _shownChargeTip = true;
                        Tip("Charge accumulata! Rimani in RETRO per caricare fino a 3. Al prossimo flip verso FRONTE, l'attacco includerà il bonus.");
                    }
                    if (!_shownMaxChargeTip && chCI.flipCharge >= 3)
                    {
                        _shownMaxChargeTip = true;
                        Tip("★ MAX CHARGE! Questa carta attaccherà con +" + CardInstance.MaxFlipCharge + " dmg extra. Flippa ora per scatenare il burst!");
                    }
                }

                if (ctx.phase.Contains("BURST") && !_shownBerserkerTip)
                {
                    _shownBerserkerTip = true;
                    Tip("Uno slot ha raggiunto la FURIA PIENA e attacca con danno doppio! Metti una carta RETRO davanti per assorbirlo.");
                }

                if (ctx.phase.Contains("Standoff") && !_shownStandoffTip)
                {
                    _shownStandoffTip = true;
                    Tip("STALLO! Entrambi in RETRO: nessun danno, ma la tua carta carica e lo slot usa effetti passivi. " +
                        "Strategia: accumula charge, poi flippa al momento giusto.");
                }
                break;
        }
    }

    // ── Stato board player ────────────────────────────────────────────────────
    void DescribeBoardState(GameManager gm)
    {
        var board = gm.player.board;
        if (board.Count == 0) { Log("Board player: vuota — gioca una carta dalla mano!"); return; }

        Log("Board player:");
        for (int i = 0; i < board.Count; i++)
        {
            var ci = board[i];
            string sideIcon  = ci.side == Side.Fronte ? "▶" : "◀";
            string chargeBar = ci.flipCharge > 0 ? $" [charge {ci.flipCharge}/3]" : "";
            string dmgInfo   = ci.side == Side.Fronte
                ? $"ATK:{ci.def.frontDamage + ci.flipCharge} BLK:{ci.def.frontBlockValue}"
                : $"BLK:{ci.def.backBlockValue}{chargeBar}";
            Log($"  Lane {i + 1}: {sideIcon} {ci.def.cardName} HP:{ci.health}/{ci.def.maxHealth}  {dmgInfo}");
        }
    }

    // ── Stato slot nemici ─────────────────────────────────────────────────────
    void DescribeSlotState(GameManager gm)
    {
        if (gm.aiBoardRoot == null) return;
        Log("Slot nemici:");
        for (int i = 0; i < gm.aiBoardRoot.childCount; i++)
        {
            var sv = gm.aiBoardRoot.GetChild(i).GetComponentInChildren<SlotView>(false);
            if (sv == null || sv.instance == null)
            {
                Log($"  Lane {i + 1}: [vuota]");
                continue;
            }
            var si = sv.instance;
            string sideIcon = si.side == Side.Fronte ? "▶" : "◀";
            string action   = si.side == Side.Fronte
                ? $"ATK:{si.def.atkDamage} BLK:{si.def.blockFront}"
                : $"BLK:{si.def.blockRetro} (effetto passivo)";
            Log($"  Lane {i + 1}: {sideIcon} {si.def.SlotName} HP:{si.health}/{si.def.maxHealth}  {action}");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    void Log(string msg)  => Logger.Info(msg);
    void Tip(string msg)  => Logger.Info($"  💡 TIP: {msg}");
    void Sep()            => Logger.Info("──────────────────────────────────────");
}
