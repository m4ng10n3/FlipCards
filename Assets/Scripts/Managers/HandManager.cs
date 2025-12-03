using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
public class HandManager : MonoBehaviour
{
    [Header("Hand settings")]
    [SerializeField] private int maxHandSize = 5;
    [Header("Hierarchy")]
    [SerializeField] private Transform handRoot;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float spawnScaleMultiplier = 1.5f;
    [Header("UI")]
    [SerializeField] public Button btnDraw;
    [Header("Runtime debug")]
    [SerializeField] private CardView selectedCard;
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
    private CardView draggingCard;
    private Transform activePlaceholder;
    private void Awake()
    {
        btnDraw.onClick.AddListener(DrawCard);
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
        var remainingByName = new Dictionary<string, int>();
        foreach (var binding in gm.playerCards)
        {
            if (binding.count <= 0)
                continue;
            string name = binding.prefab.name;
            if (!remainingByName.ContainsKey(name))
                remainingByName[name] = 0;
            remainingByName[name] += binding.count;
        }
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
        foreach (var binding in gm.playerCards)
        {
            string name = binding.prefab.name;
            if (!remainingByName.TryGetValue(name, out int remaining) || remaining <= 0)
                continue;
            for (int i = 0; i < remaining; i++)
            {
                deck.Add(binding.prefab);
            }
        }
    }
    public void DrawCard()
    {
        var gm = GameManager.Instance;
        if (!deckInitialized)
        {
            RebuildDeckFromBindings();
            deckInitialized = true;
        }
        if (deck.Count == 0)
            return;
        if (handCards.Count >= maxHandSize)
            return;
        if (gm.player.actionPoints <= 0)
            return;
        gm.player.actionPoints -= 1;
        gm.UpdateHUD();
        int deckIndex = Random.Range(0, deck.Count);
        GameObject cardPrefabToSpawn = deck[deckIndex];
        deck.RemoveAt(deckIndex);
        GameObject go = Instantiate(cardPrefabToSpawn, handRoot);
        go.name = cardPrefabToSpawn.name;
        go.SetActive(true);
        go.transform.localScale = Vector3.one * spawnScaleMultiplier;
        var cv = go.GetComponent<CardView>();
        cv.gm = GameManager.Instance;
        cv.SetHighlight(false);
        var btn = go.GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(cv.OnClicked);
        go.transform.position = spawnPoint.position;
        go.transform.rotation = spawnPoint.rotation;
        RegisterHandCard(cv);
        UpdateCardsPosition();
    }
    private void RegisterHandCard(CardView cv)
    {
        if (!handCards.Contains(cv))
            handCards.Add(cv);
        cv.EnsureHandContainer(handRoot);
        var container = cv.HandContainer != null ? cv.HandContainer.transform : null;
        if (container != null && container.parent != handRoot)
        {
            container.SetParent(handRoot, true);
        }
        if (container != null)
            container.SetAsLastSibling();
        SortHandCardsBySlotIndex();
    }
    public void RemoveFromHand(GameObject cardGO)
    {
        RemoveFromHand(cardGO != null ? cardGO.GetComponent<CardView>() : null);
    }
    public void RemoveFromHand(CardView cv)
    {
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
        return placeholder != null && placeholder.parent == handRoot ? placeholder : null;
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
    public void ReorderHandDuringDrag(CardView movingCard, Vector3 dragPosition)
    {
        var container = movingCard.HandContainer;
        var layoutItems = BuildLayoutList(null);
        if (layoutItems.Count == 0) return;
        int currentIndex = layoutItems.IndexOf(container);
        if (currentIndex < 0) return;
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
        if (targetIndex == currentIndex) return;
        container.SetSiblingIndex(targetIndex);
        activePlaceholder = null;
        SortHandCardsBySlotIndex();
        UpdateCardsPosition();
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
        activePlaceholder = null;
        SortHandCardsBySlotIndex();
        UpdateCardsPosition();
    }
    public void OnHandCardEndDrag(CardView view = null)
    {
        if (view == selectedCard || view == null)
            selectedCard = null;
        draggingCard = null;
        activePlaceholder = null;
        SortHandCardsBySlotIndex();
        UpdateCardsPosition();
    }
    public void OnHandCardDroppedToBoard(CardView view)
    {
        if (draggingCard == view)
            draggingCard = null;
        if (selectedCard == view)
            selectedCard = null;
        activePlaceholder = null;
        SortHandCardsBySlotIndex();
        UpdateCardsPosition();
    }
}
