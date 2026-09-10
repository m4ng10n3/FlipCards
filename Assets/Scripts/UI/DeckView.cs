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
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DeckView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Riferimenti")]
    [Tooltip("Rect in cui viene impilata la pila. La scala delle carte si ricava dalla sua altezza.")]
    public RectTransform stackRoot;
    [Tooltip("Riga di stato dentro la carta in cima: costo, mazzo vuoto, mano piena.")]
    public TMP_Text hintText;
    [Tooltip("Immagine della pila del kit (deck_stack_N). Se assegnata sostituisce la pila di prefab veri.")]
    public Image stackImage;
    [Tooltip("Alone di 'puoi pescare' del kit (deck_pulse), acceso solo quando il clic farebbe davvero qualcosa.")]
    public Image pulseImage;

    [Header("Pila")]
    [Tooltip("Quante carte si vedono al massimo nella pila, a mazzo pieno.")]
    [Min(1)] public int maxLayers = 3;
    [Tooltip("Scarto fra una carta e la successiva della pila, in pixel. La y va verso il basso: la pila si appoggia al tavolo.")]
    public Vector2 layerOffset = new Vector2(-2f, -3f);
    [Tooltip("Margine verticale fra la carta in cima e il bordo del rect della pila.")]
    public float verticalPadding = 12f;

    [Header("Spessore")]
    [Tooltip("Bordi di carta disegnati sotto la pila. Sono il grosso dello spessore: costano un'Image l'uno invece di un prefab con Canvas annidato.")]
    [Min(0)] public int maxEdges = 16;
    [Tooltip("Scarto fra un bordo e il successivo.")]
    public Vector2 edgeOffset = new Vector2(-1.7f, -2.7f);
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

        // Lo spessore vero sta nei bordi, non nelle carte: sul riferimento si
        // vede una faccia sola e sotto una ventina di tagli di carta. Le carte
        // vere servono perche' il dorso in cima sia davvero quello che uscira'.
        int edges = maxEdges <= 0 ? 0 : Mathf.Clamp(Mathf.CeilToInt(count / (float)start * maxEdges), 1, maxEdges);
        var cardRect = (RectTransform)next[0].transform;
        float scale = CardScale(cardRect);
        var edgeSize = new Vector2(cardRect.rect.width * scale, cardRect.rect.height * scale);
        var deepest = layerOffset * (next.Count - 1);
        for (int k = edges; k >= 1; k--)
            _layers.Add(BuildEdge(edgeSize, deepest + edgeOffset * k, (next.Count + k) * 2f));

        // Si costruisce dal fondo: l'ordine di disegno fra sub-canvas annidati lo
        // decide la z (ogni carta ha un Canvas sul figlio Visual), non la
        // gerarchia. La carta in cima sta a z 0, quelle dietro a z crescente —
        // stessa convenzione dell'ombra della carta.
        for (int i = next.Count - 1; i >= 0; i--)
            _layers.Add(BuildLayer(next[i], i, isTop: i == 0));
    }

    /// <summary>Un taglio di carta: foglio chiaro con la riga scura del bordo inferiore.</summary>
    GameObject BuildEdge(Vector2 size, Vector2 offset, float z)
    {
        var go = new GameObject("DeckEdge", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(stackRoot, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = offset;
        rt.localPosition = new Vector3(rt.localPosition.x, rt.localPosition.y, z);

        var paper = go.GetComponent<Image>();
        paper.color = edgePaper;
        paper.raycastTarget = false;

        var line = new GameObject("Line", typeof(RectTransform), typeof(Image));
        var lrt = (RectTransform)line.transform;
        lrt.SetParent(rt, false);
        lrt.anchorMin = new Vector2(0f, 0f);
        lrt.anchorMax = new Vector2(1f, 0f);
        lrt.pivot = new Vector2(0.5f, 0f);
        lrt.sizeDelta = new Vector2(0f, 1.4f);
        lrt.anchoredPosition = Vector2.zero;
        var lineImage = line.GetComponent<Image>();
        lineImage.color = edgeLine;
        lineImage.raycastTarget = false;

        return go;
    }

    GameObject BuildLayer(GameObject prefab, int index, bool isTop)
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
        float scale = CardScale(rt);

        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one * scale;
        rt.anchoredPosition = layerOffset * index;
        rt.localPosition = new Vector3(rt.localPosition.x, rt.localPosition.y, index * 2f);

        return go;
    }

    float CardScale(RectTransform card)
    {
        if (stackRoot == null) return 1f;

        float h = card.rect.height, w = card.rect.width;
        if (h <= 1f || w <= 1f) return 1f;

        // Lo spessore occupa spazio quanto le carte: senza contarlo, la pila a
        // mazzo pieno sborda dal rect.
        float spreadY = Mathf.Abs(layerOffset.y) * (maxLayers - 1) + Mathf.Abs(edgeOffset.y) * maxEdges;
        float spreadX = Mathf.Abs(layerOffset.x) * (maxLayers - 1) + Mathf.Abs(edgeOffset.x) * maxEdges;
        float availableH = stackRoot.rect.height - verticalPadding * 2f - spreadY;
        float availableW = stackRoot.rect.width - spreadX;
        return Mathf.Max(0.05f, Mathf.Min(availableH / h, availableW / w));
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
