using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gestisce la selezione casuale degli slot nemici ogni turno con animazione slot-machine.
///
/// FUNZIONAMENTO:
///  1. GameManager chiama RollNewSlots(laneCount, onComplete).
///  2. Per ogni lane viene creato un overlay che mostra lo sprite sheet in scroll UV,
///     simulando un rullo da slot machine.
///  3. Il rullo rallenta e si ferma sul frame del prefab scelto.
///  4. Quando tutti i reel si fermano, viene chiamata onComplete con i prefab scelti.
///  5. GameManager usa quei prefab per re-spawnare gli slot nelle lane.
/// </summary>
public class SlotBatchManager : MonoBehaviour
{
    [Header("Batch (pool di slot possibili)")]
    public List<GameManager.PrefabSlotBinding> batch = new List<GameManager.PrefabSlotBinding>();

    [Header("Sprite Sheet Reel")]
    [Tooltip("Immagine 144×(144×N) con tutti gli sprite slot impilati dall'alto verso il basso.")]
    [SerializeField] private Texture2D reelSpriteSheet;
    [Tooltip("Numero di frame nello sprite sheet (righe). Es: 10 slot = 10 frame.")]
    [SerializeField] private int reelFrameCount = 10;
    [Tooltip("Velocità scroll in 'frame al secondo' durante la fase veloce.")]
    [SerializeField] private float reelScrollSpeed = 12f;
    [Tooltip("Durata in secondi del tween di rallentamento verso il frame finale.")]
    [SerializeField] private float reelSettleDuration = 0.65f;

    [Header("Animazione Reel")]
    [SerializeField] private float rollDuration     = 1.5f;
    [SerializeField] private float staggerDelay     = 0.18f;
    [SerializeField] private float settlePunchScale = 0.22f;

    [Header("Testo Settle (opzionale)")]
    [SerializeField] private Font  reelFont;
    [SerializeField] private int   reelFontSize = 18;
    [SerializeField] private Color settleColor  = new Color(1f, 0.92f, 0.3f);

    public IReadOnlyList<GameObject> LastChosenPrefabs => _chosenPrefabs;
    private readonly List<GameObject> _chosenPrefabs = new List<GameObject>();
    private bool _rolling;

    // ── API pubblica ──────────────────────────────────────────────────────────

    public void RollNewSlots(int laneCount, System.Action<List<GameObject>> onComplete)
    {
        if (_rolling) { Debug.LogWarning("[SlotBatchManager] Roll già in corso, ignorato."); return; }
        StartCoroutine(RollCoroutine(laneCount, onComplete));
    }

    // ── Coroutine principale ──────────────────────────────────────────────────

    IEnumerator RollCoroutine(int laneCount, System.Action<List<GameObject>> onComplete)
    {
        _rolling = true;

        var flat = BuildFlatBatch();
        if (flat.Count == 0)
        {
            Debug.LogWarning("[SlotBatchManager] Batch vuoto.");
            _rolling = false;
            onComplete?.Invoke(new List<GameObject>());
            yield break;
        }

        _chosenPrefabs.Clear();
        var rng = new System.Random();
        for (int i = 0; i < laneCount; i++)
            _chosenPrefabs.Add(flat[rng.Next(flat.Count)]);

        Logger.Info($"[Batch] Roll: {string.Join(" | ", _chosenPrefabs.ConvertAll(p => p.GetComponent<SlotDefinition>()?.SlotName ?? p.name))}");

        var gm = GameManager.Instance;
        if (gm == null) { _rolling = false; onComplete?.Invoke(_chosenPrefabs); yield break; }

        int pending = 0;
        for (int i = 0; i < laneCount && i < gm.aiBoardRoot.childCount; i++)
        {
            pending++;
            var laneRoot = gm.aiBoardRoot.GetChild(i);
            var chosen   = _chosenPrefabs[i];
            StartCoroutine(AnimateLaneReel(laneRoot, chosen, i * staggerDelay, () => pending--));
        }

        yield return new WaitUntil(() => pending <= 0);

        _rolling = false;
        onComplete?.Invoke(_chosenPrefabs);
    }

    // ── Animazione singola lane ───────────────────────────────────────────────

