using UnityEngine;

/// <summary>
/// Risolve una singola lane applicando la matrice 2x2 (CardSide x SlotSide).
/// Chiamato da GameManager.ResolveLanes() per ogni lane del board.
/// </summary>
public static class LaneResolver
{
    /// <summary>
    /// Risolve gli scontri nella lane indicata.
    /// Pubblica eventi AttackDeclared / AttackResolved / Custom per consentire
    /// alle abilità di reagire nel normale flusso EventBus.
    /// </summary>
    public static void Resolve(int laneIndex, CardInstance card, SlotInstance slot,
                                PlayerState player, PlayerState ai)
    {
        // ── Entrambi presenti ─────────────────────────────────────────────────
        if (card != null && slot != null)
        {
            bool cardFront = card.side == Side.Fronte;
            bool slotFront = slot.side == Side.Fronte;

            if (cardFront && slotFront)
                ResolveFronteFronte(laneIndex, card, slot, player, ai);
            else if (cardFront && !slotFront)
                ResolveFronteRetro(laneIndex, card, slot, player, ai);
            else if (!cardFront && slotFront)
                ResolveRetroFronte(laneIndex, card, slot, player, ai);
            else
                ResolveRetroRetro(laneIndex, card, slot, player, ai);
        }
        // ── Solo carta ────────────────────────────────────────────────────────
        else if (card != null)
        {
            if (card.side == Side.Fronte)
            {
                // Lane nemica vuota: danno diretto all'HP del nemico
                int dmg = card.ComputeAttackDamage();
                card.ConsumeCharge();
                ai.hp -= dmg;
                GameManager.Instance?.UpdateHUD();
                EventBus.Publish(GameEventType.AttackResolved, new EventContext
                {
                    owner    = player,
                    opponent = ai,
                    source   = card,
                    target   = null,
                    amount   = dmg,
                    phase    = "DirectToEnemy"
                });
                card.PushHint($"Direct! -{dmg}");
                Logger.Info($"[Lane {laneIndex + 1}] {card.def.cardName} FRONTE → lane vuota → -{dmg} HP nemico");
            }
            else
            {
                // Carta in Retro, lane vuota: si carica (charge già accumulata da AccumulateFlipCharges)
                Logger.Info($"[Lane {laneIndex + 1}] {card.def.cardName} RETRO → lane vuota → carica");
            }
        }
        // ── Solo slot ─────────────────────────────────────────────────────────
        else if (slot != null)
        {
            if (slot.side == Side.Fronte)
            {
                // Lane player vuota: danno diretto all'HP del player
                SlotAttackPlayer(laneIndex, slot, player, ai);
            }
            else
            {
                // Slot in Retro, lane player vuota: effetto passivo (gestito da abilità slot)
                FireSlotRetroEffect(slot, player, ai);
                Logger.Info($"[Lane {laneIndex + 1}] {slot.def.SlotName} RETRO → lane vuota → effetto passivo");
            }
        }
    }

    // ── Caso 1: FRONTE vs FRONTE — combattimento diretto ─────────────────────
    static void ResolveFronteFronte(int lane, CardInstance card, SlotInstance slot,
                                     PlayerState player, PlayerState ai)
    {
        Logger.Info($"[Lane {lane + 1}] ▶FRONTE vs ▶FRONTE — combattimento");

        // Carta attacca lo slot (via AttackDeclared → SlotInstance.ResolveIncomingAttack)
        card.Attack(player, ai, slot);

        // Lo slot contrattacca la carta
        if (slot.alive)
        {
            int slotDmg    = Mathf.Max(0, slot.def.atkDamage + slot.tempAtkBonus);
            int cardBlock  = card.def.frontBlockValue + card.tempBlockBonus;
            int finalToCard = Mathf.Max(0, slotDmg - cardBlock);

            if (finalToCard > 0)
                card.health = Mathf.Max(0, card.health - finalToCard);

            EventBus.Publish(GameEventType.AttackResolved, new EventContext
            {
                owner    = ai,
                opponent = player,
                source   = slot,
                target   = card,
                amount   = finalToCard
            });

            if (finalToCard > 0)
                card.PushHint($"-{finalToCard}HP");
            else
                card.PushHint("Blocked!");

            Logger.Info($"  {slot.def.SlotName} contrattacca → {finalToCard} dmg a {card.def.cardName}");
        }

        slot.tempAtkBonus = 0;
    }

