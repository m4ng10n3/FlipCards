using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Selezione casuale degli slot nemici a inizio turno, con animazione slot-machine.
///
/// FLUSSO
///  1. GameManager chiama RollNewSlots(laneCount, onPrefabsChosen, onComplete).
///  2. Vengono create celle di copertura opache su un layer dedicato, FUORI da
///     aiBoardRoot: non partecipano a nessun LayoutGroup e sopravvivono al respawn.
///  3. Sotto la copertura GameManager ri-spawna gli slot veri (onPrefabsChosen).
///  4. Il rullo scorre e si ferma sul frame dello slot scelto.
///  5. La cella sfuma e rivela lo slot gia in posizione: nessun pop, nessun resize.
///
/// PERCHE IL LAYER E SEPARATO (fix del resize)
///  Prima l'overlay era figlio dello slot e veniva stirato su tutto il rect dello
///  slot (100x150) mentre l'artwork vero vive in un rect quadrato (100x100): al
///  reveal l'immagine "scattava" da 100x150 a 100x100. In piu un DOPunchScale su
///  anchor stretched faceva debordare la copertura oltre i bordi della lane.
///  Ora: rect dell'immagine copiato dal figlio artwork dello slot, AspectRatioFitter
///  che blocca il rapporto 1:1 del frame, RectMask2D che ritaglia tutto dentro la
///  lane. La cella non puo piu cambiare dimensione ne deformare l'immagine.
/// </summary>
public class SlotBatchManager : MonoBehaviour
{
    const string LayerName = "_ReelOverlayLayer";
    const string CellName  = "_ReelCell";

    [Header("Batch (pool di slot possibili)")]
    public List<GameManager.PrefabSlotBinding> batch = new List<GameManager.PrefabSlotBinding>();

    [Header("Reel")]
    [Tooltip("Velocita scroll in 'frame al secondo' durante la fase veloce.")]
    [SerializeField] private float reelScrollSpeed = 12f;
    [Tooltip("Durata in secondi del tween di rallentamento verso il frame finale.")]
    [SerializeField] private float reelSettleDuration = 0.65f;

    [Header("Animazione Reel")]
    [SerializeField] private float rollDuration = 1.5f;
    [SerializeField] private float staggerDelay = 0.18f;
    [Tooltip("Pausa sul risultato prima di rivelare lo slot vero.")]
    [SerializeField] private float holdAfterSettle = 0.5f;
    [Tooltip("Dissolvenza della copertura che rivela lo slot.")]
    [SerializeField] private float revealFade = 0.22f;
    [Tooltip("Punch di scala sull'immagine al settle. 0 = disattivato. Resta comunque ritagliato dentro la lane (RectMask2D).")]
    [Range(0f, 0.25f)][SerializeField] private float settlePunch = 0f;

    [Header("Area immagine dentro la lane")]
    [Tooltip("Nome del figlio del prefab slot che contiene l'artwork: il reel ne copia esattamente il rect, cosi al reveal l'immagine non cambia dimensione.")]
    [SerializeField] private string artChildName = "Sprite";
    [Tooltip("Fallback usato solo se il figlio sopra non esiste. Rect normalizzato (0-1) dentro la lane.")]
    [SerializeField] private Vector2 artAnchorMin = new Vector2(0f, 0.24f);
    [SerializeField] private Vector2 artAnchorMax = new Vector2(1f, 0.90f);

    [Header("Aspetto copertura")]
    [Tooltip("Alpha 1: lo scambio degli slot avviene dietro una copertura opaca e non traspare mai.")]
    [SerializeField] private Color coverColor = new Color(0.05f, 0.05f, 0.05f, 1f);

    [Header("Testo Settle (opzionale)")]
    [SerializeField] private Font  reelFont;
    [SerializeField] private int   reelFontSize = 18;
    [SerializeField] private Color settleColor  = new Color(1f, 0.92f, 0.3f);

    public IReadOnlyList<GameObject> LastChosenPrefabs => _chosenPrefabs;
    public bool IsRolling => _rolling;

