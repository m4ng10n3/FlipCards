using UnityEngine;

/// <summary>
/// ABILITÀ SLOT: Furia Crescente
/// Lo slot accumula 1 stack di furia ogni turno che rimane in Fronte.
/// Quando raggiunge furyThreshold stack, il prossimo attacco infligge danno doppio
/// e gli stack si azzerano.
/// Pattern visibile: il giocatore può predire il colpo potenziato e difendersi.
/// </summary>
public class SlotBerserker : AbilityBase
{
    [Min(2)] public int furyThreshold = 3;

    private int _furyStacks;
    private bool _burstReady;

    /// <summary>Stack accumulati: senza contatore a schermo il pattern non e' leggibile.</summary>
    public int FuryStacks => _furyStacks;
    public bool BurstReady => _burstReady;

    private EventBus.Handler _h;
    private SlotView _slotView;
    private SlotInstance _slot;

    protected override void Register()
    {
        _slotView = GetComponent<SlotView>();
        _slot     = _slotView ? _slotView.instance : null;
        _furyStacks = 0;
        _burstReady = false;

        _h = (t, ctx) =>
        {
            if (_slot == null || !_slot.alive) return;

            // A inizio turno (TurnStart), se lo slot è in Fronte accumula furia
            if (t == GameEventType.TurnStart)
            {
                if (_slot.side == Side.Fronte)
                {
                    _furyStacks = GameManager.Instance != null ? GameManager.Instance.CountEnemyFaction(_slot.def.faction) : 1;
                    if (_furyStacks >= furyThreshold)
                    {
                        _burstReady = true;
                        _slot.AddAtkBonus(_slot.def.atkDamage, $"{AbilityCatalog.Name(this)}: burst");
                        _slot.PushHint($"FURIA! Prossimo attacco x2");
                        Logger.Info($"[Berserker] {_slot.def.SlotName} BURST PRONTO ({_furyStacks}/{furyThreshold} stack)");
                    }
                    else
                    {
                        _slot.PushHint($"Furia {_furyStacks}/{furyThreshold}");
                    }
                }
                else
                {
                    // In Retro, la furia decade
                    if (_furyStacks > 0)
                    {
                        _furyStacks = 0;
                        _burstReady = false;
                        _slot.PushHint("Furia dispersa");
                    }
                }
                return;
            }

            // Prima che lo slot attacchi: se burst pronto, raddoppia il danno
            if (t == GameEventType.Custom && ctx.phase == "PreSlotAttack" && ReferenceEquals(ctx.source, _slot) && _burstReady)
            {
                _burstReady = false;
                _furyStacks = 0;
                _slot.PushHint("BURST x2!");
                Logger.Info($"[Berserker] {_slot.def.SlotName} attacca con danno x2");
            }
        };

        EventBus.Subscribe(GameEventType.TurnStart, _h);
        EventBus.Subscribe(GameEventType.Custom, _h);
    }

    protected override void Unregister()
    {
        EventBus.Unsubscribe(GameEventType.TurnStart, _h);
        EventBus.Unsubscribe(GameEventType.Custom, _h);
        _h = null;
        _slotView = null;
        _slot     = null;
    }
}