    // ── Caso 2: FRONTE vs RETRO — carta attacca slot difeso, slot debuffa ────
    static void ResolveFronteRetro(int lane, CardInstance card, SlotInstance slot,
                                    PlayerState player, PlayerState ai)
    {
        Logger.Info($"[Lane {lane + 1}] ▶FRONTE vs ◀RETRO — slot difeso");

        // Carta attacca con block alto del Retro (lo slot usa blockRetro)
        card.Attack(player, ai, slot);
        // SlotInstance.ResolveIncomingAttack usa ComputeSelfBlock() → blockRetro

        // Lo slot in Retro applica il suo effetto passivo
        if (slot.alive)
        {
            FireSlotRetroEffect(slot, player, ai);
            Logger.Info($"  {slot.def.SlotName} RETRO → effetto passivo su {card.def.cardName}");
        }
    }

    // ── Caso 3: RETRO vs FRONTE — carta assorbe, si carica ──────────────────
    static void ResolveRetroFronte(int lane, CardInstance card, SlotInstance slot,
                                    PlayerState player, PlayerState ai)
    {
        Logger.Info($"[Lane {lane + 1}] ◀RETRO vs ▶FRONTE — carta assorbe");

        // Lo slot attacca la carta (card usa backBlockValue via ResolveIncomingAttack)
        int slotDmg     = Mathf.Max(0, slot.def.atkDamage + slot.tempAtkBonus);
        int cardBlock   = card.def.backBlockValue + card.tempBlockBonus;
        int finalToCard = Mathf.Max(0, slotDmg - cardBlock);

        EventBus.Publish(GameEventType.AttackDeclared, new EventContext
        {
            owner    = ai,
            opponent = player,
            source   = slot,
            target   = card,
            amount   = slotDmg
        });
        // CardInstance.OnEvent reagirà e chiamerà ResolveIncomingAttack con backBlockValue

        if (finalToCard > 0)
            Logger.Info($"  {slot.def.SlotName} attacca → {card.def.cardName} assorbe {finalToCard} dmg (block {cardBlock})");
        else
            Logger.Info($"  {slot.def.SlotName} attacca → {card.def.cardName} blocca tutto! (block {cardBlock})");

        slot.tempAtkBonus = 0;
        // La charge si accumula tramite AccumulateFlipCharges() a fine turno
    }

    // ── Caso 4: RETRO vs RETRO — stallo, tutti gli effetti passivi ───────────
    static void ResolveRetroRetro(int lane, CardInstance card, SlotInstance slot,
                                   PlayerState player, PlayerState ai)
    {
        Logger.Info($"[Lane {lane + 1}] ◀RETRO vs ◀RETRO — stallo, effetti passivi");

        card.PushHint("Standoff");
        FireSlotRetroEffect(slot, player, ai);
    }

    // ── Helper: attacco diretto dello slot al player ──────────────────────────
    static void SlotAttackPlayer(int lane, SlotInstance slot, PlayerState player, PlayerState ai)
    {
        int dmg = Mathf.Max(0, slot.def.atkDamage + slot.tempAtkBonus);
        player.hp -= dmg;
        GameManager.Instance?.UpdateHUD();

        EventBus.Publish(GameEventType.AttackResolved, new EventContext
        {
            owner    = ai,
            opponent = player,
            source   = slot,
            target   = null,
            amount   = dmg,
            phase    = "DirectToPlayer"
        });

        slot.PushHint($"Direct! -{dmg}");
        slot.tempAtkBonus = 0;
        Logger.Info($"[Lane {lane + 1}] {slot.def.SlotName} FRONTE → lane vuota → -{dmg} HP player");
    }

    // ── Helper: pubblica l'evento Custom "SlotRetroEffect" per abilità passive ─
    static void FireSlotRetroEffect(SlotInstance slot, PlayerState player, PlayerState ai)
    {
        EventBus.Publish(GameEventType.Custom, new EventContext
        {
            owner    = ai,
            opponent = player,
            source   = slot,
            phase    = "SlotRetroEffect"
        });
    }
}
