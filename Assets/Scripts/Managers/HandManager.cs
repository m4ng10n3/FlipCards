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
    private readonly List<GameObject> deck = new();
    private bool deckInitialized;
    private RectTransform handRect;
    private const float PositionThresholdSqr = 0.0001f;
    private const float RotationThreshold = 0.1f;

    public Transform HandRoot => handRoot;
    private CardView draggingCard;

    private void Awake()
    {
        if (btnDraw == null || handRoot == null || spawnPoint == null)
            throw new System.InvalidOperationException("HandManager references not assigned");

        btnDraw.onClick.AddListener(DrawCard);
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
    }

    public void DrawCard()
    {
        var gm = GameManager.Instance ?? throw new System.InvalidOperationException("GameManager missing");

        if (!deckInitialized)
        {
            RebuildDeckFromBindings();
            deckInitialized = true;
        }

        if (deck.Count == 0 || handCards.Count >= maxHandSize)
            return;

        if (!gm.TrySpendPlayerAP(gm.drawCardCost, "Draw"))
            return;

        int deckIndex = Random.Range(0, deck.Count);
        GameObject cardPrefabToSpawn = deck[deckIndex];
        deck.RemoveAt(deckIndex);

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

    public void UpdateCardsPosition()
    {
        SyncHandCardsFromChildren();
        if (handCards.Count == 0 || handRoot == null) return;

        SortHandCardsBySlotIndex();

        float width = handRect != null ? handRect.rect.width : 600f;
        float spacing = width / Mathf.Max(1, maxHandSize);
        float startX = -spacing * (handCards.Count - 1) * 0.5f;

        int slotCount = handCards.Count;
        for (int i = 0; i < slotCount; i++)
        {
            var card = handCards[i];
            var container = card.handContainer ?? card.EnsureHandContainer(handRoot);
            if (container == null) throw new System.InvalidOperationException("Hand container missing");
            if (container.parent != handRoot) container.SetParent(handRoot, true);

            int slotIndex = container.GetSiblingIndex();
            Vector3 finalLocalPos = new Vector3(startX + slotIndex * spacing, 0f, 0f);

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
