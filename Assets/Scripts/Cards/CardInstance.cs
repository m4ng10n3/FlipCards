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

    // I bonus non sono due interi ma due registri: chi somma deve dire perche',
    // e l'ispettore legge la ragione invece di indovinarla. Vedi BonusLedger.
    readonly BonusLedger _atkLedger = new BonusLedger();
    readonly BonusLedger _blockLedger = new BonusLedger();

    public int tempAtkBonus => _atkLedger.Total;
    public int tempBlockBonus => _blockLedger.Total;

    /// <summary>Da cosa viene il bonus di attacco che ha adesso.</summary>
    public BonusLedger AtkBonuses => _atkLedger;
    /// <summary>Da cosa viene il bonus di guardia che ha adesso.</summary>
    public BonusLedger BlockBonuses => _blockLedger;

    public void AddAtkBonus(int amount, string reason) => _atkLedger.Add(amount, reason);
    public void AddBlockBonus(int amount, string reason) => _blockLedger.Add(amount, reason);
    public void ClearAtkBonus() => _atkLedger.Clear();
    public void ClearBlockBonus() => _blockLedger.Clear();

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

    public void Flip() => side = (side == Side.Fronte ? Side.Retro : Side.Fronte);
    public override string ToString() => $"#{id} {def.cardName} ({def.faction}) {side} HP:{health}";

    // ====== UTIL ======
    public void ClearCombatBonuses()
    {
        incomingDamageOverride = null;
        ClearBlockBonus();
        ClearAtkBonus();
    }

    public int GainCharge(int amount)
    {
        int before = flipCharge;
        flipCharge = Mathf.Clamp(flipCharge + Mathf.Max(0, amount), 0, MaxFlipCharge);
        return flipCharge - before;
    }

    // ====== FLUSSO EVENT-DRIVEN ======

    // ATTACCANTE: pubblica proposta includendo le flipCharge accumulate
    public void Attack(PlayerState owner, PlayerState defender, object target, bool ignoreBlock = false)
    {
        if (!alive || target == null) return;

        EventBus.Publish(GameEventType.Custom, new EventContext
        {
            owner = owner,
            opponent = defender,
            source = this,
            target = target,
            phase = "PreCardAttack"
        });

        int damage = def.frontDamage + flipCharge + tempAtkBonus;
        flipCharge = 0;
        ClearAtkBonus();

        EventBus.Publish(GameEventType.AttackDeclared, new EventContext
        {
            owner = owner,
            opponent = defender,
            source = this,
            target = target,
            amount = damage,
            ignoreBlock = ignoreBlock
        });
    }

    // Danno in Fronte senza passare per evento (usato da LaneResolver per direct hit)
    public int ComputeAttackDamage() => def.frontDamage + flipCharge + tempAtkBonus;
    public void ConsumeCharge() { flipCharge = 0; }


    // VITTIMA: risolve solo se il bersaglio sono io
    void OnEvent(GameEventType t, EventContext ctx)
    {
        if (t != GameEventType.DamageResolution) return;
        if (ctx.target != this || !alive) return;

        ResolveIncomingAttack(
            attackerOwner: ctx.owner,
            defenderOwner: ctx.opponent,
            attacker: ctx.source,
            proposedDamage: ctx.amount,
            ignoreBlock: ctx.ignoreBlock
        );
    }


    /// <summary>
    /// Simmetrico a quello della casella: il colpo si ferma sulla vita della
    /// carta, e quello che avanza arriva al giocatore.
    ///
    /// La carta e' la tua copertura in quella corsia. Finche' regge non prendi
    /// niente; quando cede, il resto del colpo passa. Percio' una carta con
    /// poca vita davanti a una casella che picchia forte non e' una copertura:
    /// e' una porta aperta, e spostarla e' una mossa vera.
    /// </summary>
    void ResolveIncomingAttack(PlayerState attackerOwner, PlayerState defenderOwner, object attacker, int proposedDamage, bool ignoreBlock)
    {
        int incoming = Mathf.Max(0, incomingDamageOverride ?? proposedDamage);

        // In Fronte: block base frontale. In Retro: block potenziato (la carta assorbe per il player)
        int block = ignoreBlock ? 0 : (side == Side.Fronte)
            ? (def.frontBlockValue + tempBlockBonus)
            : (def.backBlockValue  + tempBlockBonus);

        int final = Mathf.Max(0, incoming - block);

        int absorbed = Mathf.Min(final, health);
        int overflow = final - absorbed;

        if (final > 0)
            health = Mathf.Max(0, health - absorbed);
        else
            PushHint("Parato!");

        EventBus.Publish(GameEventType.AttackResolved, new EventContext
        {
            owner = attackerOwner,
            opponent = defenderOwner,
            source = attacker,
            target = this,
            amount = final
        });

        if (overflow > 0)
            GameManager.Instance?.OverflowToPlayer(overflow, attacker, this);

        // reset modificatori per-colpo
        incomingDamageOverride = null;
        ClearBlockBonus();
        ClearAtkBonus();
    }
    // Hint pilotato dalla logica carta/abilit� (CardView lo intercetta)
    public void PushHint(string msg)
        => EventBus.Publish(GameEventType.Info, new EventContext { source = this, phase = "HINT: " + msg });

    // Legacy per danno al player (lasciato intatto)
    public void DealDamageToPlayer(PlayerState owner, PlayerState opponent, int amount, string phase = null)
    {
        int final = Mathf.Max(0, amount);
        opponent.TakeDamage(final);

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
