using UnityEngine;

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance;

    public CardView SelectedOwned { get; private set; }
    public CardView SwapSource { get; private set; }
    public bool IsSwapArmed { get; private set; }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SelectOwned(CardView view)
    {
        if (view == null) return;

        // --- SE SIAMO IN MODALITÀ SWAP ED È LA SECONDA CARTA ---
        if (IsSwapArmed && SwapSource != null && view != SwapSource)
        {
            // esegue lo swap delle due carte sulla board del player
            GameManager.Instance.SwapCardPositions(SwapSource, view);

            // la carta selezionata prima perde l'highlight
            if (SelectedOwned != null)
                SelectedOwned.SetHighlight(false);

            // azzero selezioni / stato swap
            SelectedOwned = null;
            SwapSource = null;
            IsSwapArmed = false;

            return;
        }

        // --- SE CLICCO DI NUOVO LA STESSA CARTA MENTRE NON STO SWAPPANDO ---
        if (!IsSwapArmed && SelectedOwned == view) return;

        // --- SELEZIONE NORMALE ---
        if (SelectedOwned != null) SelectedOwned.SetHighlight(false);
        SelectedOwned = view;
        if (SelectedOwned != null) SelectedOwned.SetHighlight(true);

        EventBus.Publish(GameEventType.Info, new EventContext
        {
            phase = $"[SEL] #{view.instance.id} [L{GameManager.Instance.GetLaneIndexFor(view.instance) + 1}] {view.instance.def.cardName}"
        });
    }

    public void BeginSwap()
    {
        // se non c'è nessuna carta già selezionata, non ha senso armare lo swap
        if (SelectedOwned == null)
        {
            EventBus.Publish(GameEventType.Info, new EventContext
            {
                phase = "[Swap] Seleziona prima una carta da spostare"
            });
            return;
        }

        SwapSource = SelectedOwned;
        IsSwapArmed = true;

        EventBus.Publish(GameEventType.Info, new EventContext
        {
            phase = $"[Swap] In attesa della seconda carta per scambiare con #{SwapSource.instance.id}"
        });
    }


    public void ClearAll()
    {
        if (SelectedOwned != null) SelectedOwned.SetHighlight(false);
        SelectedOwned = null;

        SwapSource = null;
        IsSwapArmed = false;

        EventBus.Publish(GameEventType.Info, new EventContext
        {
            phase = "[SEL] #- [L-]"
        });
    }

}
