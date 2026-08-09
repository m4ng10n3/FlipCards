using UnityEngine;
using System.Collections.Generic;

public class HandManager : MonoBehaviour
{
    [Header("Hand settings")]
    [SerializeField] private int maxHandSize = 8;
    [Tooltip("Passo fra due carte in mano, in pixel di canvas. Piu' stretto della " +
             "larghezza della carta: le carte in mano DEVONO sovrapporsi, e' il " +
             "ventaglio. A 0 si torna al vecchio ripiego larghezza/maxHandSize, " +
             "che dava un passo piu' largo della carta e quindi una fila staccata.")]
    [SerializeField] private float handSpacing = 132f;
    [Header("Hierarchy")]
    [SerializeField] private Transform handRoot;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float spawnScaleMultiplier = 1.5f;
    [Header("Runtime debug")]
    [SerializeField] private CardView selectedCard;

    private readonly List<CardView> handCards = new();
    private readonly List<GameObject> deck = new();
    private bool deckInitialized;
    private int deckStartCount;
    private RectTransform handRect;
    private const float PositionThresholdSqr = 0.0001f;
    private const float RotationThreshold = 0.1f;

    public Transform HandRoot => handRoot;
    private CardView draggingCard;

    // Contatori per la HUD e per la pila del mazzo.
    public int MaxHandSize => maxHandSize;
    public int HandCount => handCards.Count;
    public int DeckCount => deckInitialized ? deck.Count : CountDeckFromBindings();
    public bool HandIsFull => handCards.Count >= maxHandSize;

    /// <summary>
    /// Carte del giocatore uscite dal gioco: la pila degli scarti del tabellone.
    /// Non e' una zona vera — una carta distrutta viene semplicemente rimossa —
    /// ma il conteggio e' reale, e senza di esso non si sa quanto mazzo si e'
    /// gia' bruciato in una partita che dura 12 turni.
    /// </summary>
    public int DiscardCount { get; private set; }

    /// <summary>La chiama GameManager quando una carta del giocatore lascia il campo.</summary>
    public void NotifyDiscarded() => DiscardCount++;

    /// <summary>Carte nel mazzo a inizio partita: e' il riferimento per lo spessore della pila.</summary>
    public int DeckStartCount => deckStartCount > 0 ? deckStartCount : CountDeckFromBindings();

    /// <summary>
    /// I prossimi prefab in ordine di pesca. Ha senso solo perche' il mazzo e'
    /// mescolato una volta sola e si pesca dalla cima: con la pesca casuale a ogni
    /// estrazione "la prossima carta" non esisterebbe.
    /// </summary>
    public IReadOnlyList<GameObject> PeekDeck(int count)
    {
        EnsureDeck();
        var top = new List<GameObject>(Mathf.Max(0, count));
        for (int i = 0; i < count && i < deck.Count; i++)
            top.Add(deck[i]);
        return top;
    }

    /// <summary>Conteggio del mazzo prima della prima pesca, senza costruirlo.</summary>
    private int CountDeckFromBindings()
    {
        var gm = GameManager.Instance;
        if (gm == null) return 0;

        int total = 0;
        foreach (var binding in gm.playerCards)
            if (binding != null && binding.prefab != null) total += Mathf.Max(0, binding.count);

        foreach (Transform child in gm.playerBoardRoot)
            if (child.GetComponentInChildren<CardView>(false) != null) total--;

        return Mathf.Max(0, total);
    }

    private void Awake()
    {
        if (handRoot == null || spawnPoint == null)
            throw new System.InvalidOperationException("HandManager references not assigned");

        handRect = handRoot as RectTransform;
    }

    private void Start()
    {
        SyncHandCardsFromChildren();
        UpdateCardsPosition();
    }

    private void Update()
    {
        UpdateCardsPosition();
    }

    private void SyncHandCardsFromChildren()
    {
        handCards.Clear();
        foreach (Transform child in handRoot)
        {
            var cv = child.GetComponentInChildren<CardView>();
            if (cv != null) handCards.Add(cv);
        }

        if (draggingCard != null && !handCards.Contains(draggingCard))
            handCards.Add(draggingCard);

        handCards.RemoveAll(c => c == null);
    }

