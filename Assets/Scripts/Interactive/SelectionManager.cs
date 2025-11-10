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
        if (SelectedOwned != null) SelectedOwned.SetHighlight(false);
        SelectedOwned = view;
        if (SelectedOwned != null) SelectedOwned.SetHighlight(true);

        EventBus.Publish(GameEventType.Info, new EventContext
        {
            phase = $"[SEL] Owned={Label(SelectedOwned)}  Enemy={Label(SelectedEnemy)}"
        });
    }

    public void SelectEnemy(CardView view)
    {
        if (SelectedEnemy != null) SelectedEnemy.SetHighlight(false);
        SelectedEnemy = view;
        if (SelectedEnemy != null) SelectedEnemy.SetHighlight(true);

        EventBus.Publish(GameEventType.Info, new EventContext
        {
            phase = $"[SEL] Owned={Label(SelectedOwned)}  Enemy={Label(SelectedEnemy)}"
        });
    }

    public void ClearAll()
    {
        if (SelectedOwned != null) SelectedOwned.SetHighlight(false);
        if (SelectedEnemy != null) SelectedEnemy.SetHighlight(false);
        SelectedOwned = null;
        SelectedEnemy = null;

        EventBus.Publish(GameEventType.Info, new EventContext
        {
            phase = "[SEL] Owned=-  Enemy=-"
        });
    }

    // ---------- Helpers ----------
    string Label(CardView v)
    {
        if (v == null) return "-";
        var gm = GameManager.Instance;
        if (gm == null) return v.name;

        int lane = v.transform.GetSiblingIndex() + 1;            // 1..N da sinistra a destra
        string side = (v.owner == gm.player) ? "P" : "E";        // P = Player, E = Enemy
        string nm = v.instance != null ? v.instance.def.cardName : v.name;
        return $"{side}#{lane} {nm}";
    }
}
