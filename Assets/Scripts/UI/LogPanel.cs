using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Autoscroll del log. Il pannello precedente non scrollava: le righe nuove
/// finivano fuori dal viewport e il giocatore leggeva sempre l'inizio della
/// partita. Il tetto al buffer sta in GameManager.AppendLog.
/// </summary>
[RequireComponent(typeof(ScrollRect))]
public class LogPanel : MonoBehaviour
{
    ScrollRect _scroll;
    bool _dirty;

    void Awake() => _scroll = GetComponent<ScrollRect>();

    void OnEnable()
    {
        GameManager.LogChanged += MarkDirty;
        _dirty = true;
    }

    void OnDisable() => GameManager.LogChanged -= MarkDirty;

    void MarkDirty() => _dirty = true;

    void LateUpdate()
    {
        if (!_dirty) return;
        _dirty = false;

        // Il ContentSizeFitter aggiorna l'altezza nel layout pass: senza il
        // ForceUpdateCanvases lo scroll userebbe la misura del frame precedente.
        Canvas.ForceUpdateCanvases();
        _scroll.verticalNormalizedPosition = 0f;
    }
}
