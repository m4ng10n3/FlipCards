using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.UI;

public class HandManager : MonoBehaviour
{
    [Header("Hand settings")]
    [SerializeField] private int maxHandSize = 5;

    [SerializeField] private Transform handRoot;         // parent delle carte (RectTransform sotto Canvas)
    [SerializeField] private SplineContainer splineContainer;
    [SerializeField] private Transform spawnPoint;       // punto da cui far apparire le carte
    [SerializeField] private float spawnScaleMultiplier = 1.5f;


    [Header("UI")]
    [SerializeField] private Button btnDraw;

    private readonly List<GameObject> handCards = new();
    private readonly List<GameObject> deck = new();
    private bool deckInitialized = false;

    private void Awake()
    {
        if (btnDraw != null)
            btnDraw.onClick.AddListener(DrawCard);
        else
            Debug.LogWarning("[HandManager] btnDraw non assegnato nell'Inspector.");
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

        // 2) Sottraggo le copie che sono già in gioco sul board del player
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



    private void DrawCard()
    {
        var gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogError("[HandManager] GameManager.Instance non trovato!");
            return;
        }

        // Inizializzo il deck solo alla prima pesca,
        // quando le carte iniziali sono già state messe in campo
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

        // Pescare costa punti abilità
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

        handCards.Add(go);

        // Gestisce la posizione in campo (mano) lungo la spline
        UpdateCardsPosition();
    }

    public void RemoveFromHand(GameObject cardGO)
    {
        if (cardGO == null) return;

        if (handCards.Remove(cardGO))
        {
            Destroy(cardGO);
            UpdateCardsPosition();
        }
    }

    private void UpdateCardsPosition()
    {
        if (handCards.Count == 0)
            return;

        if (splineContainer == null || splineContainer.Spline == null)
        {
            Debug.LogWarning("[HandManager] splineContainer o Spline non assegnati.");
            return;
        }

        // IMPORTANTE: splineContainer.transform dovrebbe essere == handRoot
        // così tutte le posizioni sono nello stesso local space.
        if (splineContainer.transform != handRoot)
        {
            Debug.LogWarning("[HandManager] Consigliato avere splineContainer sullo stesso GameObject di handRoot.");
        }

        Spline spline = splineContainer.Spline;

        float cardSpacing = 1f / Mathf.Max(1, maxHandSize);
        float firstCardPosition = 0.5f - (handCards.Count - 1f) * cardSpacing / 2f;

        for (int i = 0; i < handCards.Count; i++)
        {
            float t = firstCardPosition + i * cardSpacing;
            t = Mathf.Clamp01(t);

            // POSIZIONE/ROT IN LOCAL SPACE DI handRoot/splineContainer
            Vector3 splineLocalPos = spline.EvaluatePosition(t);
            Vector3 forwardLocal = spline.EvaluateTangent(t);
            Vector3 upLocal = spline.EvaluateUpVector(t);

            Quaternion rotationLocal = Quaternion.LookRotation(
                upLocal,
                Vector3.Cross(upLocal, forwardLocal).normalized
            );

            Transform cardTransform = handCards[i].transform;

            // Usiamo DOLocalMove perché la spline è definita nello stesso spazio locale del parent
            cardTransform.DOLocalMove(splineLocalPos, 0.25f);
            cardTransform.DOLocalRotateQuaternion(rotationLocal, 0.25f);
        }
    }
}
