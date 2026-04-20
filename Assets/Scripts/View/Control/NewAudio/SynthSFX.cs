using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════════════
// SynthSFX — Sintetizador de sonidos one-shot para eventos del juego.
//
// FIX: Condición de carrera entre Play() (hilo principal) y OnAudioFilterRead
// (hilo de audio) resuelta con el flag volatile _pendingReset.
// Play() nunca toca timeIndex directamente — solo activa el flag.
// El reset real ocurre al inicio del siguiente ciclo de audio.
// ═══════════════════════════════════════════════════════════════════════════════

[RequireComponent(typeof(AudioSource))]
public class SynthSFX : MonoBehaviour
{
    public enum SynthType { Sine, Square, Saw, Additive, FM, Wavetable }

    // Parámetros — públicos para debug en Inspector
    public float frequency     = 440f;
    public SynthType synthType = SynthType.Sine;

    public float attack  = 0.01f;
    public float decay   = 0.25f;
    public float sustain = 0f;
    public float release = 0.2f;
    public float volume  = 0.4f;

    public float fmModFrequency = 220f;
    public float fmModIndex     = 0.8f;

    public int   numberOfHarmonics    = 5;
    public float[] harmonicAmplitudes = new float[10]
        { 1f, 0.5f, 0.25f, 0.12f, 0.06f, 0f, 0f, 0f, 0f, 0f };

    public bool  usePitchSweep = false;
    public float sweepEndFreq  = 0f;
    public float sweepDuration = 0.4f;

    // ── Estado interno ────────────────────────────────────────────────────────
    private float sampleRate;
    private AudioSource audioSource;

    private int   timeIndex     = 0;
    private bool  isPlaying     = false;
    private bool  noteOffSent   = false;
    private int   noteOffIndex  = 0;
    private float envelopeValue = 0f;

    // volatile: garantiza visibilidad entre hilos sin necesitar lock
    private volatile bool _pendingReset = false;

