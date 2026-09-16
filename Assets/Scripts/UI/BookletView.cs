using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Il libretto del dettaglio come oggetto: si apre girando la copertina, cambia
/// sezione sfogliando, e i segnalibri sono linguette d'indice infilate fra le
/// pagine.
///
/// <b>I segnalibri stanno dal lato giusto.</b> La linguetta di una sezione e'
/// attaccata al primo foglio di quella sezione: aperto alla sezione k, le
/// linguette delle sezioni prima di k sporgono a sinistra e le altre a destra.
/// Sfogliando in avanti, ogni foglio che gira si porta dietro la sua linguetta,
/// che segue il bordo esterno del foglio (<see cref="BookLeaf.Edge"/>) e passa
/// sopra il libro fino all'altro lato.
///
/// <b>Il contenuto non gira col foglio.</b> I fogli sono carta stampata senza
/// testo: prima di sfogliare il testo sparisce, la sezione nuova si scrive sotto
/// il foglio che si alza (la pagina che si scopre e' gia' quella nuova) e la
/// pagina coperta dall'atterraggio compare quando il foglio si posa.
///
/// Solo presentazione: le sezioni le riempie <see cref="TableOverlayController"/>.
/// </summary>
public sealed class BookletView : MonoBehaviour
{
    public TableOverlayController controller;

    [Header("Struttura (dal builder)")]
    public CanvasGroup book;
    public RectTransform tabsUnder, flipLayer;
    public Image spreadLeft;
    public CanvasGroup leftContent, rightContent;
    public BookLeaf cover;
    public BookLeaf[] leaves;
    public RectTransform[] tabCarriers;
    public BookmarkTab[] tabs;

    [Header("Misure (unita' del libro, dalla piega)")]
    public float pageWidth = 613f;
    public float[] tabHeights;

    [Header("Tempi")]
    public float openSeconds = .62f;
    public float closeSeconds = .40f;
    public float leafSeconds = .52f;
    public float leafStagger = .11f;
    public float curl = .85f;

    public int Current { get; private set; }
    public bool Busy => _routine != null;

    Coroutine _routine;
    Action _closed;

