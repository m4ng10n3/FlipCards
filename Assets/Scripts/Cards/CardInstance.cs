// CardInstance.cs
using UnityEngine;

public class CardInstance
{
    public CardDefinition.Spec def;
    public int health;
    public Side side;
    public bool alive => health > 0;
    public readonly int id;
    private GameManager gm;
    public void AssignGM(GameManager gameManager)
    {
        gm = gameManager;
    }

    // Modificatori temporanei che le abilità possono impostare reagendo agli eventi
    public int? incomingDamageOverride; // override puntuale del danno in arrivo (es. 0 per parata)
    public int tempBlockBonus;          // bonus block additivo per questo colpo

    // Flip Charge System: si accumula stando in Retro, si consuma attaccando in Fronte
    public int flipCharge;              // 0..3, bonus danno al prossimo attacco
    public const int MaxFlipCharge = 3;

    EventBus.Handler _evtHandler;

    public CardInstance(CardDefinition.Spec def, System.Random rng)
    {
        this.def = def;
        health = def.maxHealth;
        side = rng.NextDouble() < 0.5 ? Side.Fronte : Side.Retro;
        id = GlobalId.Next();

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

    // ====== FLUSSO EVENT-DRIVEN ======

    // ATTACCANTE: pubblica proposta includendo le flipCharge accumulate
    public void Attack(PlayerState owner, PlayerState defender, object target)
    {
        if (!alive || target == null) return;

        int damage = def.frontDamage + flipCharge;
        flipCharge = 0; // consuma le cariche

        EventBus.Publish(GameEventType.AttackDeclared, new EventContext
        {
            owner = owner,
            opponent = defender,
            source = this,
            target = target,
            amount = damage
        });
    }

    // Danno in Fronte senza passare per evento (usato da LaneResolver per direct hit)
    public int ComputeAttackDamage() => def.frontDamage + flipCharge;
    public void ConsumeCharge() { flipCharge = 0; }


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
        int incoming = Mathf.Max(0, incomingDamageOverride ?? proposedDamage);

        // In Fronte: block base frontale. In Retro: block potenziato (la carta assorbe per il player)
        int block = (side == Side.Fronte)
            ? (def.frontBlockValue + tempBlockBonus)
            : (def.backBlockValue  + tempBlockBonus);

        int final = Mathf.Max(0, incoming - block);

        // Il danno va sempre alla carta, mai direttamente al player
        // (il player subisce danno solo se la lane è vuota — gestito da LaneResolver)
        if (final > 0)
            health = Mathf.Max(0, health - final);
        else
            PushHint("Blocked!");

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
    // Hint pilotato dalla logica carta/abilit� (CardView lo intercetta)
    public void PushHint(string msg)
        => EventBus.Publish(GameEventType.Info, new EventContext { source = this, phase = "HINT: " + msg });

    // Legacy per danno al player (lasciato intatto)
    public void DealDamageToPlayer(PlayerState owner, PlayerState opponent, int amount, string phase = null)
    {
        int final = Mathf.Max(0, amount);
        opponent.hp -= final;

        GameManager.Instance?.UpdateHUD();

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
