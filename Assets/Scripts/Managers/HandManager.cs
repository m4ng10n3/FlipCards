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


    [Header("UI")]
    [SerializeField] public Button btnDraw;

    private readonly List<CardView> handCards = new();
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
        if (handRoot == null)
            return;

        foreach (var cv in handRoot.GetComponentsInChildren<CardView>(false))
        {
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

    private void UpdateCardsPosition(Transform placeholder)
    {
        SyncHandCardsFromChildren();

        if (handRoot == null)
            return;

        placeholder = placeholder != null && placeholder.parent == handRoot ? placeholder : null;
        int slotCount = handRoot.childCount;
        if (slotCount == 0)
            return;

        handCards.RemoveAll(h => h == null);

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

            int slotIndex = cardTransform.GetSiblingIndex();

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
        if (handRoot == null || moving == null || placeholder == null) return;

        activePlaceholder = placeholder;

        int insertIndex = 0;
        for (int i = 0; i < handRoot.childCount; i++)
        {
            var child = handRoot.GetChild(i);
            if (child == placeholder) continue;
            if (moving.position.x > child.position.x) insertIndex++;
        }

        insertIndex = Mathf.Clamp(insertIndex, 0, Mathf.Max(0, handRoot.childCount - 1));
        if (placeholder.GetSiblingIndex() != insertIndex)
        {
            placeholder.SetSiblingIndex(insertIndex);
            UpdateCardsPosition(placeholder);
        }
    }

    public void OnHandCardBeginDrag(CardView view, Transform placeholder)
    {
        draggingCard = view;
        activePlaceholder = placeholder != null && placeholder.parent == handRoot ? placeholder : null;
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