    void OnDisable()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = null;
        ResetPose();
        // Spento da fuori (fine partita, ESC durante la chiusura): nessuna
        // richiamata, il modale e' gia' in fase di disattivazione.
        _closed = null;
    }

    // ── Comandi ───────────────────────────────────────────────────────────────

    public void PlayOpen(int section)
    {
        Stop();
        Current = Mathf.Clamp(section, 0, tabCarriers.Length - 1);
        controller.ApplySection(Current);
        _routine = StartCoroutine(Open());
    }

    public void PlayClose(Action closed)
    {
        if (!isActiveAndEnabled) { closed?.Invoke(); return; }
        Stop();
        _closed = closed;
        _routine = StartCoroutine(Close());
    }

    /// <summary>Clic su un segnalibro.</summary>
    public void Select(int section)
    {
        section = Mathf.Clamp(section, 0, tabCarriers.Length - 1);
        if (Busy) return;
        if (section == Current) { tabs[section].Nudge(); return; }
        _routine = StartCoroutine(Turn(section));
    }

    void Stop()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = null;
        ResetPose();
    }

    // ── Pose di riposo ────────────────────────────────────────────────────────

    void ResetPose()
    {
        if (book != null) { book.alpha = 1f; book.transform.localScale = Vector3.one; }
        if (spreadLeft != null) SetAlpha(spreadLeft, 1f);
        if (leftContent != null) leftContent.alpha = 1f;
        if (rightContent != null) rightContent.alpha = 1f;
        if (cover != null) cover.gameObject.SetActive(false);
        if (leaves != null) foreach (var leaf in leaves) if (leaf != null) leaf.gameObject.SetActive(false);
        for (int i = 0; tabCarriers != null && i < tabCarriers.Length; i++) RestTab(i);
    }

    void RestTab(int index)
    {
        bool left = index < Current;
        PlaceTab(index, left ? Mathf.PI : 0f, 0f, tabsUnder);
        tabs[index].Active = index == Current;
    }

    void PlaceTab(int index, float angle, float curlAmount, RectTransform layer)
    {
        var carrier = tabCarriers[index];
        if (carrier.parent != layer) carrier.SetParent(layer, false);
        var edge = BookLeaf.Edge(pageWidth, angle, curlAmount, 16, out float edgeAngle);
        carrier.localPosition = new Vector3(edge.x, tabHeights[index], edge.z);
        carrier.localRotation = Quaternion.Euler(0f, edgeAngle * Mathf.Rad2Deg, 0f);
        tabs[index].SetMirrored(edgeAngle > Mathf.PI * .5f);
    }

    static void SetAlpha(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }

    static float Ease(float t) => t * t * (3f - 2f * t);

    // ── Animazioni ────────────────────────────────────────────────────────────

    IEnumerator Open()
    {
        ResetPose();
        book.alpha = 0f;
        SetAlpha(spreadLeft, 0f);
        leftContent.alpha = 0f;
        rightContent.alpha = 0f;
        // Chiuso, il libro mostra solo il piatto sopra la meta' destra.
        cover.gameObject.SetActive(true);
        cover.SetAngle(0f, 0f);
        foreach (var i in LeftTabs()) tabCarriers[i].gameObject.SetActive(false);

        const float fade = .16f;
        for (float t = 0; t < fade; t += Time.unscaledDeltaTime)
        {
            float k = Ease(t / fade);
            book.alpha = k;
            book.transform.localScale = Vector3.one * Mathf.Lerp(.94f, 1f, k);
            yield return null;
        }
        book.alpha = 1f; book.transform.localScale = Vector3.one;

        bool landedLeft = false;
        for (float t = 0; t < openSeconds; t += Time.unscaledDeltaTime)
        {
            float k = Ease(t / openSeconds);
            float angle = k * Mathf.PI;
            cover.SetAngle(angle, curl * .35f);   // il cartone si piega poco
            rightContent.alpha = Mathf.Clamp01((angle - Mathf.PI * .25f) / (Mathf.PI * .4f));
            if (!landedLeft && angle > Mathf.PI * .5f)
            {
                landedLeft = true;
                SetAlpha(spreadLeft, 1f);
                foreach (var i in LeftTabs()) tabCarriers[i].gameObject.SetActive(true);
            }
            yield return null;
        }
        cover.gameObject.SetActive(false);
        for (float t = 0; t < .14f; t += Time.unscaledDeltaTime)
        {
            leftContent.alpha = Ease(t / .14f);
            yield return null;
        }
        ResetPose();
        _routine = null;
    }

    IEnumerator Close()
    {
        for (float t = 0; t < .1f; t += Time.unscaledDeltaTime)
        {
            float k = 1f - t / .1f;
            leftContent.alpha = k; rightContent.alpha = k;
            yield return null;
        }
        leftContent.alpha = 0f; rightContent.alpha = 0f;
        cover.gameObject.SetActive(true);
        bool hiddenLeft = false;
        for (float t = 0; t < closeSeconds; t += Time.unscaledDeltaTime)
        {
            float angle = (1f - Ease(t / closeSeconds)) * Mathf.PI;
            cover.SetAngle(angle, -curl * .35f);
            if (!hiddenLeft && angle < Mathf.PI * .5f)
            {
                hiddenLeft = true;
                SetAlpha(spreadLeft, 0f);
                foreach (var i in LeftTabs()) tabCarriers[i].gameObject.SetActive(false);
            }
            yield return null;
        }
        cover.SetAngle(0f, 0f);
        for (float t = 0; t < .12f; t += Time.unscaledDeltaTime)
        {
            book.alpha = 1f - Ease(t / .12f);
            book.transform.localScale = Vector3.one * Mathf.Lerp(1f, .96f, t / .12f);
            yield return null;
        }
        foreach (var i in LeftTabs()) tabCarriers[i].gameObject.SetActive(true);
        _routine = null;
        var done = _closed; _closed = null;
        done?.Invoke();
    }

    IEnumerator Turn(int target)
    {
        int from = Current;
        bool forward = target > from;
        int count = Mathf.Abs(target - from);

        // Il testo sparisce prima che il foglio si alzi: i fogli sono carta nuda.
        for (float t = 0; t < .1f; t += Time.unscaledDeltaTime)
        {
            float k = 1f - t / .1f;
            leftContent.alpha = k; rightContent.alpha = k;
            yield return null;
        }
        leftContent.alpha = 0f; rightContent.alpha = 0f;
        foreach (var tab in tabs) tab.Active = false;

        // La sezione nuova si scrive subito: la pagina che il foglio scopre e' gia' lei.
        Current = target;
        controller.ApplySection(target);
        if (forward) rightContent.alpha = 1f; else leftContent.alpha = 1f;

        // Un foglio per sezione attraversata: il pool ne ha uno per ogni salto possibile.
        int sheets = Mathf.Min(count, leaves.Length);
        float total = leafSeconds + leafStagger * (sheets - 1);
        for (int i = 0; i < sheets; i++)
        {
            var leaf = leaves[i];
            leaf.gameObject.SetActive(true);
            leaf.transform.SetAsLastSibling();
            leaf.SetAngle(forward ? 0f : Mathf.PI, 0f);
        }
        // La linguetta portata da ogni foglio: in avanti quelle da 'from' in su,
        // indietro quelle che tornano a destra.
        int[] carried = new int[count];
        for (int i = 0; i < count; i++) carried[i] = forward ? from + i : from - 1 - i;
        foreach (var index in carried) tabCarriers[index].SetParent(flipLayer, false);

        for (float t = 0; t <= total + Time.unscaledDeltaTime; t += Time.unscaledDeltaTime)
        {
            for (int i = 0; i < count; i++)
            {
                // Con piu' sezioni che fogli, gli ultimi fogli portano piu' linguette.
                int sheet = Mathf.Min(i, sheets - 1);
                float local = Mathf.Clamp01((t - sheet * leafStagger) / leafSeconds);
                float k = Ease(local);
                float angle = forward ? k * Mathf.PI : (1f - k) * Mathf.PI;
                float bend = forward ? curl : -curl;
                if (i < sheets) leaves[i].SetAngle(angle, bend);
                PlaceTab(carried[i], angle, bend, flipLayer);
            }
            yield return null;
        }

        foreach (var leaf in leaves) leaf.gameObject.SetActive(false);
        for (float t = 0; t < .14f; t += Time.unscaledDeltaTime)
        {
            float k = Ease(t / .14f);
            if (forward) leftContent.alpha = k; else rightContent.alpha = k;
            yield return null;
        }
        _routine = null;
        ResetPose();
    }

    System.Collections.Generic.IEnumerable<int> LeftTabs()
    {
        for (int i = 0; i < Current && i < tabCarriers.Length; i++) yield return i;
    }
}
