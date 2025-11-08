using UnityEngine;

public interface IAbility
{
    // chiamato quando la carta viene creata sul board
    void Bind(CardInstance source, PlayerState owner, PlayerState opponent);
    // chiamato quando la carta lascia il board / match end
    void Unbind();
}

public abstract class AbilityBase : MonoBehaviour, IAbility
{
    protected CardInstance Source;
    protected PlayerState Owner, Opponent;

    public virtual void Bind(CardInstance source, PlayerState owner, PlayerState opponent)
    {
        Source = source; Owner = owner; Opponent = opponent;
        Register(); // sottoscrizioni concrete
    }

    public virtual void Unbind()
    {
        Unregister(); // rimuovi sottoscrizioni
        Source = null; Owner = null; Opponent = null;
    }

    protected abstract void Register();
    protected abstract void Unregister();

    // helper comodi per pubblicare altri eventi/effect
    protected void Publish(GameEventType t, EventContext ctx) => EventBus.Publish(t, ctx);
}