    private readonly List<GameObject> _chosenPrefabs = new List<GameObject>();
    private readonly List<Cell> _cells = new List<Cell>();
    private bool _rolling;

    /// <summary>
    /// Facce del rullo: sono i prefab candidati di questo roll, in ordine e senza
    /// ripetizioni. Il rullo mostra le LORO immagini, non un foglio a parte:
    /// e' cio' che garantisce che l'immagine su cui si ferma sia esattamente
    /// quella dello slot che viene rivelato.
    /// </summary>
    private readonly List<GameObject> _reelFaces = new List<GameObject>();

    /// <summary>Ripetizioni della striscia: servono per far scorrere senza stacchi.</summary>
    const int StripLoops = 3;

    struct LaneStep { public Vector2 first; public Vector2 size; public float stride; }
    private LaneStep? _laneStep;

    // ── Cella di copertura ────────────────────────────────────────────────────

    class Cell
    {
        public RectTransform root;
        public CanvasGroup   group;
        public RectTransform artArea;   // stesso rect dell'artwork dello slot
        public RectTransform strip;     // colonna di facce che scorre
        public readonly List<RectTransform> frames = new List<RectTransform>();
        public RectTransform labelArea;
        public Text          label;
    }

    // ── API pubblica ──────────────────────────────────────────────────────────

    /// <summary>Compatibilita con la vecchia firma: il respawn avviene DOPO il reel.</summary>
    public void RollNewSlots(int laneCount, System.Action<List<GameObject>> onComplete)
        => RollNewSlots(laneCount, null, onComplete);

    /// <summary>
    /// onPrefabsChosen viene invocata mentre le lane sono coperte: e' li' che
    /// GameManager deve ri-spawnare gli slot, cosi il reel rivela quelli veri.
    /// </summary>
    public void RollNewSlots(int laneCount,
                             System.Action<List<GameObject>> onPrefabsChosen,
                             System.Action<List<GameObject>> onComplete)
    {
        if (_rolling)
        {
            // Chi chiama ha gia' disabilitato i bottoni e conta su onComplete per
            // riabilitarli: uscire in silenzio bloccherebbe l'input per sempre.
            // NON invocare onComplete: il roll gia' in volo lo fara' dal suo finally.
            // Farlo qui sbloccherebbe l'input con il board ancora coperto e
            // provocherebbe un secondo StartTurn (AP e TurnStart doppi).
            Debug.LogError("[SlotBatchManager] Roll gia in corso: chiamata ignorata.");
            return;
        }
        StartCoroutine(RollCoroutine(laneCount, onPrefabsChosen, onComplete));
    }

    // ── Coroutine principale ──────────────────────────────────────────────────