    IEnumerator AnimateLaneReel(Transform laneRoot, GameObject finalPrefab,
                                float delay, System.Action onDone = null)
    {
        yield return new WaitForSeconds(delay);
        if (laneRoot == null) { onDone?.Invoke(); yield break; }

        // Crea l'overlay (dark BG + RawImage sprite + Text settle)
        var (bgGO, rawImg, settleText) = GetOrCreateReelOverlay(laneRoot);
        if (bgGO == null) { onDone?.Invoke(); yield break; }
        bgGO.SetActive(true);

        // ── Fase scroll: UV scorre continuamente verso il basso ───────────────
        float frameH   = reelSpriteSheet != null ? 1f / reelFrameCount : 0f;
        float scrollY  = Random.Range(0f, 1f);
        float elapsed  = 0f;

        if (settleText != null) settleText.gameObject.SetActive(false);

        while (elapsed < rollDuration)
        {
            if (bgGO == null) { onDone?.Invoke(); yield break; }

            // Scrolla verso il basso (y decresce) → wrap in [0,1]
            scrollY -= reelScrollSpeed * frameH * Time.deltaTime;
            scrollY  = ((scrollY % 1f) + 1f) % 1f;

            if (rawImg != null)
                rawImg.uvRect = new Rect(0f, scrollY, 1f, frameH);

            elapsed += Time.deltaTime;
            yield return null; // frame-by-frame
        }

        if (bgGO == null) { onDone?.Invoke(); yield break; }

        // ── Settle: tween UV verso il frame corretto ──────────────────────────
        var finalSD    = finalPrefab.GetComponent<SlotDefinition>();
        int frameIdx   = finalSD != null ? finalSD.reelFrameIndex : 0;
        frameIdx       = Mathf.Clamp(frameIdx, 0, reelFrameCount - 1);

        // targetY: y del frame in UV (frame 0 = in cima, y vicino a 1.0)
        float targetY  = 1f - (frameIdx + 1) * frameH;

        // Calcola quante rotazioni extra fare prima di atterrare (min 1 loop)
        float distance = ((scrollY - targetY) + 1f) % 1f; // distanza percorsa verso targetY
        float totalTravel = distance + 1f;                 // +1 loop extra per decelerazione visibile
        float endY = scrollY - totalTravel;                // va negativo: il modulo lo riporta in [0,1]

        bool tweenDone = false;
        DOTween.To(
            () => scrollY,
            y =>
            {
                scrollY = y;
                if (rawImg != null)
                    rawImg.uvRect = new Rect(0f, ((y % 1f) + 1f) % 1f, 1f, frameH);
            },
            endY,
            reelSettleDuration
        ).SetEase(Ease.OutCubic)
         .SetLink(bgGO)
         .OnComplete(() => tweenDone = true);

        yield return new WaitUntil(() => tweenDone || bgGO == null);
        if (bgGO == null) { onDone?.Invoke(); yield break; }

        // Snap esatto al frame finale
        if (rawImg != null)
            rawImg.uvRect = new Rect(0f, targetY, 1f, frameH);

        // Punch scale sul BG
        bgGO.transform.DOPunchScale(Vector3.one * settlePunchScale, 0.3f, 6, 0.5f)
            .SetLink(bgGO);

        // ── Mostra testo con nome e stat ──────────────────────────────────────
        if (settleText != null && finalSD != null)
        {
            settleText.gameObject.SetActive(true);
            settleText.color = settleColor;
            settleText.text  = $"{finalSD.SlotName}\nATK {finalSD.atkDamage}  DEF {finalSD.blockFront}  HP {finalSD.maxHealth}";
        }

        yield return new WaitForSeconds(0.7f);
        if (bgGO != null) bgGO.SetActive(false);

        onDone?.Invoke();
    }

    // ── Creazione overlay ─────────────────────────────────────────────────────

    /// <summary>Crea (o recupera) l'overlay nella lane. Ritorna (bgGO, rawImg, settleText).</summary>
    (GameObject bg, RawImage raw, Text txt) GetOrCreateReelOverlay(Transform laneRoot)
    {
        // Recupera se già esiste
        var existing = laneRoot.Find("_BatchReelBG");
        if (existing != null)
        {
            var rawE  = existing.GetComponentInChildren<RawImage>(true);
            var txtE  = existing.GetComponentInChildren<Text>(true);
            return (existing.gameObject, rawE, txtE);
        }

        // ── BG: pannello opaco che copre la lane ──────────────────────────────
        var bgGO = new GameObject("_BatchReelBG", typeof(RectTransform));
        bgGO.transform.SetParent(laneRoot, false);
        var bgRt = bgGO.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color         = new Color(0.05f, 0.05f, 0.05f, 0.92f);
        bgImg.raycastTarget = false;

        // ── RawImage: sprite sheet scroll ─────────────────────────────────────
        RawImage rawImg = null;
        if (reelSpriteSheet != null)
        {
            var spriteGO = new GameObject("_BatchReelSprite", typeof(RectTransform));
            spriteGO.transform.SetParent(bgGO.transform, false);
            var srt = spriteGO.GetComponent<RectTransform>();
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;

            rawImg              = spriteGO.AddComponent<RawImage>();
            rawImg.texture      = reelSpriteSheet;
            rawImg.raycastTarget = false;
            float fh = 1f / reelFrameCount;
            rawImg.uvRect = new Rect(0f, 1f - fh, 1f, fh); // frame 0 iniziale
        }

        // ── Text: overlay in basso per il nome al settle ──────────────────────
        var textGO = new GameObject("_BatchReelText", typeof(RectTransform));
        textGO.transform.SetParent(bgGO.transform, false);
        var trt = textGO.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 0f);
        trt.anchorMax = new Vector2(1f, 0.45f);   // occupa la metà inferiore
        trt.offsetMin = new Vector2(4f, 2f);
        trt.offsetMax = new Vector2(-4f, -2f);

        var txt = textGO.AddComponent<Text>();
        txt.alignment            = TextAnchor.MiddleCenter;
        txt.fontSize             = reelFontSize;
        txt.color                = settleColor;
        txt.raycastTarget        = false;
        txt.resizeTextForBestFit = true;
        txt.fontStyle            = FontStyle.Bold;
        if (reelFont != null) txt.font = reelFont;
        textGO.SetActive(false); // nascosto fino al settle

        return (bgGO, rawImg, txt);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    List<GameObject> BuildFlatBatch()
    {
        var flat = new List<GameObject>();
        if (batch == null) return flat;
        foreach (var b in batch)
            if (b?.prefab != null)
                for (int k = 0; k < Mathf.Max(1, b.count); k++)
                    flat.Add(b.prefab);
        return flat;
    }
}
