using UnityEngine;

/// <summary>
/// La leva del rullo in tre pezzi tagliati dallo stesso disegno a inchiostro
/// (<c>SceneKit_v2/04_Controls/lever_rest.png</c>, vedi
/// <c>08_Integration/Tools/build_lever_parts.py</c>): il tamburo sta fermo,
/// appoggiato al fianco della cassa; l'asta ruota attorno al suo asse; la
/// manopola si avvicina alla camera.
///
/// L'asse fisico e' orizzontale, parallelo a quello dei rulli, quindi la corsa
/// avviene in profondita' e non nel piano dello schermo: la proiezione dice di
/// quanto l'asta si accorcia e la manopola cresce. Il disegno mostra la leva un
/// po' di fianco, inclinata di <see cref="screenLean"/>: la stessa inclinazione
/// vale per tutta la corsa, cosi a riposo i tre pezzi ricompongono l'originale.
///
/// L'animazione e' solo presentazione — il comando di fine turno parte dal
/// bottone, una volta sola, e non da qui.
/// </summary>
public sealed class MedallionLever : MonoBehaviour
{
    [Header("Pezzi")]
    public RectTransform hub;
    public RectTransform shaft;
    public RectTransform knob;

    [Tooltip("Posizione del perno (asse del tamburo) nello spazio ancorato del parent. La scrive il builder: leggerla da hub.anchoredPosition in Awake non funziona per i componenti aggiunti in edit mode.")]
    public Vector2 hinge;

    [Header("Geometria")]
    [Tooltip("Lunghezza fisica dell'asta dal perno al colletto, in pixel di canvas.")]
    public float length = 225f;
    [Tooltip("Inclinazione della camera, in gradi.")]
    public float cameraPitch = 20f;
    [Tooltip("Distanza della camera: governa quanto la manopola cresce avvicinandosi.")]
    public float focalDistance = 900f;
    [Tooltip("Scarto laterale di cio' che si avvicina alla camera: la leva sta a destra del centro.")]
    public float sideShift = 0.22f;
    [Tooltip("Inclinazione della leva nel disegno, in gradi: positiva se la cima pende a destra.")]
    public float screenLean = 10.77f;
    [Tooltip("Larghezza della tela dell'asta a riposo.")]
    public float shaftWidth = 31f;
    [Tooltip("Tela della manopola a riposo. Il pivot del rect e' il colletto, dove entra l'asta.")]
    public Vector2 knobSize = new Vector2(88f, 94f);
    [Tooltip("Oltre questo angolo l'asta viene verso la camera e passa davanti al tamburo.")]
    public float frontAngle = 40f;

    [Header("Corsa")]
    public float restAngle = 0f;
    public float pulledAngle = 105f;
    public float pullSeconds = 0.34f;
    public float holdSeconds = 0.12f;
    public float returnSeconds = 0.42f;

    float _clock = -1f;          // negativo = a riposo, nessuna corsa in atto
    SlotBatchManager _batch;
    bool _rolledLastFrame;
    bool _inFront;

    /// <summary>
    /// Monta la leva a riposo. La chiama il builder: in edit mode il ciclo di
    /// gioco non gira, quindi senza questa i pezzi resterebbero accatastati sul
    /// perno come li ha creati la costruzione.
    /// </summary>
    public void ApplyRest() => Apply(restAngle);

    /// <summary>Avvia la tirata. Chiamarla al clic: una corsa gia' in atto non riparte.</summary>
    public void Pull()
    {
        if (_clock < 0f) _clock = 0f;
    }

    void LateUpdate()
    {
        if (hub == null || shaft == null || knob == null) return;

        // Rete di sicurezza: se il rullo parte senza che il clic sia passato di
        // qui (fine turno automatica, comando da codice), la leva si muove lo
        // stesso — altrimenti il rullo girerebbe con la leva ferma.
        if (_batch == null) _batch = Object.FindAnyObjectByType<SlotBatchManager>();
        bool rolling = _batch != null && _batch.IsRolling;
        if (rolling && !_rolledLastFrame) Pull();
        _rolledLastFrame = rolling;

        float angle = restAngle;
        if (_clock >= 0f)
        {
            _clock += Time.unscaledDeltaTime;
            float hold = pullSeconds + holdSeconds;
            float total = hold + returnSeconds;

            if (_clock < pullSeconds)
                angle = Mathf.Lerp(restAngle, pulledAngle, EaseOutCubic(_clock / pullSeconds));
            else if (_clock < hold)
                angle = pulledAngle;
            else if (_clock < total)
                angle = Mathf.Lerp(pulledAngle, restAngle, EaseOutCubic((_clock - hold) / returnSeconds));
            else
            {
                angle = restAngle;
                _clock = -1f;
            }
        }

        Apply(angle);
    }

    float Perspective(float angleDegrees)
    {
        float rad = angleDegrees * Mathf.Deg2Rad;
        float pitch = cameraPitch * Mathf.Deg2Rad;
        float depth = length * Mathf.Sin(rad) * Mathf.Cos(pitch) + length * Mathf.Cos(rad) * Mathf.Sin(pitch);
        return focalDistance / Mathf.Max(1f, focalDistance - depth);
    }

    /// <summary>Proiezione dell'asse orizzontale: la corsa e' in profondita', non attorno alla Z.</summary>
    void Apply(float angleDegrees)
    {
        if (hub == null || shaft == null || knob == null) return;

        float rad = angleDegrees * Mathf.Deg2Rad;
        float pitch = cameraPitch * Mathf.Deg2Rad;
        float y = length * Mathf.Cos(rad);
        float z = length * Mathf.Sin(rad);

        // Scala relativa al riposo: a riposo i pezzi hanno la misura del disegno.
        float s = Perspective(angleDegrees) / Perspective(restAngle);

        // Coordinate ancorate: y verso l'alto, al contrario delle coordinate
        // banda della specifica.
        var tip = new Vector2(sideShift * z, y * Mathf.Cos(pitch) - z * Mathf.Sin(pitch)) * s;
        float lean = screenLean * Mathf.Deg2Rad;
        tip = new Vector2(tip.x * Mathf.Cos(lean) + tip.y * Mathf.Sin(lean),
                          -tip.x * Mathf.Sin(lean) + tip.y * Mathf.Cos(lean));

        var turn = Quaternion.Euler(0f, 0f, Mathf.Atan2(tip.y, tip.x) * Mathf.Rad2Deg - 90f);

        hub.anchoredPosition = hinge;
        shaft.anchoredPosition = hinge;
        shaft.sizeDelta = new Vector2(shaftWidth * s, Mathf.Max(1f, tip.magnitude));
        shaft.localRotation = turn;
        knob.anchoredPosition = hinge + tip;
        knob.sizeDelta = knobSize * s;
        knob.localRotation = turn;

        // A riposo l'asta esce da dietro il tamburo; tirata verso la camera esce
        // dal suo davanti. L'ordine dei fratelli e' l'ordine di disegno.
        bool front = angleDegrees > frontAngle;
        if (front == _inFront) return;
        _inFront = front;
        if (front) hub.SetSiblingIndex(shaft.GetSiblingIndex());
        else shaft.SetSiblingIndex(hub.GetSiblingIndex());
    }

    static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float inv = 1f - t;
        return 1f - inv * inv * inv;
    }
}
