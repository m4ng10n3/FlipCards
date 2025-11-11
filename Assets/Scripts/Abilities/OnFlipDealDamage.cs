using UnityEngine;

public class OnFlipDealDamage : AbilityBase
{
    [Min(1)] public int damage = 1;
    public bool onlyWhenToFront = true;

    private EventBus.Handler _h;

    protected override void Register()
    {
        _h = (t, ctx) =>
        {
            if (ctx.source != Source) return;

            if (t == GameEventType.Flip)
            {
                if (!onlyWhenToFront || Source.side == Side.Fronte)
                {
                    // Mostra SEMPRE l’hint quando l’abilità si attiva
                    EventBus.Publish(GameEventType.Info, new EventContext
                    {
                        owner = Owner,
                        opponent = Opponent,
                        source = Source,
                        phase = "HINT: Flip Damage"
                    });

                    // Trova lo SLOT opposto nella stessa lane
                    var gm = GameManager.Instance;
                    if (gm == null || gm.aiBoardRoot == null) return;

                    // Recupera la lane da CardView della Source
                    if (!gm.TryGetView(Source, out var srcView) || srcView == null) return;
                    int lane = srcView.transform.GetSiblingIndex();
                    if (lane < 0 || lane >= gm.aiBoardRoot.childCount) return;

                    var aChild = gm.aiBoardRoot.GetChild(lane);
                    var sView = aChild ? aChild.GetComponentInChildren<SlotView>(includeInactive: false) : null;
                    var slot = sView ? sView.instance : null;

                    if (slot == null || !slot.alive)
                    {
                        // Nessun bersaglio -> opzionale: messaggio
                        // Source.PushHint("No target");
                        return;
                    }

                    // Attacca lo SLOT (Attack accetta object come target)
                    Source.Attack(ctx.owner, ctx.opponent, slot);
                }
            }
        };


        EventBus.Subscribe(GameEventType.Flip, _h);
    }


    protected override void Unregister()
    {
        if (_h != null) EventBus.Unsubscribe(GameEventType.Flip, _h);
    }
}