    IEnumerator RollCoroutine(int laneCount,
                              System.Action<List<GameObject>> onPrefabsChosen,
                              System.Action<List<GameObject>> onComplete)
    {
        _rolling = true;

        // try/finally: qualunque cosa vada storta a meta' roll (il respawn degli
        // slot gira dentro questa coroutine) le coperture opache vengono comunque
        // rimosse, _rolling torna false e onComplete riabilita l'input.
        // Senza questo un'eccezione lascerebbe il gioco bloccato dietro un pannello nero.
        try
        {
            var flat  = BuildFlatBatch();
            var gm    = GameManager.Instance;
            var board = gm != null ? gm.aiBoardRoot as RectTransform : null;

            _chosenPrefabs.Clear();

            if (flat.Count == 0 || board == null)
            {
                if (flat.Count == 0) Debug.LogWarning("[SlotBatchManager] Batch vuoto.");
                else                 Debug.LogWarning("[SlotBatchManager] aiBoardRoot non e' un RectTransform.");
                onPrefabsChosen?.Invoke(_chosenPrefabs);
                yield break;   // il finally invoca comunque onComplete
            }

            // Le facce del rullo sono i candidati distinti di questo roll.
            _reelFaces.Clear();
            foreach (var candidate in flat)
                if (!_reelFaces.Contains(candidate)) _reelFaces.Add(candidate);

            var rng = new System.Random();
            for (int i = 0; i < laneCount; i++)
                _chosenPrefabs.Add(flat[rng.Next(flat.Count)]);

            Logger.Info($"[Batch] Roll: {string.Join(" | ", _chosenPrefabs.ConvertAll(p => p.GetComponent<SlotDefinition>()?.SlotName ?? p.name))}");

            // 1. Copertura sulle lane attuali, prima che qualcosa cambi ─────────
            Canvas.ForceUpdateCanvases();
            var layer = EnsureOverlayLayer(board);
            if (layer == null)
            {
                Debug.LogWarning("[SlotBatchManager] Nessun parent valido per l'overlay: roll saltato.");
                onPrefabsChosen?.Invoke(_chosenPrefabs);
                yield break;
            }
            BuildCells(layer, board, laneCount);

            // 2. Swap nascosto: gli slot veri nascono sotto la copertura ────────
            onPrefabsChosen?.Invoke(_chosenPrefabs);

            yield return null;                 // lascia girare un layout pass
            Canvas.ForceUpdateCanvases();
            layer.SetAsLastSibling();          // resta sopra ai figli appena istanziati
            CopyWorldRect(layer, board);
            ComputeLaneStep(board, layer);     // le lane sono altre: ricalcola il riferimento
            for (int i = 0; i < _cells.Count; i++)
                ApplyCellGeometry(_cells[i], LaneRect(board, i), layer, i, laneCount);

            // 3. Reel su ogni lane ──────────────────────────────────────────────
            int pending = 0;
            for (int i = 0; i < _cells.Count; i++)
            {
                pending++;
                StartCoroutine(AnimateCell(_cells[i], _chosenPrefabs[i], i * staggerDelay, () => pending--));
            }

            yield return new WaitUntil(() => pending <= 0);
        }
        finally
        {
            DestroyCells();
            _rolling = false;
            onComplete?.Invoke(_chosenPrefabs);
        }
    }

    // ── Animazione singola cella ──────────────────────────────────────────────