    private void RebuildDeckFromBindings()
    {
        deck.Clear();
        var gm = GameManager.Instance ?? throw new System.InvalidOperationException("GameManager missing");
        var remainingByName = new Dictionary<string, int>();

        foreach (var binding in gm.playerCards)
        {
            if (binding.count <= 0) continue;

            string name = binding.prefab.name;
            if (!remainingByName.ContainsKey(name)) remainingByName[name] = 0;
            remainingByName[name] += binding.count;
        }

        foreach (Transform child in gm.playerBoardRoot)
        {
            var view = child.GetComponentInChildren<CardView>(false);
            if (view == null) continue;

            string instName = view.gameObject.name;
            int cloneIdx = instName.IndexOf("(Clone)");
            if (cloneIdx >= 0) instName = instName.Substring(0, cloneIdx);
            instName = instName.Trim();

            if (remainingByName.TryGetValue(instName, out int count) && count > 0)
                remainingByName[instName] = count - 1;
        }

        foreach (var binding in gm.playerCards)
        {
            string name = binding.prefab.name;
            if (!remainingByName.TryGetValue(name, out int remaining) || remaining <= 0) continue;
            for (int i = 0; i < remaining; i++) deck.Add(binding.prefab);
        }

        // Si mescola qui, una volta sola, e si pesca dalla cima. Estrarre a caso a
        // ogni pesca rendeva "la prossima carta" inesistente, e senza prossima
        // carta la pila del mazzo non puo' mostrare cosa sta per arrivare.
        var rng = gm.Rng ?? new System.Random();
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }

