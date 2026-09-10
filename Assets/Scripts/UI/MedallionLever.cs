using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// La leva del rullo in tre pezzi: perno fermo, asta che ruota, manopola che si
/// avvicina alla camera. Sostituisce la rotazione di un'immagine sola, che non
/// poteva mostrare la tirata perche' l'asse fisico e' orizzontale e la corsa
/// avviene in profondita', non nel piano dello schermo.
///
/// Proiezione e tempi vengono da
/// <c>Assets/Graphics/NeonMonte/SceneKit_v1/Layout/animation_spec.md</c>: la
/// manopola si ingrandisce avvicinandosi (s), l'asta si accorcia in proiezione.
/// L'animazione e' solo presentazione — il comando di fine turno parte dal
/// bottone, una volta sola, e non da qui.
/// </summary>
public sealed class MedallionLever : MonoBehaviour
{
    [Header("Pezzi")]
    public RectTransform pivot;
    public RectTransform shaft;
    public RectTransform grip;

    [Tooltip("Posizione del perno nello spazio ancorato del parent. La scrive il builder: leggerla da pivot.anchoredPosition in Awake non funziona per i componenti aggiunti in edit mode.")]
    public Vector2 hinge;

    [Header("Geometria")]
    [Tooltip("Lunghezza fisica dell'asta, in pixel di canvas.")]
    public float length = 212f;
    [Tooltip("Inclinazione della camera, in gradi.")]
    public float cameraPitch = 20f;
    [Tooltip("Distanza della camera: governa quanto la manopola cresce avvicinandosi.")]
    public float focalDistance = 900f;
    [Tooltip("Larghezza della TELA dell'asta, non dell'asta: nello sprite il tondino occupa il 14% della larghezza, il resto e' trasparente. A 110 il brass viene circa 16px.")]
    public float shaftWidth = 110f;
    [Tooltip("Lato della manopola a riposo.")]
    public float gripSize = 96f;

    [Header("Corsa")]
    public float restAngle = -16f;
    public float pulledAngle = 94f;
    public float pullSeconds = 0.34f;
    public float holdSeconds = 0.12f;
    public float returnSeconds = 0.42f;

    float _clock = -1f;          // negativo = a riposo, nessuna corsa in atto
    SlotBatchManager _batch;
    bool _rolledLastFrame;

    /// <summary>
    /// Monta la leva a riposo. La chiama il builder: in edit mode il ciclo di
    /// gioco non gira, quindi senza questa i tre pezzi resterebbero accatastati
    /// sul perno come li ha creati la costruzione.
    /// </summary>
    public void ApplyRest() => Apply(restAngle);

    /// <summary>Avvia la tirata. Chiamarla al clic: una corsa gia' in atto non riparte.</summary>
    public void Pull()
    {
        if (_clock < 0f) _clock = 0f;
    }

    void LateUpdate()
    {
        if (pivot == null || shaft == null || grip == null) return;

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

    /// <summary>Proiezione dell'asse orizzontale: la corsa e' in profondita', non attorno alla Z.</summary>
    void Apply(float angleDegrees)
    {
        float rad = angleDegrees * Mathf.Deg2Rad;
        float pitch = cameraPitch * Mathf.Deg2Rad;
        float cosPitch = Mathf.Cos(pitch), sinPitch = Mathf.Sin(pitch);

        float y = length * Mathf.Cos(rad);
        float z = length * Mathf.Sin(rad);
        float depth = z * cosPitch + y * sinPitch;
        float s = focalDistance / Mathf.Max(1f, focalDistance - depth);

        // Coordinate ancorate: y verso l'alto, al contrario delle coordinate
        // banda della specifica, da cui il segno invertito.
        float dx = 0.22f * z * s;
        float dy = (y * cosPitch - z * sinPitch) * s;

        pivot.anchoredPosition = hinge;
        grip.anchoredPosition = hinge + new Vector2(dx, dy);
        grip.sizeDelta = new Vector2(gripSize * s, gripSize * s);

        float span = Mathf.Max(1f, new Vector2(dx, dy).magnitude);
        shaft.anchoredPosition = hinge;
        shaft.sizeDelta = new Vector2(shaftWidth * s, span);
        shaft.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dy, dx) * Mathf.Rad2Deg - 90f);
    }

    static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float inv = 1f - t;
        return 1f - inv * inv * inv;
    }
}
