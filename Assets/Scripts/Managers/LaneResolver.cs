using UnityEngine;

public static class LaneResolver
{
    public static void Resolve(int laneIndex, CardInstance card, SlotInstance slot, PlayerState player, PlayerState ai, bool playerAttacks = true)
    {
        if (card != null && slot != null)
        {
            if (playerAttacks && card.side == Side.Fronte)
                ResolveCardPressure(laneIndex, card, slot, player, ai);
            else if (slot.side == Side.Fronte)
                ResolveSlotPressure(laneIndex, slot, card, player, ai);

            if (slot.alive && slot.side == Side.Retro)
                FireSlotRetroEffect(slot, player, ai);

            return;
        }

        if (card != null)
        {
            if (playerAttacks && card.side == Side.Fronte)
                DirectCardPressure(laneIndex, card, player, ai);
            return;
        }

        if (slot != null)
        {
            if (slot.side == Side.Fronte)
                DirectSlotPressure(laneIndex, slot, player, ai);
            else
                FireSlotRetroEffect(slot, player, ai);
        }
    }

    static void ResolveCardPressure(int laneIndex, CardInstance card, SlotInstance slot, PlayerState player, PlayerState ai)
    {
        bool slotWasAlive = slot.alive;
        // Risonanza: stessa fazione in corsia, la guardia della casella non tiene.
        card.Attack(player, ai, slot, SynergyResolver.Resonates(GameManager.Instance, laneIndex));

        if (slotWasAlive && !slot.alive)
            GameManager.Instance?.AnnouncePoolBreak(slot);

        if (slot.alive && slot.side == Side.Fronte)
            ResolveSlotPressure(laneIndex, slot, card, player, ai);
    }

    static void ResolveSlotPressure(int laneIndex, SlotInstance slot, CardInstance card, PlayerState player, PlayerState ai)
    {
        EventBus.Publish(GameEventType.Custom, new EventContext
        {
            owner = ai,
            opponent = player,
            source = slot,
            target = card,
            phase = "PreSlotAttack"
        });

        int damage = Mathf.Max(0, slot.def.atkDamage + slot.tempAtkBonus);
        if (card != null)
        {
            EventBus.Publish(GameEventType.AttackDeclared, new EventContext
            {
                owner = ai,
                opponent = player,
                source = slot,
                target = card,
                amount = damage,
                // La risonanza taglia da tutte e due le parti: se il tuo colpo
                // passa la sua guardia, il suo passa la tua.
                ignoreBlock = SynergyResolver.Resonates(GameManager.Instance, laneIndex)
            });

            Logger.Info($"Corsia {laneIndex + 1}: {slot.def.SlotName} colpisce {card.def.cardName}");
        }

        slot.ClearAtkBonus();
    }

    static void DirectCardPressure(int laneIndex, CardInstance card, PlayerState player, PlayerState ai)
    {
        int damage = Mathf.Max(0, card.ComputeAttackDamage());
        card.ConsumeCharge();
        ai.TakeDamage(damage);
        GameManager.Instance?.UpdateHUD();

        EventBus.Publish(GameEventType.AttackResolved, new EventContext
        {
            owner = player,
            opponent = ai,
            source = card,
            target = null,
            amount = damage,
            phase = "DirectToEnemy"
        });

        if (damage > 0)
        {
            card.PushHint($"Boss -{damage}");
            Logger.Info($"Corsia {laneIndex + 1}: {card.def.cardName} colpisce il boss scoperto per {damage}");
        }
    }

    static void DirectSlotPressure(int laneIndex, SlotInstance slot, PlayerState player, PlayerState ai)
    {
        EventBus.Publish(GameEventType.Custom, new EventContext
        {
            owner = ai,
            opponent = player,
            source = slot,
            target = null,
            phase = "PreSlotAttack"
        });

        int damage = Mathf.Max(0, slot.def.atkDamage + slot.tempAtkBonus);
        slot.ClearAtkBonus();

        player.TakeDamage(damage);
        GameManager.Instance?.UpdateHUD();

        EventBus.Publish(GameEventType.AttackResolved, new EventContext
        {
            owner = ai,
            opponent = player,
            source = slot,
            target = null,
            amount = damage,
            phase = "DirectToPlayer"
        });

        if (damage > 0)
        {
            slot.PushHint($"Player -{damage}");
            Logger.Info($"Corsia {laneIndex + 1}: corsia scoperta, {slot.def.SlotName} colpisce il giocatore per {damage}");
        }
    }

    static void FireSlotRetroEffect(SlotInstance slot, PlayerState player, PlayerState ai)
    {
        EventBus.Publish(GameEventType.Custom, new EventContext
        {
            owner = ai,
            opponent = player,
            source = slot,
            phase = "SlotRetroEffect"
        });
    }
}
