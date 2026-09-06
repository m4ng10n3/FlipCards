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

    public SlotInstance(SlotDefinition.Spec def, int initialStep = 0)
    {
        this.def = def;
        health = def.maxHealth;
        id = GlobalId.Next();

        int length = PatternLength;
        int step = length > 0 ? ((initialStep % length) + length) % length : 0;
        side = PatternSideAt(step);
        _patternIndex = step + 1;

        _evtHandler = OnEvent;
        EventBus.Subscribe(GameEventType.DamageResolution, _evtHandler);
    }

    public void Dispose()
    {
        if (_evtHandler != null)
        {
            EventBus.Unsubscribe(GameEventType.DamageResolution, _evtHandler);
            _evtHandler = null;
        }
    }

    /// <summary>Lunghezza del pattern; 0 = nessun pattern (sempre Fronte).</summary>
    public int PatternLength => def.flipPattern != null ? def.flipPattern.Length : 0;

    /// <summary>
    /// Passo del pattern attualmente a schermo. _patternIndex punta gia' al prossimo,
    /// quindi il corrente e' quello precedente in modulo.
    /// </summary>
    public int PatternStep
    {
        get
        {
            int len = PatternLength;
            if (len == 0) return 0;
            return ((_patternIndex - 1) % len + len) % len;
        }
    }

    /// <summary>Lato previsto al passo indicato del pattern.</summary>
    public Side PatternSideAt(int step)
    {
        int len = PatternLength;
        if (len == 0) return Side.Fronte;
        return def.flipPattern[((step % len) + len) % len];
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

    public void ClearCombatBonuses()
    {
        incomingDamageOverride = null;
        tempBlockBonus = 0;
        tempAtkBonus = 0;
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
        if (t != GameEventType.DamageResolution) return;
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
        // Attack bonus survives taking damage and is consumed when retaliating.
    }

    public void PushHint(string msg)
        => EventBus.Publish(GameEventType.Info, new EventContext { source = this, phase = "HINT: " + msg });

    public override string ToString() => $"Slot#{id} {def.SlotName} ({def.faction}) {side} HP:{health}";
}
