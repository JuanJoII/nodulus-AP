using System.Collections;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════════════
// AudioBridge — Puente centralizado entre eventos de Nodulus y sintetizadores.
//
// Resuelve el problema de polifonía en SFX mediante un pool round-robin de
// instancias SynthSFX. Cada sonido toma la siguiente instancia libre del pool,
// permitiendo que múltiples sonidos suenen simultáneamente sin interrumpirse.
//
// Uso desde GameAudio:
//   Audiobridge.Instance.Trigger(GameClip.NodeEnter, volumeScale);
// ═══════════════════════════════════════════════════════════════════════════════

public class Audiobridge : MonoBehaviour
{
    public static Audiobridge Instance { get; private set; }

    [Header("Pool de SynthSFX (asignar 5-6 instancias en el Inspector)")]
    public SynthSFX[] sfxPool;

    [Header("Referencia a música de fondo")]
    public SynthAmbient synthAmbient;

    private int _poolIndex = 0;

    // ── Singleton ─────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // ── Pool round-robin ──────────────────────────────────────────────────

    private SynthSFX Next()
    {
        SynthSFX sfx = sfxPool[_poolIndex];
        _poolIndex = (_poolIndex + 1) % sfxPool.Length;
        return sfx;
    }

    // ── API pública ───────────────────────────────────────────────────────

    public void Trigger(GameClip clip, float volumeScale = 1f)
    {
        Debug.Log($"[Audiobridge] Trigger: {clip} | pool size={sfxPool?.Length}");

        switch (clip)
        {
            // ── EVENTOS DE NODO ───────────────────────────────────────────

            case GameClip.NodeEnter:
                // Seno puro: suave, cristalino, no interrumpe el flujo
                Next().Play(880f, SynthSFX.SynthType.Sine,
                    atk: 0.01f, dec: 0.20f, sus: 0f, rel: 0.15f,
                    vol: 0.40f * volumeScale);
                break;

            case GameClip.NodeLeave:
                // Seno más grave: contraste direccional con NodeEnter
                Next().Play(600f, SynthSFX.SynthType.Sine,
                    atk: 0.01f, dec: 0.18f, sus: 0f, rel: 0.12f,
                    vol: 0.35f * volumeScale);
                break;

            // ── MOVIMIENTOS PUSH (FM índice 1.5) ─────────────────────────
            // Carrier = modulador → timbre de campana metálica
            // Tres frecuencias = tres registros de intensidad

            case GameClip.MovePushHigh:
                Next().Play(1047f, SynthSFX.SynthType.FM,
                    atk: 0.005f, dec: 0.35f, sus: 0f, rel: 0.30f,
                    vol: 0.55f * volumeScale,
                    fmFreq: 1047f, fmIdx: 1.5f);
                break;

            case GameClip.MovePushMid:
                Next().Play(523f, SynthSFX.SynthType.FM,
                    atk: 0.005f, dec: 0.35f, sus: 0f, rel: 0.30f,
                    vol: 0.50f * volumeScale,
                    fmFreq: 523f, fmIdx: 1.5f);
                break;

            case GameClip.MovePushLow:
                Next().Play(261f, SynthSFX.SynthType.FM,
                    atk: 0.005f, dec: 0.40f, sus: 0f, rel: 0.35f,
                    vol: 0.50f * volumeScale,
                    fmFreq: 261f, fmIdx: 1.5f);
                break;

            // ── MOVIMIENTOS PULL (FM índice 2.0, modulador a mitad) ───────
            // Modulador en ratio 1:2 → espectro más rico que Push
            // Perceptualmente: pull "tira" más que push, índice mayor = más tensión

            case GameClip.MovePullHigh:
                Next().Play(988f, SynthSFX.SynthType.FM,
                    atk: 0.005f, dec: 0.30f, sus: 0f, rel: 0.25f,
                    vol: 0.55f * volumeScale,
                    fmFreq: 494f, fmIdx: 2.0f);
                break;

            case GameClip.MovePullMid:
                Next().Play(494f, SynthSFX.SynthType.FM,
                    atk: 0.005f, dec: 0.30f, sus: 0f, rel: 0.25f,
                    vol: 0.50f * volumeScale,
                    fmFreq: 247f, fmIdx: 2.0f);
                break;

            case GameClip.MovePullLow:
                Next().Play(247f, SynthSFX.SynthType.FM,
                    atk: 0.005f, dec: 0.35f, sus: 0f, rel: 0.30f,
                    vol: 0.50f * volumeScale,
                    fmFreq: 123f, fmIdx: 2.0f);
                break;

            // ── ARC MOVE (síntesis aditiva) ───────────────────────────────
            // 5 armónicos con decaimiento natural → timbre cálido, fluido
            // Aditiva en lugar de FM porque el movimiento en arco es suave

            case GameClip.ArcMoveHigh:
                Next().Play(880f, SynthSFX.SynthType.Additive,
                    atk: 0.01f, dec: 0.30f, sus: 0f, rel: 0.30f,
                    vol: 0.40f * volumeScale);
                break;

            case GameClip.ArcMoveMid:
                Next().Play(440f, SynthSFX.SynthType.Additive,
                    atk: 0.01f, dec: 0.30f, sus: 0f, rel: 0.30f,
                    vol: 0.38f * volumeScale);
                break;

            case GameClip.ArcMoveLow:
                Next().Play(220f, SynthSFX.SynthType.Additive,
                    atk: 0.01f, dec: 0.35f, sus: 0f, rel: 0.35f,
                    vol: 0.35f * volumeScale);
                break;

            // ── ROTACIÓN (FM + pitch sweep) ───────────────────────────────
            // Frecuencia desciende 800→250Hz en 0.5s
            // El glissando descendente imita físicamente el giro del nodo

            case GameClip.NodeRotate90:
                Next().Play(800f, SynthSFX.SynthType.FM,
                    atk: 0.005f, dec: 0.50f, sus: 0f, rel: 0.30f,
                    vol: 0.55f * volumeScale,
                    fmFreq: 400f, fmIdx: 2.5f,
                    sweep: true, sweepEnd: 250f, sweepDur: 0.5f);
                break;

            // ── MOVIMIENTO INVÁLIDO (diente de sierra) ────────────────────
            // Saw es la forma de onda más agresiva de las básicas
            // Frecuencia grave (120Hz) + timbre áspero = error claro sin saturar

            case GameClip.InvalidRotate:
                Next().Play(120f, SynthSFX.SynthType.Saw,
                    atk: 0.005f, dec: 0.20f, sus: 0f, rel: 0.08f,
                    vol: 0.45f * volumeScale);
                break;

            // ── VICTORIA (arpeggio aditivo) ───────────────────────────────
            case GameClip.WinBoard:
                StartCoroutine(PlayWinArpeggio(volumeScale));
                break;

            // ── INICIO DE PARTIDA ─────────────────────────────────────────
            case GameClip.GameStart:
                StartCoroutine(PlayStartArpeggio(volumeScale));
                break;

            // ── FIN DE JUEGO (FM descendente) ─────────────────────────────
            case GameClip.GameEnd:
                Next().Play(440f, SynthSFX.SynthType.FM,
                    atk: 0.01f, dec: 0.60f, sus: 0f, rel: 0.50f,
                    vol: 0.35f * volumeScale,
                    fmFreq: 220f, fmIdx: 1.0f,
                    sweep: true, sweepEnd: 220f, sweepDur: 0.8f);
                break;

            // ── MENÚ (onda cuadrada) ──────────────────────────────────────
            // Cuadrada = retro, inmediata, claramente diferenciada de SFX de juego
            case GameClip.MenuSelect:
                Next().Play(660f, SynthSFX.SynthType.Square,
                    atk: 0.005f, dec: 0.08f, sus: 0f, rel: 0.05f,
                    vol: 0.20f * volumeScale);
                break;

            // ── NIVEL DISPONIBLE (wavetable) ──────────────────────────────
            // Wavetable basada en aditiva suave → cálido, invita al jugador
            case GameClip.LevelEnable:
                Next().Play(440f, SynthSFX.SynthType.Wavetable,
                    atk: 0.02f, dec: 0.30f, sus: 0.20f, rel: 0.40f,
                    vol: 0.25f * volumeScale);
                break;
        }
    }

