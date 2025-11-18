using System.ComponentModel;
using UnityEngine;

/// <summary>
/// Abilità per SLOT: quando arriva il suo turno di agire (fase "SlotEffect"),
/// infligge 'damage' alla carta di fronte, se presente.
/// </summary>
public class SlotStrikeOnAct : AbilityBase
{
    [Min(1)] public int damage = 2;

    private EventBus.Handler _h;
    private SlotView _slotView;
    private SlotInstance _slot;

    protected override void Register()
    {
        // L’abilità è montata sul prefab dello slot: recupero SlotView/SlotInstance
        _slotView = GetComponent<SlotView>();
        _slot = _slotView ? _slotView.instance : null;

        _slotView.ClearHint();
        _slotView.ShowHint($"incoming damage {damage}");
        var gm = GameManager.Instance;

        _h = (t, ctx) =>
        {
            if (_slot == null || !_slot.alive) return;
            if (t == GameEventType.TurnEnd && ReferenceEquals(ctx.owner, gm.ai))
            {
                Logger.Info("pippo");
                _slotView.ClearHint();
                _slotView.ShowHint($"incoming damage {damage}");
                return;
            }
            // Agiamo solo nella fase dedicata agli slot
            if (ctx.phase != "SlotEffect") return;

            // E solo quando l'evento riguarda proprio questo slot
            if (!ReferenceEquals(ctx.source, _slot)) return;

            // Trova la carta di fronte nella stessa lane
            if (gm == null || gm.playerBoardRoot == null || _slotView == null) return;

            int lane = _slotView.transform.GetSiblingIndex();
            if (lane < 0 || lane >= gm.playerBoardRoot.childCount) return;

            var pChild = gm.playerBoardRoot.GetChild(lane);
            var pView = pChild ? pChild.GetComponentInChildren<CardView>(includeInactive: false) : null;
            var target = pView ? pView.instance : null;

            if (target == null || !target.alive)
            {
                EventBus.Publish(GameEventType.AttackResolved, new EventContext
                {
                    owner = Owner,
                    opponent = Opponent,
                    source = _slot,
                    target = null,             // danno diretto al player
                    amount = damage,
                    phase = "Direct Damage"
                });
                DealDamageToPlayer(Owner, Opponent, damage);
                return;
            }
            else
            {
                // Usa la pipeline eventi standard: AttackDeclared + IncomingAttack.
                // owner = AI (Owner), opponent = Player (Opponent), source = questo SlotInstance
                EventBus.Publish(GameEventType.AttackDeclared, new EventContext
                {
                    owner = Owner,
                    opponent = Opponent,
                    source = _slot,
                    target = target,
                    amount = Mathf.Max(0, damage),
                    phase = "SlotStrikeOnAct"
                });
            }

            EventBus.Publish(GameEventType.Info, new EventContext
            {
                owner = Owner,             // AI
                opponent = Opponent,       // Player
                source = _slot,            // SlotInstance locale
                phase = "HINT: Strike!"
            });


        };

        EventBus.Subscribe(GameEventType.Custom, _h);
        EventBus.Subscribe(GameEventType.TurnEnd, _h);
    }

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

    protected override void Unregister()
    {
        if (_h != null)
        {
            EventBus.Unsubscribe(GameEventType.Custom, _h);
            _h = null;
        }

        _slotView = null;
        _slot = null;
    }
}
