using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.UI;

public class HandManager : MonoBehaviour
{
    [Header("Hand settings")]
    [SerializeField] private int maxHandSize = 5;
    [SerializeField] private GameObject cardPrefab;

    [SerializeField] private Transform handManager;         // parent delle carte (RectTransform sotto Canvas)
    [SerializeField] private SplineContainer splineContainer;
    [SerializeField] private Transform spawnPoint;       // punto da cui far apparire le carte

    [Header("UI")]
    [SerializeField] private Button btnDraw;

    private readonly List<GameObject> handCards = new();

    private void Awake()
    {
        if (btnDraw != null)
            btnDraw.onClick.AddListener(DrawCard);
        else
            Debug.LogWarning("[HandManager] btnDraw non assegnato nell'Inspector.");
    }

    private void DrawCard()
    {
        if (handCards.Count >= maxHandSize)
            return;

        if (cardPrefab == null)
        {
            Debug.LogError("[HandManager] cardPrefab non assegnato!");
            return;
        }

        if (handManager == null)
        {
            Debug.LogError("[HandManager] handManager non assegnato!");
            return;
        }

        // === ISTANZIA COME FIGLIO DI handManager ===
        GameObject go = Instantiate(cardPrefab, handManager);
        go.name = cardPrefab.name;
        go.SetActive(true);

        // POSIZIONE INIZIALE = spawnPoint
        if (spawnPoint != null)
        {
            // Per UI: usare la posizione/rotazione in world space dello spawnPoint
            go.transform.position = spawnPoint.position;
            go.transform.rotation = spawnPoint.rotation;
        }

        handCards.Add(go);

        UpdateCardsPosition();
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

        // IMPORTANTE: splineContainer.transform dovrebbe essere == handManager
        // così tutte le posizioni sono nello stesso local space.
        if (splineContainer.transform != handManager)
        {
            Debug.LogWarning("[HandManager] Consigliato avere splineContainer sullo stesso GameObject di handManager.");
        }

        Spline spline = splineContainer.Spline;

        float cardSpacing = 1f / Mathf.Max(1, maxHandSize);
        float firstCardPosition = 0.5f - (handCards.Count - 1f) * cardSpacing / 2f;

        for (int i = 0; i < handCards.Count; i++)
        {
            float t = firstCardPosition + i * cardSpacing;
            t = Mathf.Clamp01(t);

            // POSIZIONE/ROT IN LOCAL SPACE DI handManager/splineContainer
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
