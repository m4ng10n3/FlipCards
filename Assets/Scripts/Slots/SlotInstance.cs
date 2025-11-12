using UnityEngine;

public class SlotInstance
{
    public SlotDefinition.Spec def;
    public int health;
    public bool alive => health > 0;
    public readonly int id;

    // Modificatori per-colpo che abilità di slot possono settare
    public int? incomingDamageOverride; // es. 0 = annulla il colpo
    public int tempBlockBonus;          // block additivo per questo colpo

    EventBus.Handler _evtHandler;

    public SlotInstance(SlotDefinition.Spec def)
    {
        this.def = def;
        health = def.maxHealth;
        id = GlobalId.Next();

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

    int ComputeSelfBlock()
    {
        // per ora: solo bonus temporaneo; se vuoi, aggiungi stat base di block nello Spec
        int blk = Mathf.Max(0, tempBlockBonus);
        return blk;
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
        int block = ComputeSelfBlock();
        int final = Mathf.Max(0, incoming - block);

        if (final > 0) health = Mathf.Max(0, health - final);
        else PushHint("No damage");

        EventBus.Publish(GameEventType.AttackResolved, new EventContext
        {
            owner = attackerOwner,
            opponent = defenderOwner,
            source = attacker,
            target = this,
            amount = final
        });

        incomingDamageOverride = null;
        tempBlockBonus = 0;
    }

    // Hint pilotato dalla logica slot/abilità (SlotView lo intercetta)
    public void PushHint(string msg)
        => EventBus.Publish(GameEventType.Info, new EventContext { source = this, phase = "HINT: " + msg });


    public override string ToString() => $"Slot#{id} {def.SlotName} ({def.faction}) HP:{health}";
}
