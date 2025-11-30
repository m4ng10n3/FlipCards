using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.UI;

public class HandManager : MonoBehaviour
{
    [Header("Hand settings")]
    [SerializeField] private int maxHandSize = 5;

    [Header("Hierarchy")]
    [SerializeField] private Transform handRoot;         // parent delle carte (RectTransform sotto Canvas)
    [SerializeField] private Transform spawnPoint;       // punto da cui far apparire le carte
    [SerializeField] private float spawnScaleMultiplier = 1.5f;

    [Header("Curve layout")]
    [SerializeField] private CurveParameters curveParameters;
    [SerializeField] private float curveYOffsetMultiplier = 1f;
    [SerializeField] private float curveRotationInfluence = 1f;
    [SerializeField] private float handTweenDuration = 0.2f;
    [SerializeField] private Ease handTweenEase = Ease.OutQuad;

    [SerializeField] private Card selectedCard;
    [SerializeReference] private Card hoveredCard;

    [Header("UI")]
    [SerializeField] public Button btnDraw;

    private readonly List<CardView> handCards = new();
    private readonly List<Transform> layoutBuffer = new();
    private readonly List<GameObject> deck = new();
    private bool deckInitialized = false;
    private RectTransform handRect;

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
            var cv = child.GetComponent<CardView>();
            if (cv != null && !handCards.Contains(cv))
                handCards.Add(cv);
        }

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
            cv.transform.SetParent(handRoot, true);
            cv.transform.SetAsLastSibling();
        }
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

            if (child.GetComponent<CardView>() != null)
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

        placeholder = SanitizePlaceholder(placeholder);
        var layoutItems = BuildLayoutList(placeholder);
        int slotCount = layoutItems.Count;
        if (slotCount == 0)
            return;

        handCards.RemoveAll(h => h == null);

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

            var cardTransform = card.transform;
            if (cardTransform == null || cardTransform.parent != handRoot)
                continue;

            if (!indexByTransform.TryGetValue(cardTransform, out int slotIndex))
                continue;

            float normalized = slotCount <= 1 ? 0.5f : (float)slotIndex / (slotCount - 1);
            Vector3 finalLocalPos = new Vector3(startX + slotIndex * spacing, 0f, 0f);
            Quaternion finalRot = Quaternion.identity;

            if (curveParameters != null)
            {
                float yOff = curveParameters.positioning.Evaluate(normalized) * curveParameters.positioningInfluence * slotCount * curveYOffsetMultiplier;
                finalLocalPos += Vector3.up * yOff;

                float rotZ = curveParameters.rotation.Evaluate(normalized) * curveParameters.rotationInfluence * curveRotationInfluence;
                finalRot = Quaternion.Euler(0f, 0f, rotZ);
            }

            cardTransform.DOLocalMove(finalLocalPos, handTweenDuration).SetEase(handTweenEase);
            cardTransform.DOLocalRotateQuaternion(finalRot, handTweenDuration).SetEase(handTweenEase);
        }
    }

    // Gestisce lo swap basato sulla posizione orizzontale del drag (stile HorizontalCardHolder)
    public void ReorderHandDuringDrag(Transform moving, Transform placeholder)
    {
        placeholder = SanitizePlaceholder(placeholder);
        if (handRoot == null || moving == null || placeholder == null) return;

        activePlaceholder = placeholder;

        var layoutItems = BuildLayoutList(placeholder);

        int insertIndex = 0;
        for (int i = 0; i < layoutItems.Count; i++)
        {
            var child = layoutItems[i];
            if (child == placeholder) continue;
            if (moving.position.x > child.position.x) insertIndex++;
        }

        int cardCount = Mathf.Max(0, layoutItems.Count - 1); // escludo il placeholder
        insertIndex = Mathf.Clamp(insertIndex, 0, cardCount);

        RepositionPlaceholder(placeholder, insertIndex);
    }

    private void RepositionPlaceholder(Transform placeholder, int insertIndex)
    {
        if (handRoot == null || placeholder == null) return;

        var layoutItems = BuildLayoutList(placeholder);
        layoutItems.Remove(placeholder);

        insertIndex = Mathf.Clamp(insertIndex, 0, layoutItems.Count);

        int startIndex = layoutItems.Count > 0 ? layoutItems[0].GetSiblingIndex() : placeholder.GetSiblingIndex();
        layoutItems.Insert(insertIndex, placeholder);

        for (int i = 0; i < layoutItems.Count; i++)
        {
            int targetIndex = startIndex + i;
            if (layoutItems[i].GetSiblingIndex() != targetIndex)
                layoutItems[i].SetSiblingIndex(targetIndex);
        }

        UpdateCardsPosition(placeholder);
    }

    public void OnHandCardBeginDrag(CardView view, Transform placeholder)
    {
        draggingCard = view;
        activePlaceholder = SanitizePlaceholder(placeholder);
        UpdateCardsPosition(activePlaceholder);
    }

    public void OnHandCardEndDrag(CardView view = null)
    {
        // se un'altra carta sta ancora trascinando, non toccare
        if (view != null && draggingCard != null && view != draggingCard && activePlaceholder != null)
            return;

        draggingCard = null;
        activePlaceholder = null;
        UpdateCardsPosition();
    }
}
