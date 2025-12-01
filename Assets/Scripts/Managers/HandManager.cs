using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class HandManager : MonoBehaviour
{
    [Header("Hand settings")]
    [SerializeField] private int maxHandSize = 5;

    [Header("Hierarchy")]
    [SerializeField] private Transform handRoot;         // parent delle carte (RectTransform sotto Canvas)
    [SerializeField] private Transform spawnPoint;       // punto da cui far apparire le carte
    [SerializeField] private float spawnScaleMultiplier = 1.5f;

    [Header("UI")]
    [SerializeField] public Button btnDraw;

    [Header("Runtime debug")]
    [SerializeField] private CardView selectedCard;
    [SerializeField] private CardView hoveredCard;

    private readonly List<CardView> handCards = new();
    private readonly List<Transform> layoutBuffer = new();
    private readonly List<GameObject> deck = new();
    private bool deckInitialized = false;
    private RectTransform handRect;
    private const float PositionThresholdSqr = 0.0001f;
    private const float RotationThreshold = 0.1f;
    private Vector2 lastHandSize;
    private Transform lastHandParent;
    private Vector3 lastHandScale;

    public Transform HandRoot => handRoot;

    // Stato runtime per gestione della mano reattiva
    private CardView draggingCard;
    private Transform activePlaceholder;

    private void Awake()
    {
        if (btnDraw != null)
            btnDraw.onClick.AddListener(DrawCard);
        else
            Debug.LogWarning("[HandManager] btnDraw non assegnato nell'Inspector.");

        handRect = handRoot as RectTransform;
        CacheHandRootState();
    }

    private void Start()
    {
        SyncHandCardsFromChildren();
        UpdateCardsPosition();
    }


    private void SyncHandCardsFromChildren()
    {
        handCards.Clear();

        if (handRoot == null)
            return;

        foreach (Transform child in handRoot)
        {
            var cv = child.GetComponentInChildren<CardView>();
            if (cv != null && !handCards.Contains(cv))
                handCards.Add(cv);
        }

        if (draggingCard != null && !handCards.Contains(draggingCard))
            handCards.Add(draggingCard);

        handCards.RemoveAll(c => c == null);
    }


    private void RebuildDeckFromBindings()
    {
        deck.Clear();

        var gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogError("[HandManager] GameManager.Instance non trovato per costruire il deck!");
            return;
        }

        // 1) Copie totali per tipo di carta (per nome prefab) dai bindings
        var remainingByName = new Dictionary<string, int>();

        foreach (var binding in gm.playerCards)
        {
            if (binding.prefab == null || binding.count <= 0)
                continue;

            string name = binding.prefab.name;
            if (!remainingByName.ContainsKey(name))
                remainingByName[name] = 0;

            remainingByName[name] += binding.count;
        }

        // 2) Sottraggo le copie che sono gia in gioco sul board del player
        if (gm.playerBoardRoot != null)
        {
            foreach (Transform child in gm.playerBoardRoot)
            {
                var view = child.GetComponentInChildren<CardView>(false);
                if (view == null)
                    continue;

                string instName = view.gameObject.name;
                int cloneIdx = instName.IndexOf("(Clone)");
                if (cloneIdx >= 0)
                    instName = instName.Substring(0, cloneIdx);

                instName = instName.Trim();

                if (remainingByName.TryGetValue(instName, out int count) && count > 0)
                {
                    remainingByName[instName] = count - 1;
                }
            }
        }

        // 3) Ricostruisco il deck solo con le copie rimanenti (non in gioco)
        foreach (var binding in gm.playerCards)
        {
            if (binding.prefab == null)
                continue;

            string name = binding.prefab.name;
            if (!remainingByName.TryGetValue(name, out int remaining) || remaining <= 0)
                continue;

            for (int i = 0; i < remaining; i++)
            {
                deck.Add(binding.prefab);
            }
        }

        Debug.Log($"[HandManager] Deck ricostruito: {deck.Count} carte disponibili.");
    }



    public void DrawCard()
    {
        var gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogError("[HandManager] GameManager.Instance non trovato!");
            return;
        }

        // Inizializzo il deck solo alla prima pesca,
        // quando le carte iniziali sono gia state messe in campo
        if (!deckInitialized)
        {
            RebuildDeckFromBindings();
            deckInitialized = true;
        }

        // Nessuna carta disponibile nel deck
        if (deck.Count == 0)
        {
            Debug.Log("[HandManager] Deck vuoto: nessuna carta pescabile.");
            return;
        }

        // Limite di carte in mano
        if (handCards.Count >= maxHandSize)
            return;

        // Pescare costa punti abilita
        if (gm.player.actionPoints <= 0)
        {
            Debug.Log("[HandManager] Nessun PA disponibile per pescare.");
            return;
        }

        gm.player.actionPoints -= 1;
        gm.UpdateHUD();

        if (handRoot == null)
        {
            Debug.LogError("[HandManager] handRoot non assegnato!");
            return;
        }

        if (spawnPoint == null)
        {
            Debug.LogWarning("[HandManager] spawnPoint non assegnato, uso posizione/rotazione di handRoot.");
        }

        // Pesca randomica dal deck del player
        int deckIndex = Random.Range(0, deck.Count);
        GameObject cardPrefabToSpawn = deck[deckIndex];
        deck.RemoveAt(deckIndex);   // la carta pescata esce dal deck

        // Istanzia la carta pescata come figlio di handRoot
        GameObject go = Instantiate(cardPrefabToSpawn, handRoot);
        go.name = cardPrefabToSpawn.name;
        go.SetActive(true);
        go.transform.localScale = Vector3.one * spawnScaleMultiplier;

        var cv = go.GetComponent<CardView>();
        if (cv != null)
        {
            cv.gm = GameManager.Instance;
            cv.SetHighlight(false);

            // Assicuro che la carta in mano sia cliccabile
            var btn = go.GetComponent<Button>() ?? go.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(cv.OnClicked);
        }


        // Posizione iniziale = spawnPoint (o handRoot come fallback)
        if (spawnPoint != null)
        {
            go.transform.position = spawnPoint.position;
            go.transform.rotation = spawnPoint.rotation;
        }
        else
        {
            go.transform.position = handRoot.position;
            go.transform.rotation = handRoot.rotation;
        }

        RegisterHandCard(cv);

        // Gestisce la posizione in campo (mano) lungo la spline
        UpdateCardsPosition();
    }

    private void RegisterHandCard(CardView cv)
    {
        if (cv == null)
            return;

        if (!handCards.Contains(cv))
            handCards.Add(cv);

        if (handRoot != null && cv.transform.parent != handRoot)
        {
            cv.EnsureHandContainer(handRoot);
            var container = cv.HandContainer != null ? cv.HandContainer.transform : null;
            if (container != null && container.parent != handRoot)
            {
                container.SetParent(handRoot, true);
            }
            if (container != null)
                container.SetAsLastSibling();
        }
        else
        {
            cv.EnsureHandContainer(handRoot);
        }

        SortHandCardsBySlotIndex();
    }

    public void RemoveFromHand(GameObject cardGO)
    {
        RemoveFromHand(cardGO != null ? cardGO.GetComponent<CardView>() : null);
    }

    public void RemoveFromHand(CardView cv)
    {
        if (cv == null) return;

        // se sto trascinando proprio questa carta, resetta lo stato di drag della mano
        if (draggingCard == cv)
            OnHandCardEndDrag(cv);

        if (handCards.Remove(cv))
        {
            var container = cv.HandContainer != null ? cv.HandContainer.transform : null;
            if (container != null && container.parent == handRoot)
                Destroy(container.gameObject);
            Destroy(cv.gameObject);
            UpdateCardsPosition();
        }
    }

    public void UpdateCardsPosition()
    {
        UpdateCardsPosition(activePlaceholder);
    }

    private Transform SanitizePlaceholder(Transform placeholder)
    {
        return placeholder != null && handRoot != null && placeholder.parent == handRoot ? placeholder : null;
    }

    private void Update()

    {
        UpdateCardsPosition(activePlaceholder);
    }



    private void CacheHandRootState()

    {

        lastHandSize = handRect != null ? handRect.rect.size : Vector2.zero;

        lastHandParent = handRoot != null ? handRoot.parent : null;

        lastHandScale = handRoot != null ? handRoot.lossyScale : Vector3.one;

    }

    private List<Transform> BuildLayoutList(Transform placeholder)
    {
        layoutBuffer.Clear();

        placeholder = SanitizePlaceholder(placeholder);

        if (handRoot == null)
            return layoutBuffer;

        foreach (Transform child in handRoot)
        {
            if (child == placeholder)
            {
                layoutBuffer.Add(child);
                continue;
            }

            if (child.GetComponentInChildren<CardView>() != null)
                layoutBuffer.Add(child);
        }

        if (placeholder != null && !layoutBuffer.Contains(placeholder))
            layoutBuffer.Add(placeholder);

        return layoutBuffer;
    }

    private void UpdateCardsPosition(Transform placeholder)
    {
        SyncHandCardsFromChildren();

        if (handRoot == null)
            return;

        handCards.RemoveAll(h => h == null);

        for (int i = 0; i < handCards.Count; i++)
        {
            var card = handCards[i];
            if (card == null)
                continue;

            var container = card.HandContainer;
            var cardTransform = card.transform;
            bool isDetached = container != null && cardTransform != null &&
                              cardTransform.parent != handRoot && cardTransform.parent != container;

            if (container == null)
            {
                container = card.EnsureHandContainer(handRoot);
            }
            else
            {
                if (container.parent != handRoot)
                    container.SetParent(handRoot, true);

                if (!isDetached)
                    card.EnsureHandContainer(handRoot);
            }
        }

        placeholder = SanitizePlaceholder(placeholder);
        var layoutItems = BuildLayoutList(placeholder);
        int slotCount = layoutItems.Count;
        if (slotCount == 0)
            return;

        SortHandCardsBySlotIndex();

        var indexByTransform = new Dictionary<Transform, int>(slotCount);
        for (int i = 0; i < slotCount; i++)
            indexByTransform[layoutItems[i]] = i;

        float width = handRect != null ? handRect.rect.width : 600f;
        float spacing = width / Mathf.Max(1, maxHandSize);
        float startX = -spacing * (slotCount - 1) * 0.5f;

        foreach (var card in handCards)
        {
            if (card == null)
                continue;

            var containerTransform = card.HandContainer != null ? (Transform)card.HandContainer : card.transform;
            if (containerTransform == null || containerTransform.parent != handRoot)
                continue;

            bool isDetached = card.transform != null && card.HandContainer != null &&
                              card.transform.parent != card.HandContainer && card.transform.parent != handRoot;

            if (!indexByTransform.TryGetValue(containerTransform, out int slotIndex))
                continue;

            float normalized = slotCount <= 1 ? 0.5f : (float)slotIndex / (slotCount - 1);
            Vector3 finalLocalPos = new Vector3(startX + slotIndex * spacing, 0f, 0f);
            Quaternion finalRot = Quaternion.identity;

            card.EvaluateHandCurve(normalized, slotCount, out var posOffset, out var rotOffset);
            finalLocalPos += posOffset;
            finalRot = rotOffset;

            var container = card.HandContainer;
            if (container == null)
                container = card.EnsureHandContainer(handRoot);

            if (container == null)
                continue;

            if (isDetached)
            {
                Vector3 targetWorld = handRoot.TransformPoint(finalLocalPos);
                if ((container.position - targetWorld).sqrMagnitude > PositionThresholdSqr)
                    container.position = targetWorld;

                card.UpdateHandContainerTarget(finalLocalPos, finalRot);
                continue;
            }

            bool needsMove = (container.localPosition - finalLocalPos).sqrMagnitude > PositionThresholdSqr;
            bool needsRot = Quaternion.Angle(container.localRotation, finalRot) > RotationThreshold;
            if (!needsMove && !needsRot)
                continue;

            card.UpdateHandContainerTarget(finalLocalPos, finalRot);
        }
    }

    private CardView FindCardByContainer(Transform container)
    {
        if (container == null)
            return null;

        for (int i = 0; i < handCards.Count; i++)
        {
            var card = handCards[i];
            if (card != null && card.HandContainer == container)
                return card;
        }

        return null;
    }

    private void SortHandCardsBySlotIndex()
    {
        handCards.RemoveAll(c => c == null);
        handCards.Sort((a, b) =>
        {
            var ta = a != null ? a.HandContainer : null;
            var tb = b != null ? b.HandContainer : null;
            int ia = ta != null ? ta.GetSiblingIndex() : int.MaxValue;
            int ib = tb != null ? tb.GetSiblingIndex() : int.MaxValue;
            return ia.CompareTo(ib);
        });
    }

    // Gestisce lo swap basato sulla posizione orizzontale del drag (stile HorizontalCardHolder)
    public void ReorderHandDuringDrag(CardView movingCard, Vector3 dragPosition)
    {
        if (handRoot == null || movingCard == null)
            return;

        var reservedSlot = SanitizePlaceholder(movingCard.HandContainer);
        if (reservedSlot == null)
            return;

        activePlaceholder = reservedSlot;

        var layoutItems = BuildLayoutList(reservedSlot);
        int currentIndex = layoutItems.IndexOf(reservedSlot);
        if (currentIndex < 0)
            return;

        int targetIndex = currentIndex;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < layoutItems.Count; i++)
        {
            var item = layoutItems[i];
            float dist = Mathf.Abs(dragPosition.x - GetLayoutItemX(item));
            if (dist < bestDistance)
            {
                bestDistance = dist;
                targetIndex = i;
            }
        }

        if (targetIndex == currentIndex)
            return;

        var targetSlot = layoutItems[targetIndex];
        var targetCard = FindCardByContainer(targetSlot);

        if (targetCard != null && targetCard != movingCard)
            targetCard.SetHandContainer(reservedSlot as RectTransform, true);

        movingCard.SetHandContainer(targetSlot as RectTransform, false);
        activePlaceholder = movingCard.HandContainer;
        SortHandCardsBySlotIndex();
        UpdateCardsPosition(activePlaceholder);
    }

    private float GetLayoutItemX(Transform item)
    {
        if (item == null)
            return 0f;

        var view = item.GetComponentInChildren<CardView>();
        var container = view != null ? view.HandContainer : null;
        return container != null ? container.position.x : item.position.x;
    }

    public void OnHandCardBeginDrag(CardView view, Transform reservedSlot)
    {
        draggingCard = view;
        selectedCard = view;
        view?.EnsureHandContainer(handRoot);
        activePlaceholder = SanitizePlaceholder(reservedSlot);
        SortHandCardsBySlotIndex();
        UpdateCardsPosition(activePlaceholder);
    }

    public void OnHandCardEndDrag(CardView view = null)
    {
        // se un'altra carta sta ancora trascinando, non toccare
        if (view != null && draggingCard != null && view != draggingCard && activePlaceholder != null)
            return;

        if (view == selectedCard || view == null)
            selectedCard = null;

        draggingCard = null;
        activePlaceholder = null;
        SortHandCardsBySlotIndex();
        UpdateCardsPosition();
    }

    public void SetHoveredCard(CardView view)
    {
        hoveredCard = view;
    }

    public void ClearHoveredCard(CardView view)
    {
        if (hoveredCard == view)
            hoveredCard = null;
    }

    public void OnHandCardDroppedToBoard(CardView view)
    {
        if (view == null)
            return;

        if (draggingCard == view)
            draggingCard = null;
        if (selectedCard == view)
            selectedCard = null;
        if (hoveredCard == view)
            hoveredCard = null;

        activePlaceholder = null;
        SortHandCardsBySlotIndex();
        UpdateCardsPosition();
    }
}