    private float[] wavetable;
    private const int wavetableSize = 2048;

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        sampleRate  = AudioSettings.outputSampleRate;
        audioSource = GetComponent<AudioSource>();
        audioSource.enabled     = true;
        audioSource.clip        = AudioClip.Create("sfx_silence", (int)sampleRate, 1, (int)sampleRate, false);
        audioSource.loop        = true;
        audioSource.playOnAwake = false;
        audioSource.volume      = 1f;
        audioSource.Play();
    }

    // ── API pública ───────────────────────────────────────────────────────────

    public void Play(
        float freq,
        SynthType type,
        float atk, float dec, float sus, float rel,
        float vol       = 0.4f,
        float fmModFreq = 220f,
        float fmModIdx  = 0.8f,
        bool  pitchSweep = false,
        float sweepEnd  = 0f,
        float sweepDur  = 0.4f)
    {
        // Escribir parámetros ANTES de activar el flag
        // para que el hilo de audio los vea completos
        frequency      = freq;
        synthType      = type;
        attack         = atk;
        decay          = dec;
        sustain        = sus;
        release        = rel;
        volume         = vol;
        fmModFrequency = fmModFreq;
        fmModIndex     = fmModIdx;
        usePitchSweep  = pitchSweep;
        sweepEndFreq   = sweepEnd;
        sweepDuration  = sweepDur;

        if (type == SynthType.Wavetable)
            GenerateWavetable();

        // Activar flag — el reset real ocurre en OnAudioFilterRead
        isPlaying     = true;
        _pendingReset = true;
    }

    // ── Wavetable ─────────────────────────────────────────────────────────────

    private void GenerateWavetable()
    {
        wavetable = new float[wavetableSize];
        for (int i = 0; i < wavetableSize; i++)
        {
            float sum = 0f, total = 0f;
            for (int h = 0; h < numberOfHarmonics; h++)
            {
                sum   += harmonicAmplitudes[h] * Mathf.Sin(2f * Mathf.PI * (h + 1) * i / wavetableSize);
                total += harmonicAmplitudes[h];
            }
            wavetable[i] = total > 0f ? sum / total : 0f;
        }
    }

    private float SampleWavetable(float freq, int t)
    {
        if (wavetable == null) return 0f;
        int idx = Mathf.RoundToInt((t * freq / sampleRate) * wavetableSize) % wavetableSize;
        return wavetable[Mathf.Abs(idx) % wavetableSize];
    }

    // ── Formas de onda ────────────────────────────────────────────────────────

    private float Sine(float freq, int t)
        => Mathf.Sin(2f * Mathf.PI * freq * t / sampleRate);

    private float Square(float freq, int t)
        => Mathf.Sign(Mathf.Sin(2f * Mathf.PI * freq * t / sampleRate));

    private float Saw(float freq, int t)
    {
        float T = sampleRate / freq;
        return Mathf.Lerp(1f, -1f, (t % T) / T);
    }

    private float Additive(float baseFreq, int t)
    {
        float sample = 0f, totalAmp = 0f;
        for (int h = 0; h < numberOfHarmonics; h++)
        {
            float hf = baseFreq * (h + 1);
            if (hf >= sampleRate * 0.5f) break;
            sample   += harmonicAmplitudes[h] * Mathf.Sin(2f * Mathf.PI * hf * t / sampleRate);
            totalAmp += harmonicAmplitudes[h];
        }
        return totalAmp > 0f ? sample / totalAmp : 0f;
    }

    private float FM(float carrier, int t)
    {
        float cp = 2f * Mathf.PI * carrier        * t / sampleRate;
        float mp = 2f * Mathf.PI * fmModFrequency * t / sampleRate;
        return Mathf.Sin(cp + fmModIndex * Mathf.Sin(mp));
    }

    private float GetCurrentFrequency()
    {
        if (!usePitchSweep) return frequency;
        float t     = timeIndex / sampleRate;
        float alpha = Mathf.Clamp01(t / sweepDuration);
        return Mathf.Lerp(frequency, sweepEndFreq, alpha);
    }

    // ── ADSR one-shot ─────────────────────────────────────────────────────────

    private float ADSR()
    {
        float attackSamples  = attack  * sampleRate;
        float decaySamples   = decay   * sampleRate;
        float releaseSamples = release * sampleRate;
        float envelope       = 0f;

        if (!noteOffSent)
        {
            if (timeIndex < attackSamples)
            {
                envelope = timeIndex / attackSamples;
            }
            else if (timeIndex < attackSamples + decaySamples)
            {
                float progress = (timeIndex - attackSamples) / decaySamples;
                envelope = Mathf.Lerp(1f, sustain, progress);
            }
            else
            {
                envelope = sustain;
                if (sustain <= 0.001f)
                {
                    noteOffSent   = true;
                    noteOffIndex  = timeIndex;
                    envelopeValue = 0f;
                }
            }
        }
        else
        {
            int elapsed = timeIndex - noteOffIndex;
            if (elapsed < releaseSamples)
            {
                envelope = Mathf.Lerp(envelopeValue, 0f, elapsed / releaseSamples);
            }
            else
            {
                envelope  = 0f;
                isPlaying = false;
            }
        }

        envelopeValue = envelope;
        return envelope;
    }

    // ── OnAudioFilterRead (hilo de audio) ─────────────────────────────────────

    void OnAudioFilterRead(float[] data, int channels)
    {
        // Reset thread-safe: ocurre aquí, en el hilo de audio
        if (_pendingReset)
        {
            timeIndex     = 0;
            noteOffSent   = false;
            noteOffIndex  = 0;
            envelopeValue = 0f;
            _pendingReset = false;
        }

        for (int i = 0; i < data.Length; i += channels)
        {
            float sample = 0f;

            if (isPlaying)
            {
                float freq = GetCurrentFrequency();
                float raw  = 0f;

                switch (synthType)
                {
                    case SynthType.Sine:      raw = Sine           (freq, timeIndex); break;
                    case SynthType.Square:    raw = Square         (freq, timeIndex); break;
                    case SynthType.Saw:       raw = Saw            (freq, timeIndex); break;
                    case SynthType.Additive:  raw = Additive       (freq, timeIndex); break;
                    case SynthType.FM:        raw = FM             (freq, timeIndex); break;
                    case SynthType.Wavetable: raw = SampleWavetable(freq, timeIndex); break;
                }

                sample = raw * ADSR() * volume;
                timeIndex++;
            }

            data[i] = sample;
            if (channels == 2) data[i + 1] = sample;
        }
    }
}