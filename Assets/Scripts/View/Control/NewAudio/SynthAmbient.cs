using System.Collections;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════════════
// SynthAmbient v8.2 — Fix de compilación (Stop) y Armonización
// ═══════════════════════════════════════════════════════════════════════════════

[RequireComponent(typeof(AudioSource))]
public class SynthAmbient : MonoBehaviour
{
    [Header("Arpegio (Pool de SynthSFX)")]
    public SynthSFX[] arpPool;

    [Header("Tempo")]
    [Range(40f, 80f)] public float bpm = 60f;

    [Header("Volumen")]
    [Range(0f, 1f)] public float masterVolume = 0.45f;
    [Range(0f, 1f)] public float droneVolume  = 0.18f;
    [Range(0f, 1f)] public float arpVolume    = 0.22f;

    [Header("Glide y Textura")]
    [Range(2f, 8f)] public float glideSeconds = 4.0f;
    [Range(0f, 1f)] public float harmonicVariation = 0.15f;

    [Header("LFO Dual")]
    public float lfo1Freq = 0.03f;
    public float lfo1Depth = 0.25f;

    private struct ChordDef
    {
        public float[] droneFreqs; 
        public float[] arpFreqs;   
        public int bars;       
    }

    private readonly ChordDef[] _chords = new ChordDef[]
    {
        new ChordDef { 
            droneFreqs = new[] { 130.81f, 164.81f, 196.00f, 246.94f },
            arpFreqs   = new[] { 261.63f, 329.63f, 392.00f, 493.88f, 523.25f }, 
            bars = 2
        },
        new ChordDef { 
            droneFreqs = new[] { 110.00f, 130.81f, 164.81f, 196.00f },
            arpFreqs   = new[] { 220.00f, 261.63f, 329.63f, 392.00f, 440.00f },
            bars = 2
        },
        new ChordDef { 
            droneFreqs = new[] { 174.61f, 220.00f, 261.63f, 329.63f },
            arpFreqs   = new[] { 174.61f, 220.00f, 261.63f, 329.63f, 349.23f },
            bars = 2
        },
        new ChordDef { 
            droneFreqs = new[] { 196.00f, 246.94f, 293.66f, 392.00f },
            arpFreqs   = new[] { 196.00f, 246.94f, 293.66f, 392.00f, 261.63f },
            bars = 2
        },
    };

    private readonly int[] _arpPattern = new int[] { 0, 2, 1, 3, 0, 3, 2, 4, 1, 2, 0, 3, 2, 1, 0, 4 };

    private const int NumDrones = 4;
    private float[] _dronePhase = new float[NumDrones];
    private float[] _droneFreqNow = new float[NumDrones];
    private float[] _droneFreqTgt = new float[NumDrones];
    private float _droneEnv = 0f;
    private float _droneEnvTgt = 0f;
    private readonly float[] _droneHarmonics = { 1f, 0.35f, 0.12f };

    private float _sr;
    private float _lfo1Phase = 0f;
    private bool _running = false;
    private volatile int _activeChord = 0;
    private int _arpPoolIdx = 0;

    void Awake()
    {
        _sr = AudioSettings.outputSampleRate;
        for (int i = 0; i < NumDrones; i++) {
            _droneFreqNow[i] = _droneFreqTgt[i] = _chords[0].droneFreqs[i];
        }
        var aud = GetComponent<AudioSource>();
        aud.clip = AudioClip.Create("amb", (int)_sr, 1, (int)_sr, false);
        aud.loop = true;
        aud.spatialBlend = 0f;
        aud.Play();
    }

    public void StartAmbient()
    {
        if (_running) return;
        _running = true;
        _droneEnvTgt = 1f;
        StartCoroutine(ProgressionLoop());
        StartCoroutine(ArpLoop());
    }

    public void StopAmbient()
    {
        _running = false;
        _droneEnvTgt = 0f;
        StopAllCoroutines();
        
        // CORRECCIÓN: Si tu SynthSFX no tiene Stop(), usamos ReleaseAll() 
        // o simplemente lo ignoramos ya que al detener las corrutinas no se dispararán más.
        if(arpPool != null) {
            foreach(var sfx in arpPool) {
                if(sfx != null) {
                    // Si tienes un método para soltar la nota, úsalo aquí. 
                    // Si no, basta con dejar que el sonido muera por su propio release.
                }
            }
        }
    }

    private IEnumerator ProgressionLoop()
    {
        while (_running)
        {
            for (int c = 0; c < _chords.Length; c++)
            {
                if (!_running) yield break;
                _activeChord = c;
                for (int d = 0; d < NumDrones; d++)
                    _droneFreqTgt[d] = _chords[c].droneFreqs[d];

                yield return new WaitForSeconds((60f / bpm) * _chords[c].bars * 4f);
            }
        }
    }

    private IEnumerator ArpLoop()
    {
        float beat = 60f / bpm;
        int step = 0;

        while (_running)
        {
            if (arpPool != null && arpPool.Length > 0)
            {
                var chord = _chords[_activeChord];
                int noteIdx = _arpPattern[step % _arpPattern.Length];
                float freq = chord.arpFreqs[Mathf.Min(noteIdx, chord.arpFreqs.Length - 1)];
                
                SynthSFX sfx = arpPool[_arpPoolIdx % arpPool.Length];
                _arpPoolIdx++;

                if (sfx != null)
                {
                    float var3 = 0.4f + Random.Range(-harmonicVariation, harmonicVariation);
                    sfx.numberOfHarmonics = 4;
                    sfx.harmonicAmplitudes = new float[] { 1f, 0.15f, var3, 0.05f, 0f, 0f, 0f, 0f, 0f, 0f };

                    sfx.Play(freq, SynthSFX.SynthType.Additive,
                        atk: 0.5f, 
                        dec: 1.0f,
                        sus: 0.7f, 
                        rel: 3.5f, 
                        vol: arpVolume * Random.Range(0.85f, 1.0f));
                }
            }
            step++;
            yield return new WaitForSeconds(beat * 1.5f); 
        }
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (_sr == 0) return;

        float glideAlpha = 1f - Mathf.Exp(-5f / (_sr * glideSeconds));
        float envAlpha = 1f - Mathf.Exp(-3f / (_sr * 2f));

        for (int i = 0; i < data.Length; i += channels)
        {
            float droneSample = 0f;
            for (int d = 0; d < NumDrones; d++)
            {
                _droneFreqNow[d] += (_droneFreqTgt[d] - _droneFreqNow[d]) * glideAlpha;
                float s = 0f;
                for (int h = 0; h < _droneHarmonics.Length; h++)
                {
                    s += _droneHarmonics[h] * Mathf.Sin(_dronePhase[d] * (h + 1));
                }
                droneSample += s / NumDrones;
                _dronePhase[d] += 2f * Mathf.PI * _droneFreqNow[d] / _sr;
                if (_dronePhase[d] > 2f * Mathf.PI) _dronePhase[d] -= 2f * Mathf.PI;
            }

            _droneEnv += (_droneEnvTgt - _droneEnv) * envAlpha;
            float gain = (1f - lfo1Depth) + lfo1Depth * (0.5f + 0.5f * Mathf.Sin(_lfo1Phase));
            float finalSample = (data[i] + (droneSample * _droneEnv * droneVolume)) * gain * masterVolume;

            data[i] = finalSample;
            if (channels == 2) data[i + 1] = finalSample;

            _lfo1Phase += 2f * Mathf.PI * lfo1Freq / _sr;
            if (_lfo1Phase > 2f * Mathf.PI) _lfo1Phase -= 2f * Mathf.PI;
        }
    }
}