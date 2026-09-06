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

    // Come per le carte: i bonus si registrano con la loro causa, cosi
    // l'ispettore puo' dire da cosa viene il "+2" stampato sulla casella
    // invece di mostrarlo e basta. Vedi BonusLedger.
    readonly BonusLedger _atkLedger = new BonusLedger();
    readonly BonusLedger _blockLedger = new BonusLedger();

    public int tempAtkBonus => _atkLedger.Total;
    public int tempBlockBonus => _blockLedger.Total;

    public BonusLedger AtkBonuses => _atkLedger;
    public BonusLedger BlockBonuses => _blockLedger;

    public void AddAtkBonus(int amount, string reason) => _atkLedger.Add(amount, reason);
    public void AddBlockBonus(int amount, string reason) => _blockLedger.Add(amount, reason);
    public void ClearAtkBonus() => _atkLedger.Clear();
    public void ClearBlockBonus() => _blockLedger.Clear();

    /// <summary>
    /// La riga del pool da cui viene questa casella. E' il filo che tiene
    /// insieme le comparse: la vita si scrive li' a ogni colpo, cosi la casella
    /// che il rullo scarta non porta via le ferite con se'.
    /// </summary>
    public BossPool.Entry origin;

    /// <summary>Il numero stampato sulla casella, 0 se non viene dal pool.</summary>
    public int PoolNumber => origin != null ? origin.number : 0;

    // Pattern di flip AI
    private int _patternIndex;

    EventBus.Handler _evtHandler;

    /// <summary>
    /// La casella nasce con la vita che ha nel pool, non con quella massima: se
    /// e' gia' stata ferita in un giro precedente torna in campo ferita, ed e'
    /// questo che rende sensato colpirla anche senza ucciderla.
    /// </summary>
    public SlotInstance(SlotDefinition.Spec def, int initialStep = 0, BossPool.Entry origin = null)
    {
        this.def = def;
        this.origin = origin;
        health = origin != null ? Mathf.Clamp(origin.health, 0, def.maxHealth) : def.maxHealth;
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
        ClearBlockBonus();
        ClearAtkBonus();
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
            proposedDamage: ctx.amount,
            ignoreBlock: ctx.ignoreBlock
        );
    }

    /// <summary>
    /// Il colpo si ferma sulla vita della casella; quello che avanza arriva al
    /// boss.
    ///
    /// E' la regola centrale del combattimento: la casella e' corazza, non
    /// bersaglio. Colpirla per il suo valore esatto non fa male a nessuno,
    /// colpirla per piu' di quanto le resta la sfonda e il resto passa. Da qui
    /// vengono le due decisioni del turno: quale corsia puo' sfondare, e dove
    /// conviene spendere le cariche.
    ///
    /// ignoreBlock arriva dalla risonanza: carta e casella della stessa fazione,
    /// la guardia non tiene.
    /// </summary>
    void ResolveIncomingAttack(PlayerState attackerOwner, PlayerState defenderOwner, object attacker, int proposedDamage, bool ignoreBlock)
    {
        int incoming = Mathf.Max(0, incomingDamageOverride ?? proposedDamage);
        int block    = ignoreBlock ? 0 : ComputeSelfBlock();
        int final    = Mathf.Max(0, incoming - block);

        int absorbed = Mathf.Min(final, health);
        int overflow = final - absorbed;

        if (final > 0)
            health = Mathf.Max(0, health - absorbed);
        else
            PushHint(ignoreBlock ? "Nessun danno" : $"Guardia {block}");

        SyncOrigin();

        EventBus.Publish(GameEventType.AttackResolved, new EventContext
        {
            owner    = attackerOwner,
            opponent = defenderOwner,
            source   = attacker,
            target   = this,
            amount   = final
        });

        if (overflow > 0)
            GameManager.Instance?.OverflowToBoss(overflow, attacker, this);

        incomingDamageOverride = null;
        ClearBlockBonus();
        // Il bonus di attacco sopravvive al colpo subito: si consuma quando risponde.
    }

    /// <summary>Riporta la vita corrente nel pool: e' cio' che la fa persistere fra un giro e l'altro.</summary>
    public void SyncOrigin()
    {
        if (origin != null) origin.health = health;
    }

    /// <summary>
    /// Cura la casella e riporta il valore nel pool. Passa da qui chiunque le
    /// aggiunga vita: scrivere <c>health</c> a mano lascerebbe la riga del pool
    /// col valore vecchio, e la casella tornerebbe in campo con le ferite che si
    /// era appena curata — il numero sulla cella e quello nella corazza
    /// direbbero due cose diverse.
    /// </summary>
    public int Heal(int amount)
    {
        if (amount <= 0 || !alive) return 0;
        int before = health;
        health = Mathf.Min(def.maxHealth, health + amount);
        int healed = health - before;
        if (healed > 0) SyncOrigin();
        return healed;
    }

    public void PushHint(string msg)
        => EventBus.Publish(GameEventType.Info, new EventContext { source = this, phase = "HINT: " + msg });

    public override string ToString() => $"Slot#{id} {def.SlotName} ({def.faction}) {side} HP:{health}";
}
