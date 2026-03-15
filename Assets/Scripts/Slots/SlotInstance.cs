using UnityEngine;

public class SlotInstance
{
    public SlotDefinition.Spec def;
    public int health;
    public Side side;           // lato corrente: Fronte (attacca) o Retro (passivo)
    public bool alive => health > 0;
    public readonly int id;

    // Modificatori per-colpo
    public int? incomingDamageOverride;
    public int tempBlockBonus;
    public int tempAtkBonus;    // bonus attacco per questo turno (es. Berserker)

    // Pattern di flip AI
    private int _patternIndex;

    EventBus.Handler _evtHandler;

    public SlotInstance(SlotDefinition.Spec def)
    {
        this.def = def;
        health = def.maxHealth;
        id = GlobalId.Next();

        // Imposta il lato iniziale dal pattern (o Fronte di default)
        side = (def.flipPattern != null && def.flipPattern.Length > 0)
            ? def.flipPattern[0]
            : Side.Fronte;
        _patternIndex = 1; // pronto per il prossimo avanzamento

        _evtHandler = OnEvent;
        EventBus.Subscribe(GameEventType.AttackDeclared, _evtHandler);
    }

    public void Dispose()
    {
        if (_evtHandler != null)
        {
            EventBus.Unsubscribe(GameEventType.AttackDeclared, _evtHandler);
            _evtHandler = null;
        }
    }

    /// <summary>
    /// Avanza il pattern di flip di un passo. Chiamato da GameManager a fine turno player.
    /// </summary>
    public void AdvanceFlip()
    {
        if (def.flipPattern == null || def.flipPattern.Length == 0)
        {
            side = Side.Fronte;
            return;
        }
        side = def.flipPattern[_patternIndex % def.flipPattern.Length];
        _patternIndex++;
    }

    /// <summary>
    /// Block calcolato in base al lato corrente + eventuali bonus temporanei.
    /// </summary>
    public int ComputeSelfBlock()
    {
        int blockBase = (side == Side.Fronte) ? def.blockFront : def.blockRetro;
        return Mathf.Max(0, blockBase + tempBlockBonus);
    }

    void OnEvent(GameEventType t, EventContext ctx)
    {
        if (t != GameEventType.AttackDeclared) return;
        if (!alive) return;
        if (!ReferenceEquals(ctx.target, this)) return;

        ResolveIncomingAttack(
            attackerOwner: ctx.owner,
            defenderOwner: ctx.opponent,
            attacker: ctx.source,
            proposedDamage: ctx.amount
        );
    }

    void ResolveIncomingAttack(PlayerState attackerOwner, PlayerState defenderOwner, object attacker, int proposedDamage)
    {
        int incoming = Mathf.Max(0, incomingDamageOverride ?? proposedDamage);
        int block    = ComputeSelfBlock();
        int final    = Mathf.Max(0, incoming - block);

        if (final > 0)
            health = Mathf.Max(0, health - final);
        else
            PushHint("No damage");

        EventBus.Publish(GameEventType.AttackResolved, new EventContext
        {
            owner    = attackerOwner,
            opponent = defenderOwner,
            source   = attacker,
            target   = this,
            amount   = final
        });

        incomingDamageOverride = null;
        tempBlockBonus = 0;
        tempAtkBonus   = 0;
    }

    public void PushHint(string msg)
        => EventBus.Publish(GameEventType.Info, new EventContext { source = this, phase = "HINT: " + msg });

    public override string ToString() => $"Slot#{id} {def.SlotName} ({def.faction}) {side} HP:{health}";
}
