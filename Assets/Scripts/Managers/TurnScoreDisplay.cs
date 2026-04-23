using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Accumula i danni inflitti/ricevuti durante il turno e
/// stampa un riepilogo chiaro nel log a fine turno.
/// Aggiungilo allo stesso GameObject di GameManager.
/// </summary>
public class TurnScoreDisplay : MonoBehaviour
{
    [SerializeField] private bool enableTurnSummary = false;

    // ── Accumuli turno ────────────────────────────────────────────────────────
    private int _dmgDealt;     // danni inflitti agli slot (card → slot)
    private int _dmgReceived;  // danni ricevuti dal player (slot → card / direct)
    private int _directHits;   // volte che uno slot ha colpito direttamente l'HP player
    private int _blockedDealt; // attacchi bloccati dal player (net 0)
    private int _blockedTaken; // attacchi che il player ha bloccato completamente

    private int _currentTurn = 0;

    private EventBus.Handler _h;

    void OnEnable()
    {
        _h = OnGameEvent;
        EventBus.Subscribe(GameEventType.AttackResolved, _h);
        EventBus.Subscribe(GameEventType.TurnStart,      _h);
        EventBus.Subscribe(GameEventType.TurnEnd,        _h);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe(GameEventType.AttackResolved, _h);
        EventBus.Unsubscribe(GameEventType.TurnStart,      _h);
        EventBus.Unsubscribe(GameEventType.TurnEnd,        _h);
        _h = null;
    }

    void OnGameEvent(GameEventType type, EventContext ctx)
    {
        if (!enableTurnSummary) return;

        switch (type)
        {
            case GameEventType.TurnStart:
                // Estrai il numero turno dalla phase "TURN N"
                if (ctx.phase != null && ctx.phase.StartsWith("TURN ") &&
                    int.TryParse(ctx.phase.Substring(5), out int n))
                {
                    _currentTurn = n;
                }
                ResetAccumulators();
                break;

            case GameEventType.AttackResolved:
                // source è CardInstance (player attacca slot)
                if (ctx.source is CardInstance)
                {
                    if (ctx.amount > 0)
                        _dmgDealt += ctx.amount;
                    else
                        _blockedDealt++;
                }
                // source è SlotInstance (slot attacca card o player direttamente)
                else if (ctx.source is SlotInstance)
                {
                    if (ctx.amount > 0)
                    {
                        _dmgReceived += ctx.amount;
                        if (ctx.phase == "DirectToPlayer") _directHits++;
                    }
                    else
                        _blockedTaken++;
                }
                break;

            case GameEventType.TurnEnd:
                PrintSummary();
                break;
        }
    }

    // ── Stampa riepilogo ──────────────────────────────────────────────────────

    void PrintSummary()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        Logger.Info("──────────────────────────────────────");
        Logger.Info($"📊  RIEPILOGO TURNO {_currentTurn}");
        Logger.Info($"   ⚔  Danni inflitti agli slot : {_dmgDealt}");

        if (_blockedDealt > 0)
            Logger.Info($"   🛡  Attacchi bloccati dagli slot : {_blockedDealt}");

        Logger.Info($"   💥  Danni ricevuti           : {_dmgReceived}");

        if (_directHits > 0)
            Logger.Info($"   ❗  Colpi diretti all'HP player : {_directHits}");

        if (_blockedTaken > 0)
            Logger.Info($"   🛡  Attacchi slot bloccati      : {_blockedTaken}");

        int net = _dmgDealt - _dmgReceived;
        string netStr = net > 0 ? $"+{net} (turno positivo)"
                      : net < 0 ? $"{net} (turno negativo)"
                      : "0 (pareggio)";
        Logger.Info($"   📈  Bilancio netto            : {netStr}");
        Logger.Info($"   ❤  HP Player: {gm.player.hp}   |   HP Nemico: {gm.ai.hp}");
        Logger.Info("──────────────────────────────────────");
    }

    // ── Reset ─────────────────────────────────────────────────────────────────

    void ResetAccumulators()
    {
        _dmgDealt     = 0;
        _dmgReceived  = 0;
        _directHits   = 0;
        _blockedDealt = 0;
        _blockedTaken = 0;
    }
}
