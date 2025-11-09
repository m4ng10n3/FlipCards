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
        GameManagerInteractive.Logf("[SEL] Owned={0} Enemy={1}", SelectedOwned, SelectedEnemy);
        if (SelectedOwned != null) SelectedOwned.SetHighlight(false);
        SelectedOwned = view;
        if (SelectedOwned != null) SelectedOwned.SetHighlight(true);
    }

    public void SelectEnemy(CardView view)
    {
        GameManagerInteractive.Logf("[SEL] Owned={0} Enemy={1}", SelectedOwned, SelectedEnemy);
        if (SelectedEnemy != null) SelectedEnemy.SetHighlight(false);
        SelectedEnemy = view;
        if (SelectedEnemy != null) SelectedEnemy.SetHighlight(true);
    }

    public void ClearAll()
    {
        GameManagerInteractive.Logf("[SEL] Owned={0} Enemy={1}", SelectedOwned, SelectedEnemy);
        if (SelectedOwned != null) SelectedOwned.SetHighlight(false);
        if (SelectedEnemy != null) SelectedEnemy.SetHighlight(false);
        SelectedOwned = null;
        SelectedEnemy = null;
    }
}