        deckStartCount = deck.Count;
    }

    private void EnsureDeck()
    {
        if (deckInitialized) return;
        RebuildDeckFromBindings();
        deckInitialized = true;
    }

    /// <summary>
    /// Pesca dalla cima. Ritorna false e lo dice nel log quando non si puo':
    /// uscire in silenzio faceva sembrare rotto il comando.
    /// </summary>
    public bool DrawCard()
    {
        var gm = GameManager.Instance ?? throw new System.InvalidOperationException("GameManager missing");

        EnsureDeck();

        if (deck.Count == 0)
        {
            Logger.Info("Pesca: il mazzo e' vuoto");
            return false;
        }

        if (handCards.Count >= maxHandSize)
        {
            Logger.Info($"Pesca: mano piena ({handCards.Count}/{maxHandSize})");
            return false;
        }

        if (!gm.TrySpendPlayerAP(gm.drawCardCost, "Draw"))
            return false;

        GameObject cardPrefabToSpawn = deck[0];
        deck.RemoveAt(0);

        GameObject go = Instantiate(cardPrefabToSpawn, handRoot);
        go.name = cardPrefabToSpawn.name;
        go.SetActive(true);
        go.transform.localScale = Vector3.one * spawnScaleMultiplier;

        var cv = go.GetComponentInChildren<CardView>() ?? throw new System.InvalidOperationException("Card prefab missing CardView");
        cv.gm = gm;

        go.transform.position = spawnPoint.position;
        go.transform.rotation = spawnPoint.rotation;

        RegisterHandCard(cv);
        Logger.Info($"Draw: {cv.GetComponentInParent<CardDefinition>()?.cardName ?? cv.gameObject.name}");
        UpdateCardsPosition();
        return true;
    }

    private void RegisterHandCard(CardView cv)
    {
        if (!handCards.Contains(cv)) handCards.Add(cv);

        var container = cv.EnsureHandContainer(handRoot);
        if (container == null) throw new System.InvalidOperationException("Cannot create hand container");
        if (container.parent != handRoot) container.SetParent(handRoot, true);
        container.SetAsLastSibling();

        SortHandCardsBySlotIndex();
    }
    public void RemoveFromHand(CardView cv)
    {
        if (cv == null) return;

        if (draggingCard == cv) OnHandCardEndDrag(cv);

        if (handCards.Remove(cv))
        {
            var container = cv.handContainer != null ? cv.handContainer.transform : null;
            if (container != null && container.parent == handRoot) Destroy(container.gameObject);
            Destroy(cv.GetComponentInParent<CardDefinition>().gameObject);
            UpdateCardsPosition();
        }
    }

    public int GetCardHandIndex(CardView cv)
    {
        if (cv == null) return 0;
        int idx = handCards.IndexOf(cv);
        return idx >= 0 ? idx : 0;
    }

    public void RestoreContainerSiblingIndices()
    {
        if (handCards.Count == 0 || handRoot == null) return;
        for (int i = 0; i < handCards.Count; i++)
        {
            var card = handCards[i];
            if (card != null && card.handContainer != null && card.handContainer.parent == handRoot)
            {
                card.handContainer.SetSiblingIndex(i);
            }
        }
    }

    public void UpdateCardsPosition()
    {
        SyncHandCardsFromChildren();
        if (handCards.Count == 0 || handRoot == null) return;

        SortHandCardsBySlotIndex();

        float width = handRect != null ? handRect.rect.width : 600f;
        float spacing = handSpacing > 0f ? handSpacing : width / Mathf.Max(1, maxHandSize);
        float startX = -spacing * (handCards.Count - 1) * 0.5f;

        int slotCount = handCards.Count;
        for (int i = 0; i < slotCount; i++)
        {
            var card = handCards[i];
            var container = card.handContainer ?? card.EnsureHandContainer(handRoot);
            if (container == null) throw new System.InvalidOperationException("Hand container missing");
            if (container.parent != handRoot) container.SetParent(handRoot, true);

            Vector3 finalLocalPos = new Vector3(startX + i * spacing, 0f, 0f);

            bool needsMove = (container.localPosition - finalLocalPos).sqrMagnitude > PositionThresholdSqr;
            if (needsMove)
                container.localPosition = finalLocalPos;

            if (Quaternion.Angle(container.localRotation, Quaternion.identity) > RotationThreshold)
                container.localRotation = Quaternion.identity;
        }
    }

    private void SortHandCardsBySlotIndex()
    {
        handCards.RemoveAll(c => c == null);
        handCards.Sort((a, b) =>
        {
            var ta = a != null ? a.handContainer : null;
            var tb = b != null ? b.handContainer : null;
            int ia = ta != null ? ta.GetSiblingIndex() : int.MaxValue;
            int ib = tb != null ? tb.GetSiblingIndex() : int.MaxValue;
            return ia.CompareTo(ib);
        });
    }

    public void ReorderHandDuringDrag(CardView movingCard, Vector3 dragPosition)
    {
        if (movingCard == null) throw new System.InvalidOperationException("Missing moving card");

        var sourceContainer = movingCard.handContainer ?? movingCard.EnsureHandContainer(handRoot);
        if (sourceContainer == null) throw new System.InvalidOperationException("Missing container for moving card");

        var containers = new List<RectTransform>();
        foreach (Transform child in handRoot)
        {
            if (child is RectTransform rt) containers.Add(rt);
        }

        if (containers.Count == 0) return;

        float dragLocalX = handRoot.InverseTransformPoint(dragPosition).x;
        int targetIndex = 0;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < containers.Count; i++)
        {
            float dist = Mathf.Abs(dragLocalX - containers[i].localPosition.x);
            if (dist < bestDistance)
            {
                bestDistance = dist;
                targetIndex = i;
            }
        }

        var targetContainer = containers[targetIndex];
        if (targetContainer == sourceContainer) return;

        var targetCard = targetContainer.GetComponentInChildren<CardView>(includeInactive: false);

        movingCard.handContainer = targetContainer;
        movingCard.GetComponent<RectTransform>().parent.SetParent(targetContainer);
        movingCard.MoveInHandRequest = true;
        if (targetCard != null)
        {
            targetCard.handContainer = sourceContainer;
            targetCard.GetComponent<RectTransform>().parent.SetParent(sourceContainer);
            targetCard.MoveInHandRequest = true;
        }
        SortHandCardsBySlotIndex();
        UpdateCardsPosition();
    }

    public void OnHandCardBeginDrag(CardView view)
    {
        draggingCard = view;
        selectedCard = view;
        view?.EnsureHandContainer(handRoot);
        SortHandCardsBySlotIndex();
        UpdateCardsPosition();
    }

    public void OnHandCardEndDrag(CardView view = null)
    {
        if (view == selectedCard || view == null) selectedCard = null;
        draggingCard = null;
        SortHandCardsBySlotIndex();
        UpdateCardsPosition();
    }

    public void OnHandCardDroppedToBoard(CardView view)
    {
        if (draggingCard == view) draggingCard = null;
        if (selectedCard == view) selectedCard = null;
        SortHandCardsBySlotIndex();
        UpdateCardsPosition();
    }
}