    IEnumerator AnimateCell(Cell cell, GameObject finalPrefab, float delay, System.Action onDone)
    {
        yield return new WaitForSeconds(delay);
        if (cell == null || cell.root == null) { onDone?.Invoke(); yield break; }

        if (cell.label != null) cell.label.gameObject.SetActive(false);

        int faces  = _reelFaces.Count;
        float step = cell.artArea != null ? cell.artArea.rect.height : 0f;

        if (faces == 0 || step <= 0f || cell.strip == null)
        {
            // Nessuna striscia da far girare: si passa direttamente al reveal.
            yield return Reveal(cell, finalPrefab, onDone);
            yield break;
        }

        float loop    = faces * step;
        float y       = loop + Random.Range(0, faces) * step;   // parte dalla ripetizione centrale
        float elapsed = 0f;

        // ── Fase scroll: la colonna di facce scorre verso l'alto ──────────────
        while (elapsed < rollDuration)
        {
            if (cell.root == null) { onDone?.Invoke(); yield break; }

            y += reelScrollSpeed * step * Time.deltaTime;
            while (y >= 2f * loop) y -= loop;                   // rientro invisibile

            cell.strip.anchoredPosition = new Vector2(0f, y);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (cell.root == null) { onDone?.Invoke(); yield break; }

        // ── Settle: si ferma sulla faccia del prefab scelto ───────────────────
        int index = _reelFaces.IndexOf(finalPrefab);
        if (index < 0) index = 0;

        float target = loop + index * step;
        while (target < y + loop * 0.5f) target += loop;        // almeno mezzo giro di decelerazione

        bool tweenDone = false;
        DOTween.To(
            () => y,
            v => { y = v; if (cell.strip != null) cell.strip.anchoredPosition = new Vector2(0f, v); },
            target,
            reelSettleDuration
        ).SetEase(Ease.OutCubic)
         .SetLink(cell.root.gameObject)
         .OnComplete(() => tweenDone = true);

        yield return new WaitUntil(() => tweenDone || cell.root == null);
        if (cell.root == null) { onDone?.Invoke(); yield break; }

        cell.strip.anchoredPosition = new Vector2(0f, target);

        if (settlePunch > 0f)
            cell.strip.DOPunchScale(Vector3.one * settlePunch, 0.28f, 6, 0.6f)
                      .SetLink(cell.root.gameObject);

        yield return Reveal(cell, finalPrefab, onDone);
    }

    /// <summary>Etichetta dello slot uscito, pausa, poi la copertura sfuma.</summary>
    IEnumerator Reveal(Cell cell, GameObject finalPrefab, System.Action onDone)
    {
        var finalSD = finalPrefab != null ? finalPrefab.GetComponent<SlotDefinition>() : null;

        if (cell.label != null && finalSD != null)
        {
            cell.label.gameObject.SetActive(true);
            cell.label.color = settleColor;
            cell.label.text  = $"{finalSD.SlotName}\nATK {finalSD.atkDamage}  DEF {finalSD.blockFront}  HP {finalSD.maxHealth}";
        }

        yield return new WaitForSeconds(holdAfterSettle);
        if (cell.root == null) { onDone?.Invoke(); yield break; }

        // ── Reveal: la copertura sfuma sullo slot vero, gia in posizione ──────
        if (cell.group != null && revealFade > 0f)
        {
            bool fadeDone = false;
            cell.group.DOFade(0f, revealFade)
                      .SetEase(Ease.InQuad)
                      .SetLink(cell.root.gameObject)
                      .OnComplete(() => fadeDone = true);
            yield return new WaitUntil(() => fadeDone || cell.root == null);
        }

        if (cell.root != null) cell.root.gameObject.SetActive(false);
        onDone?.Invoke();
    }

    // ── Layer + celle ─────────────────────────────────────────────────────────

    /// <summary>
    /// Il layer vive come fratello di aiBoardRoot: cosi aiBoardRoot continua ad
    /// avere SOLO gli slot come figli (childCount e sibling index restano validi)
    /// e l'HorizontalLayoutGroup non vede mai la copertura.
    /// </summary>
    RectTransform EnsureOverlayLayer(RectTransform board)
    {
        // MAI dentro aiBoardRoot: sporcherebbe childCount/GetChild(lane), su cui si
        // basano GetEnemySlotViewAtLane, GetLaneIndexFor e le abilita' di lane.
        var parent = board.parent as RectTransform;
        if (parent == null)
        {
            var canvas = board.GetComponentInParent<Canvas>();
            parent = canvas != null ? canvas.transform as RectTransform : null;
        }
        // GetComponentInParent include se stesso: senza questo controllo il layer
        // potrebbe finire dentro aiBoardRoot e sfasare GetChild(lane).
        if (parent == null || parent == board || parent.IsChildOf(board)) return null;

        var layer = parent.Find(LayerName) as RectTransform;
        if (layer == null)
        {
            var go = new GameObject(LayerName, typeof(RectTransform));
            layer  = go.GetComponent<RectTransform>();
            layer.SetParent(parent, false);

            var le = go.AddComponent<LayoutElement>();
            le.ignoreLayout = true;   // invisibile a qualunque LayoutGroup
        }

        layer.gameObject.SetActive(true);
        CopyWorldRect(layer, board);
        layer.SetAsLastSibling();
        return layer;
    }

    void BuildCells(RectTransform layer, RectTransform board, int laneCount)
    {
        DestroyCells();
        ComputeLaneStep(board, layer);

        for (int i = 0; i < laneCount; i++)
        {
            var cell = CreateCell(layer, i);
            ApplyCellGeometry(cell, LaneRect(board, i), layer, i, laneCount);
            _cells.Add(cell);
        }
    }

    Cell CreateCell(RectTransform layer, int index)
    {
        var cell = new Cell();

        // Root: copertura opaca + mask. Il mask garantisce che NIENTE (nemmeno un
        // punch di scala) possa debordare oltre i bordi della lane.
        var rootGO = new GameObject($"{CellName}_{index}", typeof(RectTransform));
        cell.root  = rootGO.GetComponent<RectTransform>();
        cell.root.SetParent(layer, false);

        var bg = rootGO.AddComponent<Image>();
        bg.color         = coverColor;
        bg.raycastTarget = true;   // blocca i click sulla lane mentre gira il rullo

        rootGO.AddComponent<RectMask2D>();
        cell.group = rootGO.AddComponent<CanvasGroup>();

        // Area artwork: stesso rect del figlio "Sprite" dello slot.
        var areaGO  = new GameObject("ArtArea", typeof(RectTransform));
        cell.artArea = areaGO.GetComponent<RectTransform>();
        cell.artArea.SetParent(cell.root, false);

        // Striscia: una faccia per candidato, ripetuta StripLoops volte per poter
        // scorrere senza stacchi. Le facce sono copie dell'artwork dei prefab, non
        // fotogrammi di un foglio a parte: cosi cio' su cui il rullo si ferma e'
        // esattamente cio' che viene rivelato.
        var stripGO = new GameObject("Strip", typeof(RectTransform));
        cell.strip  = stripGO.GetComponent<RectTransform>();
        cell.strip.SetParent(cell.artArea, false);
        cell.strip.anchorMin = new Vector2(0f, 1f);
        cell.strip.anchorMax = new Vector2(1f, 1f);
        cell.strip.pivot     = new Vector2(0.5f, 1f);

        int faces = _reelFaces.Count;
        for (int loop = 0; loop < StripLoops; loop++)
        {
            for (int face = 0; face < faces; face++)
            {
                var frameGO = new GameObject($"Face_{loop}_{face}", typeof(RectTransform));
                var frameRt = frameGO.GetComponent<RectTransform>();
                frameRt.SetParent(cell.strip, false);
                frameRt.anchorMin = new Vector2(0f, 1f);
                frameRt.anchorMax = new Vector2(1f, 1f);
                frameRt.pivot     = new Vector2(0.5f, 1f);

                var img = frameGO.AddComponent<Image>();
                img.raycastTarget = false;
                CopyArtwork(_reelFaces[face], img);

                cell.frames.Add(frameRt);
            }
        }

        // Etichetta: sotto l'artwork, non sopra.
        var labelGO   = new GameObject("Label", typeof(RectTransform));
        cell.labelArea = labelGO.GetComponent<RectTransform>();
        cell.labelArea.SetParent(cell.root, false);

        cell.label = labelGO.AddComponent<Text>();
        cell.label.alignment            = TextAnchor.MiddleCenter;
        cell.label.fontSize             = reelFontSize;
        cell.label.color                = settleColor;
        cell.label.raycastTarget        = false;
        cell.label.resizeTextForBestFit = true;
        cell.label.fontStyle            = FontStyle.Bold;
        cell.label.font                 = reelFont != null ? reelFont : FallbackFont();
        labelGO.SetActive(false);

        return cell;
    }

    /// <summary>Allinea la cella alla lane e l'immagine al rect dell'artwork dello slot.</summary>
    void ApplyCellGeometry(Cell cell, RectTransform lane, RectTransform layer, int index, int laneCount)
    {
        if (cell == null || cell.root == null) return;

        if (lane != null)
        {
            cell.root.gameObject.SetActive(true);   // puo' essere stata spenta al giro precedente
            CopyWorldRect(cell.root, lane);
        }
        else if (_laneStep.HasValue)
        {
            cell.root.gameObject.SetActive(true);
            // Fallback: lane mancante (es. uno slot e' morto a fine turno).
            // Il passo NON si ricava da layer.rect: aiBoardRoot e' 100x150 ma il
            // suo HorizontalLayoutGroup lascia debordare i figli, quindi il rect
            // del board e' largo quanto UNA lane. Si estrapola dalla lane 0.
            var step = _laneStep.Value;
            cell.root.anchorMin = cell.root.anchorMax = new Vector2(0.5f, 0.5f);
            cell.root.pivot      = new Vector2(0.5f, 0.5f);
            cell.root.localScale = Vector3.one;
            cell.root.sizeDelta  = step.size;
            cell.root.anchoredPosition = step.first + new Vector2(step.stride * index, 0f);
        }
        else
        {
            cell.root.gameObject.SetActive(false);   // niente riferimento: meglio nulla che una scheggia
            return;
        }

        GetArtRect(lane, out Vector2 nMin, out Vector2 nMax);

        cell.artArea.anchorMin = nMin;
        cell.artArea.anchorMax = nMax;
        cell.artArea.offsetMin = Vector2.zero;
        cell.artArea.offsetMax = Vector2.zero;

        // La striscia e' alta quanto tutte le facce messe in colonna; ogni faccia
        // occupa esattamente l'area artwork, cosi il fermo e' sempre allineato.
        float faceH = cell.artArea.rect.height;
        if (cell.strip != null && faceH > 0f)
        {
            cell.strip.localScale = Vector3.one;
            cell.strip.offsetMin = new Vector2(0f, cell.strip.offsetMin.y);
            cell.strip.offsetMax = new Vector2(0f, cell.strip.offsetMax.y);
            cell.strip.sizeDelta = new Vector2(0f, faceH * cell.frames.Count);
            cell.strip.anchoredPosition = Vector2.zero;

            for (int f = 0; f < cell.frames.Count; f++)
            {
                var frame = cell.frames[f];
                frame.offsetMin = new Vector2(0f, frame.offsetMin.y);
                frame.offsetMax = new Vector2(0f, frame.offsetMax.y);
                frame.sizeDelta = new Vector2(0f, faceH);
                frame.anchoredPosition = new Vector2(0f, -f * faceH);
            }
        }

        cell.labelArea.anchorMin = new Vector2(0f, 0f);
        cell.labelArea.anchorMax = new Vector2(1f, Mathf.Max(0.01f, nMin.y));
        cell.labelArea.offsetMin = new Vector2(2f, 2f);
        cell.labelArea.offsetMax = new Vector2(-2f, -1f);
    }

    /// <summary>
    /// Rect normalizzato dell'artwork dentro la lane. Letto dallo slot vero quando
    /// possibile: e' cio' che garantisce che l'immagine del reel e quella dello slot
    /// abbiano ESATTAMENTE la stessa dimensione e posizione.
    /// </summary>
    void GetArtRect(RectTransform lane, out Vector2 nMin, out Vector2 nMax)
    {
        nMin = artAnchorMin;
        nMax = artAnchorMax;
        if (lane == null || string.IsNullOrEmpty(artChildName)) return;

        var art = lane.Find(artChildName) as RectTransform;
        if (art == null) return;

        Rect lr = lane.rect;
        if (lr.width <= 0f || lr.height <= 0f) return;

        var c = new Vector3[4];
        art.GetWorldCorners(c);
        Vector3 bl = lane.InverseTransformPoint(c[0]);
        Vector3 tr = lane.InverseTransformPoint(c[2]);

        nMin = new Vector2((bl.x - lr.xMin) / lr.width, (bl.y - lr.yMin) / lr.height);
        nMax = new Vector2((tr.x - lr.xMin) / lr.width, (tr.y - lr.yMin) / lr.height);

        nMin = new Vector2(Mathf.Clamp01(nMin.x), Mathf.Clamp01(nMin.y));
        nMax = new Vector2(Mathf.Clamp01(nMax.x), Mathf.Clamp01(nMax.y));

        if (nMax.x - nMin.x < 0.05f || nMax.y - nMin.y < 0.05f)
        {
            nMin = artAnchorMin;
            nMax = artAnchorMax;
        }
    }

    void DestroyCells()
    {
        for (int i = 0; i < _cells.Count; i++)
            if (_cells[i]?.root != null) Destroy(_cells[i].root.gameObject);
        _cells.Clear();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static RectTransform LaneRect(RectTransform board, int index)
    {
        if (board == null || index < 0 || index >= board.childCount) return null;
        return board.GetChild(index) as RectTransform;
    }

    /// <summary>
    /// Copia il rect di 'source' su 'target' passando per lo spazio mondo: funziona
    /// anche quando i due hanno parent o scale diversi (aiBoardRoot ha scale 1.8).
    /// </summary>
    static void CopyWorldRect(RectTransform target, RectTransform source)
    {
        if (target == null) return;
        if (!MeasureInParent(source, target.parent as RectTransform, out var size, out var center)) return;

        target.localScale    = Vector3.one;
        target.localRotation = Quaternion.identity;
        target.anchorMin     = new Vector2(0.5f, 0.5f);
        target.anchorMax     = new Vector2(0.5f, 0.5f);
        target.pivot         = new Vector2(0.5f, 0.5f);
        target.sizeDelta     = size;
        target.anchoredPosition = center;
    }

    /// <summary>
    /// Misura 'source' nello spazio di 'parent' passando per i world corners:
    /// assorbe scale e pivot diversi (aiBoardRoot ha scale 1.8 e pivot 0,1).
    /// Ritorna la dimensione e l'anchoredPosition equivalente per anchor/pivot (0.5, 0.5).
    /// </summary>
    static bool MeasureInParent(RectTransform source, RectTransform parent,
                                out Vector2 size, out Vector2 anchoredCenter)
    {
        size = Vector2.zero;
        anchoredCenter = Vector2.zero;
        if (source == null || parent == null) return false;

        var c = new Vector3[4];
        source.GetWorldCorners(c);                    // 0=BL 1=TL 2=TR 3=BR

        Vector3 bl = parent.InverseTransformPoint(c[0]);
        Vector3 tr = parent.InverseTransformPoint(c[2]);

        size = new Vector2(Mathf.Abs(tr.x - bl.x), Mathf.Abs(tr.y - bl.y));
        anchoredCenter = new Vector2((bl.x + tr.x) * 0.5f, (bl.y + tr.y) * 0.5f) - parent.rect.center;
        return true;
    }

    /// <summary>
    /// Geometria di riferimento presa dalle lane REALI: serve solo come fallback
    /// quando una lane manca. Non si puo' usare layer.rect, perche' aiBoardRoot e'
    /// 100x150 mentre i suoi figli debordano (childControlWidth = 0).
    /// </summary>
    void ComputeLaneStep(RectTransform board, RectTransform layer)
    {
        _laneStep = null;
        if (!MeasureInParent(LaneRect(board, 0), layer, out var size0, out var c0)) return;

        float stride = size0.x;
        if (MeasureInParent(LaneRect(board, 1), layer, out _, out var c1))
            stride = Mathf.Abs(c1.x - c0.x);

        _laneStep = new LaneStep { first = c0, size = size0, stride = stride };
    }

    /// <summary>Un Text creato a runtime nasce senza font: senza questo non disegna nulla.</summary>
    static Font _fallbackFont;
    static Font FallbackFont()
    {
        if (_fallbackFont != null) return _fallbackFont;
        // niente ?? : su UnityEngine.Object bypasserebbe l'operatore == di Unity
        _fallbackFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_fallbackFont == null) _fallbackFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return _fallbackFont;
    }

    /// <summary>
    /// Copia sull'immagine del rullo l'artwork del prefab slot: sprite, colore,
    /// materiale. Il colore conta — piu' slot condividono lo stesso sprite e si
    /// distinguono solo per la tinta.
    /// </summary>
    void CopyArtwork(GameObject prefab, Image target)
    {
        if (prefab == null || target == null) return;

        var art = prefab.transform.Find(artChildName);
        var source = art != null ? art.GetComponent<Image>() : null;
        if (source == null)
        {
            target.color = coverColor;
            return;
        }

        target.sprite         = source.sprite;
        target.color          = source.color;
        target.material       = source.material;
        target.type           = source.type;
        target.preserveAspect = source.preserveAspect;
    }

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
