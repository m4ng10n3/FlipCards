using UnityEngine;

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance;

    public CardView SelectedOwned { get; private set; }
    public CardView SelectedEnemy { get; private set; }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SelectOwned(CardView view)
    {
        if (SelectedOwned == view) return;
        if (SelectedOwned != null) SelectedOwned.SetHighlight(false);
        SelectedOwned = view;
        if (SelectedOwned != null) SelectedOwned.SetHighlight(true);

        EventBus.Publish(GameEventType.Info, new EventContext
        {
            phase = $"[SEL] #{view.instance.id} [L{GameManager.Instance.GetLaneIndexFor(view.instance) + 1}] {view.instance.def.cardName}"
        });
    }

    public void ClearAll()
    {
        if (SelectedOwned != null) SelectedOwned.SetHighlight(false);
        SelectedOwned = null;

        EventBus.Publish(GameEventType.Info, new EventContext
        {
            phase = "[SEL] #- [L-]"
        });
    }
}
