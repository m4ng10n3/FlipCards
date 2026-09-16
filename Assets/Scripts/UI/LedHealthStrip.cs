using UnityEngine;

/// <summary>
/// Pilota un <see cref="LedMatrix"/> con una vita: la barra non ha scritte, il
/// valore si legge da quanti LED sono accesi. Legge HP e il pronostico puro del
/// boss; non cambia mai lo stato di gioco.
///
/// Due disposizioni. <see cref="Fill.BottomUp"/> per la guancia verticale della
/// cassa; <see cref="Fill.FromCenter"/> per l'arco del fungo, che si svuota dai
/// due capi verso il centro e resta simmetrico sul bottone tondo.
///
/// Oltre al valore: una scansione lenta che passa sui LED accesi, la scia di
/// ferita che si spegne a brace, il lampo bianco quando la vita risale, il
/// battito sotto un quarto di vita e — solo per il boss — i LED che il clic in
/// campo toglierebbe, a intermittenza ambra.
/// </summary>
[RequireComponent(typeof(LedMatrix))]
public sealed class LedHealthStrip : MonoBehaviour
{
    public enum Fill { BottomUp, FromCenter }

    public bool boss;
    public Fill fill = Fill.BottomUp;
    public Color lit = new Color(1f, .16f, .05f);
    [Tooltip("Colore di un LED spento: il filamento freddo che si intravede nel foro.")]
    public Color ember = new Color(.10f, .025f, .012f);

    static readonly Color PreviewAmber = new Color(1f, .72f, .10f);

    LedMatrix _matrix;
    float _health = -1f, _trail = 1f, _flash, _clock;
    int _lastHp = int.MinValue;

    void Awake() => _matrix = GetComponent<LedMatrix>();

    void Update()
    {
        var gm = GameManager.Instance;
        if (_matrix == null) return;
        float dt = Time.unscaledDeltaTime;
        _clock += dt;

        float target = 1f;
        float preview = -1f;
        if (gm != null && gm.player != null && gm.ai != null)
        {
            int hp = boss ? gm.ai.hp : gm.player.hp;
            int max = boss ? gm.ai.maxHp : gm.player.maxHp;
            target = max > 0 ? Mathf.Clamp01((float)hp / max) : 0f;
            if (_lastHp != int.MinValue && hp > _lastHp) _flash = 1f;
            _lastHp = hp;
            var forecast = boss ? DamagePreviewController.Active : null;
            if (forecast != null && forecast.BossDamage > 0 && max > 0)
                preview = Mathf.Clamp01((float)forecast.DisplayedBossHp / max);
        }

        if (_health < 0f) { _health = target; _trail = target; }
        // Il valore scende in fretta, la scia resta accesa a brace e si spegne piano.
        _health = Mathf.MoveTowards(_health, target, dt * 1.6f);
        _trail = Mathf.Max(_health, Mathf.MoveTowards(_trail, _health, dt * .22f));
        _flash = Mathf.MoveTowards(_flash, 0f, dt * 2.2f);

        // Il bagliore legge la stessa texture: basta aggiornarne il contenuto.
        Paint(preview);
        _matrix.Apply();
    }

    /// <summary>Posizione del LED lungo la barra: 0 e' l'ultimo a spegnersi, 1 il primo.</summary>
    float Rank(int x, int y)
    {
        if (fill == Fill.BottomUp)
            return (y + .5f) / _matrix.Rows;
        float u = (x + .5f) / _matrix.Columns;
        return Mathf.Abs(u - .5f) * 2f;
    }

    void Paint(float preview)
    {
        int cols = _matrix.Columns, rows = _matrix.Rows;
        bool low = _health <= .25f && _health > 0f;
        float pulse = low ? .72f + .28f * Mathf.Sin(_clock * 6.5f) : 1f;
        float blink = .5f + .5f * Mathf.Sin(_clock * 9f);
        for (int y = 0; y < rows; y++)
        for (int x = 0; x < cols; x++)
        {
            float edge = Rank(x, y);
            bool on = edge <= _health;
            Color c; float g;
            if (on)
            {
                // Scansione: una fascia chiara che corre lungo la barra ogni 3 secondi.
                float along = fill == Fill.BottomUp ? edge : (x + .5f) / cols;
                float sweep = Mathf.Pow(Mathf.Max(0f, Mathf.Cos((along - Mathf.Repeat(_clock / 3.2f, 1.4f) + .2f) * 9f)), 12f);
                c = lit * pulse;
                c = Color.Lerp(c, Color.white, sweep * .28f + _flash * .55f);
                g = .75f * pulse + sweep * .25f;
                bool doomed = preview >= 0f && edge > preview;
                if (doomed) { c = Color.Lerp(lit, PreviewAmber, .35f + .65f * blink); g = .6f + .4f * blink; }
            }
            else if (edge <= _trail)
            {
                float k = Mathf.InverseLerp(_health, Mathf.Max(_trail, _health + 1e-4f), edge);
                c = Color.Lerp(lit * .55f, ember * 2f, k);
                g = .35f * (1f - k);
            }
            else { c = ember; g = 0f; }
            _matrix.SetPixel(x, y, c, g);
        }
    }
}
