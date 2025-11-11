// CardInstance.cs
using UnityEngine;

public class CardInstance
{
    public CardDefinition.Spec def;
    public int health;
    public Side side;
    public bool alive => health > 0;
    public readonly int id;
    static int _nextId = 1;

    // Modificatori temporanei che le abilità possono impostare reagendo agli eventi
    public int? incomingDamageOverride; // override puntuale del danno in arrivo (es. 0 per parata)
    public int tempBlockBonus;        // bonus block additivo per questo colpo

    EventBus.Handler _evtHandler;

    public CardInstance(CardDefinition.Spec def, System.Random rng)
    {
        this.def = def;
        health = def.maxHealth;
        side = rng.NextDouble() < 0.5 ? Side.Fronte : Side.Retro;
        id = _nextId++;

        // La vittima risolve i colpi che la riguardano
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

    public void Flip() => side = (side == Side.Fronte ? Side.Retro : Side.Fronte);
    public override string ToString() => $"#{id} {def.cardName} ({def.faction}) {side} HP:{health}";

    // ====== UTIL ======
    int DamageBonusFromAlliedRetro(PlayerState owner)
    {
        int bonus = 0;
        foreach (var ci in owner.board)
            if (ci.alive && ci.side == Side.Retro && ci.def.faction == def.faction)
                bonus += ci.def.backDamageBonusSameFaction;
        return bonus;
    }

    int ComputeSelfBlock(PlayerState myOwner)
    {
        int blk = 0;
        if (side == Side.Fronte && def.frontType == FrontType.Blocco)
            blk += def.frontBlockValue;

        foreach (var ci in myOwner.board)
            if (ci.alive && ci.side == Side.Retro && ci.def.faction == def.faction)
                blk += ci.def.backBlockBonusSameFaction;

        blk += Mathf.Max(0, tempBlockBonus);
        return Mathf.Max(0, blk);
    }

    public int ComputeFrontDamage(PlayerState owner)
        => Mathf.Max(0, def.frontDamage + DamageBonusFromAlliedRetro(owner));

    // ====== FLUSSO EVENT-DRIVEN ======

    // ATTACCANTE: pubblica proposta e poi chiede alla vittima di risolvere
    public void Attack(PlayerState owner, PlayerState defender, object target)
    {
        if (!alive || target == null) return;

        int proposed = ComputeFrontDamage(owner);

        EventBus.Publish(GameEventType.AttackDeclared, new EventContext
        {
            owner = owner,
            opponent = defender,
            source = this,
            target = target,
            amount = proposed
        });
    }


    // VITTIMA: risolve solo se il bersaglio sono io
    void OnEvent(GameEventType t, EventContext ctx)
    {
        if (t != GameEventType.AttackDeclared) return;
        if (ctx.target != this || !alive) return;

        ResolveIncomingAttack(
            attackerOwner: ctx.owner,
            defenderOwner: ctx.opponent,
            attacker: ctx.source,
            proposedDamage: ctx.amount
        );
    }


    // Calcolo/applicazione del danno: niente logica abilità qui dentro
    void ResolveIncomingAttack(PlayerState attackerOwner, PlayerState defenderOwner, object attacker, int proposedDamage)
    {
        // Le abilità hanno avuto occasione di settare questi modificatori ascoltando IncomingAttack.
        int incoming = Mathf.Max(0, incomingDamageOverride ?? proposedDamage);
        int block = ComputeSelfBlock(defenderOwner);
        int final = Mathf.Max(0, incoming - block);

        if (final > 0) health = Mathf.Max(0, health - final);
        else PushHint("No damage");

        // Evento unico di esito del combattimento
        EventBus.Publish(GameEventType.AttackResolved, new EventContext
        {
            owner = attackerOwner,
            opponent = defenderOwner,
            source = attacker,
            target = this,
            amount = final
        });

        // reset modificatori per-colpo
        incomingDamageOverride = null;
        tempBlockBonus = 0;
    }
    // Hint pilotato dalla logica carta/abilità (CardView lo intercetta)
    public void PushHint(string msg)
        => EventBus.Publish(GameEventType.Info, new EventContext { source = this, phase = "HINT: " + msg });

    // Legacy per danno al player (lasciato intatto)
    public void DealDamageToPlayer(PlayerState owner, PlayerState opponent, int amount, string phase = null)
    {
        int final = Mathf.Max(0, amount);
        opponent.hp -= final;

        EventBus.Publish(GameEventType.AttackResolved, new EventContext
        {
            owner = owner,
            opponent = opponent,
            source = this,
            target = null,             // danno diretto al player
            amount = final,
            phase = phase ?? "Damage"
        });
    }
}
