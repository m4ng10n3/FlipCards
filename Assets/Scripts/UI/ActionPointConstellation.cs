using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gli action point come costellazione nel cielo: il simbolo del dorso delle
/// carte — rombo e tre occhi — con una stella per vertice. Ogni AP e' una
/// stella accesa; spendendolo la stella si gonfia di luce e si spegne nel suo
/// contorno freddo, riguadagnandolo si riaccende con un piccolo scatto.
///
/// Si spengono in senso antiorario a partire dall'ultima accesa (sinistra,
/// basso, destra, alto), cosi' l'ultima stella a restare e' quella in cima.
/// Le stelle oltre il tetto di AP del turno (<see cref="GameManager.MaxPlayerAP"/>)
/// restano spente e piu' fioche. Legge lo stato, non lo modifica.
/// </summary>
public sealed class ActionPointConstellation : MonoBehaviour
{
    [Tooltip("Linee e occhi della figura.")]
    public Image figure;
    [Tooltip("Una per vertice, nell'ordine alto, destra, basso, sinistra.")]
    public RectTransform[] stars = new RectTransform[4];
    public Image[] lit = new Image[4];
    public Image[] unlit = new Image[4];

    readonly float[] _level = new float[4];
    readonly float[] _flare = new float[4];
    readonly float[] _pop = new float[4];
    readonly bool[] _on = new bool[4];
    bool _initialized;
    float _clock;

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.player == null) return;
        float dt = Time.unscaledDeltaTime;
        _clock += dt;

        int ap = Mathf.Clamp(gm.player.actionPoints, 0, stars.Length);
        int cap = Mathf.Clamp(gm.MaxPlayerAP, 0, stars.Length);

        for (int i = 0; i < stars.Length; i++)
        {
            bool on = i < ap;
            if (!_initialized) { _on[i] = on; _level[i] = on ? 1f : 0f; }
            else if (on != _on[i])
            {
                _on[i] = on;
                if (on) _pop[i] = 1f; else _flare[i] = 1f;
            }

            // Si spegne dopo il lampo, si accende subito: lo scatto lo fa _pop.
            float target = on ? 1f : 0f;
            float speed = on ? 5f : (_flare[i] > .45f ? 0f : 2.6f);
            _level[i] = Mathf.MoveTowards(_level[i], target, dt * speed);
            _flare[i] = Mathf.MoveTowards(_flare[i], 0f, dt * 2.2f);
            _pop[i] = Mathf.MoveTowards(_pop[i], 0f, dt * 3.5f);

            float twinkle = 1f + .06f * Mathf.Sin(_clock * (1.3f + .37f * i) + i * 2.1f);
            float swell = Mathf.Sin(_flare[i] * Mathf.PI) * .55f;
            float pop = Mathf.Sin(_pop[i] * Mathf.PI) * .28f;
            float scale = Mathf.Lerp(.8f, 1f, _level[i]) * twinkle + swell + pop;
            stars[i].localScale = Vector3.one * scale;

            SetAlpha(lit[i], Mathf.Clamp01(_level[i] + _flare[i] * .8f));
            SetAlpha(unlit[i], (1f - _level[i]) * (i < cap ? .9f : .35f));
        }
        _initialized = true;

        // La figura prende luce dalle stelle che la reggono: a zero AP resta un
        // disegno appena visibile nel cielo.
        if (figure != null)
        {
            float mean = 0f;
            for (int i = 0; i < _level.Length; i++) mean += _level[i];
            mean /= _level.Length;
            SetAlpha(figure, .32f + .5f * mean + .04f * Mathf.Sin(_clock * .8f));
        }
    }

    static void SetAlpha(Graphic g, float a)
    {
        if (g == null) return;
        var c = g.color;
        c.a = a;
        g.color = c;
    }
}
