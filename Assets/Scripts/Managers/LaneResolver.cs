using UnityEngine;

public static class LaneResolver
{
    public static void Resolve(int laneIndex, CardInstance card, SlotInstance slot, PlayerState player, PlayerState ai)
    {
        if (card != null && slot != null)
        {
            if (card.side == Side.Fronte)
                ResolveCardPressure(laneIndex, card, slot, player, ai);
            else if (slot.side == Side.Fronte)
                ResolveSlotPressure(laneIndex, slot, card, player, ai);

            if (slot.alive && slot.side == Side.Retro)
                FireSlotRetroEffect(slot, player, ai);

            return;
        }

        if (card != null)
        {
            if (card.side == Side.Fronte)
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
        card.Attack(player, ai, slot);

        if (slotWasAlive && !slot.alive)
            GameManager.Instance?.DealBossPressureFromBreak(card, GameManager.Instance.bossDamageOnSlotBreak, laneIndex);

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
                amount = damage
            });

            Logger.Info($"Lane {laneIndex + 1}: {slot.def.SlotName} presses {card.def.cardName}");
        }

        slot.tempAtkBonus = 0;
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
            Logger.Info($"Lane {laneIndex + 1}: {card.def.cardName} hits boss for {damage}");
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
        slot.tempAtkBonus = 0;

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
            Logger.Info($"Lane {laneIndex + 1}: {slot.def.SlotName} hits player for {damage}");
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