    // ── Arpeggios ─────────────────────────────────────────────────────────

    private IEnumerator PlayWinArpeggio(float vol)
    {
        // C5 → E5 → G5 → C6: resolución armónica del acorde de tónica
        // Cada nota tiene decay más largo para que se superpongan levemente
        float[] freqs  = { 523f, 659f, 784f, 1047f };
        float[] decays = { 0.5f, 0.5f, 0.6f,  0.9f };

        for (int i = 0; i < freqs.Length; i++)
        {
            SynthSFX sfx = Next();
            sfx.numberOfHarmonics    = 5;
            sfx.harmonicAmplitudes   = new float[]
                { 1f, 0.6f, 0.4f, 0.2f, 0.1f, 0f, 0f, 0f, 0f, 0f };
            sfx.Play(freqs[i], SynthSFX.SynthType.Additive,
                atk: 0.01f, dec: decays[i], sus: 0.1f, rel: 0.6f,
                vol: 0.55f * vol);
            yield return new WaitForSeconds(0.13f);
        }
    }

    private IEnumerator PlayStartArpeggio(float vol)
    {
        // C4 → G4 → C5: intervalo de quinta + octava, sensación de apertura
        float[] freqs = { 261f, 392f, 523f };
        foreach (float f in freqs)
        {
            Next().Play(f, SynthSFX.SynthType.Additive,
                atk: 0.01f, dec: 0.25f, sus: 0f, rel: 0.20f,
                vol: 0.35f * vol);
            yield return new WaitForSeconds(0.12f);
        }
    }
}
