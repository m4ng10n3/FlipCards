using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance;

    public CardView SelectedOwned { get; private set; }
    public CardView SwapSource { get; private set; }
    public bool IsSwapArmed { get; private set; }
    public Transform SelectedEmptySpot { get; private set; }

    static readonly List<RaycastResult> _hits = new List<RaycastResult>(8);

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Un clic fuori da una carta o da una casella libera annulla la selezione,
    /// anche se non colpisce niente.
    ///
    /// PERCHE' NON UN BERSAGLIO DI FONDO: la strada ovvia sarebbe stendere un
    /// Raycast Target invisibile dietro tutto il tabellone e farci il clic
    /// sopra. In questo progetto e' la trappola gia' pagata piu' costosa —
    /// l'area della mano era esattamente questo e mangiava clic e hover delle
    /// carte in campo, "a volte", cioe' ogni volta che era sollevata. Qui non
    /// serve nessun bersaglio nuovo: si guarda il tasto e si chiede all'
    /// EventSystem cosa c'era sotto, e se non era niente di selezionabile si
    /// spegne.
    ///
    /// La decisione viene dal **raycast** e non dallo stato, quindi non importa
    /// se questo Update gira prima o dopo i gestori del clic. Conta invece
    /// escludere le due cose il cui clic la selezione la usa: una carta (in mano
    /// o in campo) e una casella libera. Se il clic su una casella libera
    /// arrivasse qui, cancellerebbe la selezione che il suo stesso gestore ha
    /// appena fatto, e la sequenza "casella prima, carta poi" non funzionerebbe
    /// piu' — a seconda dell'ordine di Update, cioe' in modo intermittente.
    ///
    /// La scheda agganciata nell'ispettore **non** si sgancia: e' agganciata
    /// proprio per poter guardare altrove senza perderla, e cade da sola quando
    /// il suo oggetto muore. Sono due cose diverse e restano tali.
    /// </summary>
    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

        ClearIfClickedAway(mouse.position.ReadValue());
    }

    /// <summary>
    /// Annulla la selezione se in <paramref name="screenPosition"/> non c'e'
    /// niente il cui clic la riguardi. Ritorna true se ha spento qualcosa.
    ///
    /// E' pubblica e separata da <see cref="Update"/> perche' e' la parte che si
    /// puo' provare: iniettare un clic finto nell'Input System non arriva
    /// all'EventSystem in questo progetto (stessa famiglia della trappola di
    /// <c>SetCursorPos</c> scritta in AGENTS.md), mentre chiamare questo metodo
    /// con una posizione esercita esattamente il codice che gira in partita —
    /// raycast compreso. All'Update resta solo la lettura del tasto.
    /// </summary>
    public bool ClearIfClickedAway(Vector2 screenPosition)
    {
        if (SelectedOwned == null && SelectedEmptySpot == null) return false;
        if (PointerOverSelectable(screenPosition)) return false;

        ClearAll();

        // Le caselle libere accese fanno parte della stessa attesa: se la
        // selezione cade, non deve restare niente illuminato a chiedere un clic.
        GameManager.Instance?.HighlightFreeSpots(false);
        return true;
    }

    /// <summary>
    /// Sotto il puntatore c'e' qualcosa il cui clic riguarda la selezione?
    ///
    /// Si cammina **verso l'alto** dal colpo (<c>GetComponentInParent</c>): e' la
    /// stessa risoluzione che fa Unity per consegnare l'evento, e non produce
    /// falsi positivi. Cercare verso il basso da un contenitore grande — il
    /// fondo del campo, per esempio — troverebbe le carte che ci stanno dentro
    /// e nessun clic deselezionerebbe mai piu'.
    /// </summary>
    bool PointerOverSelectable(Vector2 screenPosition)
    {
        var events = EventSystem.current;
        if (events == null) return false;

        var data = new PointerEventData(events) { position = screenPosition };
        _hits.Clear();
        events.RaycastAll(data, _hits);

        var gm = GameManager.Instance;
        string spotName = gm != null && gm.EmptySpot != null ? gm.EmptySpot.name : null;

        for (int i = 0; i < _hits.Count; i++)
        {
            var hit = _hits[i].gameObject;
            if (hit == null) continue;

            // Una carta: CardDefinition sta sulla radice e gestisce il suo clic.
            if (hit.GetComponentInParent<CardDefinition>() != null) return true;

            // Una casella libera: il suo clic seleziona, non deseleziona.
            if (spotName != null && HasAncestorNamed(hit.transform, spotName)) return true;
        }

        return false;
    }

    static bool HasAncestorNamed(Transform t, string name)
    {
        for (var current = t; current != null; current = current.parent)
            if (current.name == name) return true;
        return false;
    }

    public void SelectOwned(CardView view)
    {
        if (view == null) return;

        if (SelectedEmptySpot != null)
        {
            var outline = SelectedEmptySpot.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
            SelectedEmptySpot = null;
        }

        // --- SE SIAMO IN MODALITÀ SWAP ED È LA SECONDA CARTA ---
        if (IsSwapArmed && SwapSource != null && view != SwapSource)
        {
            // esegue lo swap delle due carte sulla board del player
            GameManager.Instance.SwapCardPositions(SwapSource, view);

            // azzero selezioni / stato swap
            if (SelectedOwned != null) SelectedOwned.ApplySelect(false);
            SelectedOwned = null;
            SwapSource = null;
            IsSwapArmed = false;

            return;
        }

        // --- RICLIC SULLA STESSA CARTA: DESELEZIONA ---
        // Senza questo la selezione non si puo' togliere: una volta scelta una
        // carta il tavolo resta per forza con una accesa, e riclickarla non fa
        // niente. Vale anche come annullamento dello swap, perche' la seconda
        // carta dello scambio e' gestita sopra: qui ci arriva solo la sorgente.
        if (SelectedOwned == view)
        {
            view.ApplySelect(false);
            SelectedOwned = null;
            SwapSource = null;
            IsSwapArmed = false;

            EventBus.Publish(GameEventType.Info, new EventContext
            {
                phase = "[SEL] #- [L-]"
            });
            return;
        }

        // --- SELEZIONE NORMALE ---
        if (SelectedOwned != null) SelectedOwned.ApplySelect(false);
        SelectedOwned = view;
        view.ApplySelect(true);

        EventBus.Publish(GameEventType.Info, new EventContext
        {
            phase = $"[SEL] #{view.instance.id} [L{GameManager.Instance.GetLaneIndexFor(view.instance) + 1}] {view.instance.def.cardName}"
        });
    }

    public void SelectEmptySpot(Transform spot)
    {
        // se clicco lo stesso spot e vuoi il "toggle" puoi gestirlo qui,
        // per ora semplicemente cambiamo selezione

        // 1) spegni highlight sulla carta selezionata (se c'è)
        if (SelectedOwned != null)
        {
            if (SelectedOwned != null) SelectedOwned.ApplySelect(false);
            SelectedOwned = null;
        }

        // lo swap non ha più senso se sto scegliendo uno spot vuoto
        SwapSource = null;
        IsSwapArmed = false;

        // 2) spegni highlight sul precedente empty spot
        if (SelectedEmptySpot != null)
        {
            var oldOutline = SelectedEmptySpot.GetComponent<Outline>();
            if (oldOutline != null) oldOutline.enabled = false;
        }

        // 3) aggiorna selezione
        SelectedEmptySpot = spot;

        // 4) accendi outline sul nuovo empty spot
        if (SelectedEmptySpot != null)
        {
            var outline = SelectedEmptySpot.GetComponent<Outline>();
            if (outline != null) outline.enabled = true;
        }

        EventBus.Publish(GameEventType.Info, new EventContext
        {
            phase = "[SEL] EmptySpot"
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
        if (SelectedOwned != null) SelectedOwned.ApplySelect(false);
        SelectedOwned = null;

        if (SelectedEmptySpot != null)
        {
            var outline = SelectedEmptySpot.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
            SelectedEmptySpot = null;
        }

        SwapSource = null;
        IsSwapArmed = false;

        EventBus.Publish(GameEventType.Info, new EventContext
        {
            phase = "[SEL] #- [L-]"
        });
    }

}
