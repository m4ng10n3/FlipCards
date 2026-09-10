using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Il mazzo come oggetto fisico: una pila di carte vere, tutte di dorso, che si
/// assottiglia man mano. Cliccandola si pesca.
///
/// Sostituisce il bottone PESCA, che non comunicava ne' quante carte restassero
/// ne' cosa stesse per uscire, e a mazzo vuoto o mano piena non faceva nulla
/// senza dirlo.
///
/// La pila e' fatta con i prefab carta veri, non con una grafica finta: cosi il
/// dorso della prossima carta e' davvero il suo. Le copie sono decorative — i
/// componenti di input vengono spenti — e sono poche, perche' ogni prefab si
/// porta dietro un Canvas annidato e un CardDefinition.
///
/// <b>La pila sta sul piano del tavolo.</b> <see cref="stackRoot"/> vive dentro
/// un rect ruotato come il panno (lo monta il builder) e lo spessore cresce lungo
/// la sua z locale, cioe' la normale del tavolo verso la camera. Prima i tagli di
/// carta erano scalati nel piano della carta ruotata: lo spessore usciva in
/// diagonale e la pila sembrava un libro storto.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DeckView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Riferimenti")]
    [Tooltip("Rect in cui viene impilata la pila. Deve stare sul piano del tavolo: la sua z locale e' lo spessore.")]
    public RectTransform stackRoot;
    [Tooltip("Riga di stato dentro la carta in cima: costo, mazzo vuoto, mano piena.")]
    public TMP_Text hintText;
    [Tooltip("Immagine della pila del kit (deck_stack_N). Se assegnata sostituisce la pila di prefab veri.")]
    public Image stackImage;
    [Tooltip("Alone di 'puoi pescare' del kit (deck_pulse), acceso solo quando il clic farebbe davvero qualcosa.")]
    public Image pulseImage;

    [Header("Pila")]
    [Tooltip("Quante carte vere stanno in cima alla pila, a mazzo pieno. Sotto ci sono solo tagli di carta.")]
    [Min(1)] public int maxLayers = 1;
    [Tooltip("Scala delle carte della pila rispetto al prefab.")]
    public float cardScale = 1f;

    [Header("Spessore")]
    [Tooltip("Tagli di carta sotto la carta in cima. Sono il grosso dello spessore: costano un'Image l'uno invece di un prefab con Canvas annidato.")]
    [Min(0)] public int maxEdges = 16;
    [Tooltip("Spessore di un taglio, lungo la normale del tavolo.")]
    public float edgeThickness = 2.2f;
    [Tooltip("Scarto di un taglio nel piano della pila, nello spazio di stackRoot. Il mazzo sta a sinistra e lo si guarda dal centro del tavolo: i tagli alti scivolano verso sinistra e lo spessore si vede sul fianco destro, come nel riferimento.")]
    public Vector2 edgeShift = Vector2.zero;
    public Color edgePaper = new Color(.90f, .85f, .74f, 1f);
    public Color edgeLine = new Color(.28f, .19f, .11f, .85f);

    readonly List<GameObject> _layers = new List<GameObject>();

    int _lastCount = -1;
    string _lastHint;
    GameObject _topPrefab;
    Tween _stackTween;
    Tween _pulseTween;
    bool _pulseOn;

    void LateUpdate()
    {
        var gm = GameManager.Instance;
        var hand = gm != null ? gm.HandManager : null;
        if (hand == null) return;

        int count = hand.DeckCount;
        if (count != _lastCount)
        {
            _lastCount = count;
            RebuildStack(hand, count);
        }

        UpdateHint(hand, count);
        UpdatePulse(gm, hand, count);
    }

    /// <summary>
    /// L'alone del kit dice "questo clic pesca", non "qui c'e' un mazzo": si
    /// accende solo quando pescare e' davvero possibile — fase giusta, carte nel
    /// mazzo, mano non piena — cosi il rifiuto non arriva mai a sorpresa.
    /// </summary>
    void UpdatePulse(GameManager gm, HandManager hand, int count)
    {
        if (pulseImage == null) return;

        bool canDraw = gm.CanAct && count > 0 && !hand.HandIsFull;
        if (canDraw != _pulseOn)
        {
            _pulseOn = canDraw;
            _pulseTween?.Kill();
            if (!canDraw)
            {
                pulseImage.enabled = false;
                return;
            }

            pulseImage.enabled = true;
            var c = pulseImage.color;
            _pulseTween = DOTween.To(() => c.a, a => { c.a = a; pulseImage.color = c; }, 1f, 0.9f)
                                 .From(0.35f)
                                 .SetLoops(-1, LoopType.Yoyo)
                                 .SetEase(Ease.InOutSine)
                                 .SetUpdate(true)
                                 .SetLink(gameObject);
        }
    }

    // ── Interazione ───────────────────────────────────────────────────────────

    public void OnPointerClick(PointerEventData eventData)
    {
        var gm = GameManager.Instance;
        var hand = gm != null ? gm.HandManager : null;
        if (gm == null || hand == null) return;

        if (!gm.CanAct)
        {
            Logger.Info("Pesca: non in questa fase");
            Refuse();
            return;
        }

        if (hand.DrawCard()) Accept();
        else Refuse();
    }

    /// <summary>Il mazzo si ispeziona come le carte: la prossima e' un'informazione, non una sorpresa.</summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        var definition = _topPrefab != null ? _topPrefab.GetComponent<CardDefinition>() : null;
        if (definition != null) InspectorPanel.Instance?.ShowCardPreview(definition);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        var definition = _topPrefab != null ? _topPrefab.GetComponent<CardDefinition>() : null;
        if (definition != null) InspectorPanel.Instance?.HideFor(definition);
    }

    void Accept()
    {
        if (stackRoot == null) return;
        _stackTween?.Kill(complete: true);
        _stackTween = stackRoot.DOPunchAnchorPos(new Vector2(0f, -14f), 0.28f, 5, 0.6f)
                               .SetUpdate(true)
                               .SetLink(gameObject);
    }

    void Refuse()
    {
        if (stackRoot == null) return;
        _stackTween?.Kill(complete: true);
        _stackTween = stackRoot.DOShakeAnchorPos(0.3f, new Vector2(10f, 0f), 14, 90f, false, false)
                               .SetUpdate(true)
                               .SetLink(gameObject);
    }

    // ── Pila ──────────────────────────────────────────────────────────────────

    void RebuildStack(HandManager hand, int count)
    {
        // Con la pila del kit lo spessore e' gia' negli sprite (deck_stack_0..5,
        // per scaglioni di carte residue) e non serve impilare prefab veri: il
        // dorso del kit e' generico, come nel tabellone di riferimento. La carta
        // che sta per uscire resta leggibile passandoci sopra, dall'ispettore.
        if (stackImage != null)
        {
            _topPrefab = null;
            var peek = hand.PeekDeck(1);
            if (peek.Count > 0) _topPrefab = peek[0];

            var sprite = UiSkin.Sprite(UiSkin.DeckStack(count));
            if (sprite != null) stackImage.sprite = sprite;
            stackImage.enabled = count > 0 || sprite != null;
            return;
        }

        ClearLayers();
        if (stackRoot == null) return;

        // Lo spessore e' proporzionale al residuo: e' l'unica lettura a colpo
        // d'occhio di quanto mazzo resta.
        int start = Mathf.Max(1, hand.DeckStartCount);
        int layers = count <= 0 ? 0 : Mathf.Clamp(Mathf.CeilToInt(count / (float)start * maxLayers), 1, maxLayers);
        if (layers == 0)
        {
            _topPrefab = null;
            return;
        }

        var next = hand.PeekDeck(layers);
        if (next.Count == 0) { _topPrefab = null; return; }

        _topPrefab = next[0];

        int edges = maxEdges <= 0 ? 0 : Mathf.Clamp(Mathf.CeilToInt(count / (float)start * maxEdges), 1, maxEdges);
        var cardRect = (RectTransform)next[0].transform;
        var size = cardRect.rect.size * cardScale;

        // Dal fondo verso la cima, un taglio sopra l'altro lungo la normale. Fra
        // i tagli (Image del canvas radice) decide la gerarchia; la carta vera
        // ha un Canvas annidato e sta piu' vicina alla camera di tutti, quindi
        // disegna sopra anche con l'ordinamento per distanza.
        int slice = 0;
        for (int k = 0; k < edges; k++)
            _layers.Add(BuildEdge(size, slice++));
        for (int i = next.Count - 1; i >= 0; i--)
            _layers.Add(BuildLayer(next[i], i, slice++));
    }

    Vector3 Lift(int slice) => new Vector3(edgeShift.x * slice, edgeShift.y * slice, -slice * edgeThickness);

    /// <summary>
    /// Un taglio di carta: foglio chiaro filettato sui quattro lati. Si vedono
    /// solo i filetti dei lati rivolti alla camera — gli altri li copre il taglio
    /// di sopra — quindi non serve sapere come e' girata la pila.
    /// </summary>
    GameObject BuildEdge(Vector2 size, int slice)
    {
        var go = new GameObject("DeckEdge", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(stackRoot, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        rt.localPosition = Lift(slice);

        var paper = go.GetComponent<Image>();
        paper.color = edgePaper;
        paper.raycastTarget = false;

        Line(rt, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1.4f));
        Line(rt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1.4f));
        Line(rt, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1.4f, 0f));
        Line(rt, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1.4f, 0f));
        return go;
    }

    void Line(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
    {
        var line = new GameObject("Line", typeof(RectTransform), typeof(Image));
        var lrt = (RectTransform)line.transform;
        lrt.SetParent(parent, false);
        lrt.anchorMin = anchorMin;
        lrt.anchorMax = anchorMax;
        lrt.pivot = (anchorMin + anchorMax) * 0.5f;
        lrt.sizeDelta = size;
        lrt.anchoredPosition = Vector2.zero;
        var image = line.GetComponent<Image>();
        image.color = edgeLine;
        image.raycastTarget = false;
    }

    GameObject BuildLayer(GameObject prefab, int index, int slice)
    {
        var go = Instantiate(prefab, stackRoot);
        go.name = $"DeckLayer_{index}";
        go.SetActive(true);

        // Copia decorativa: senza spegnere l'input ogni carta della pila
        // gestirebbe il proprio click e il mazzo non riceverebbe mai il suo.
        var definition = go.GetComponent<CardDefinition>();
        if (definition != null) definition.enabled = false;

        var view = go.GetComponentInChildren<CardView>(true);
        if (view != null)
        {
            var overlay = view.GetComponent<CardOverlay>();
            if (overlay != null) overlay.enabled = false;
            view.ShowDeckBack(false);
            view.enabled = false;   // niente hover, tilt, ombra o inseguimento del container
        }

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one * cardScale;
        rt.anchoredPosition = Vector2.zero;
        rt.localPosition = Lift(slice);

        return go;
    }

    void ClearLayers()
    {
        // Distacco prima del Destroy, che e' differito a fine frame: senza,
        // vecchia e nuova pila convivrebbero per un frame sovrapposte.
        for (int i = 0; i < _layers.Count; i++)
        {
            if (_layers[i] == null) continue;
            _layers[i].transform.SetParent(null, false);
            Destroy(_layers[i]);
        }
        _layers.Clear();
    }

    // ── Stato ─────────────────────────────────────────────────────────────────

    void UpdateHint(HandManager hand, int count)
    {
        if (hintText == null) return;

        string text;
        Color color;

        if (count <= 0) { text = "mazzo esaurito"; color = GamePalette.Danger; }
        else if (hand.HandIsFull) { text = $"mano piena {hand.HandCount}/{hand.MaxHandSize}"; color = GamePalette.Danger; }
        else { text = "clic per pescare · 1 AP"; color = GamePalette.TextMuted; }

        if (text == _lastHint) return;
        _lastHint = text;
        hintText.text = text;
        hintText.color = color;
    }
}
