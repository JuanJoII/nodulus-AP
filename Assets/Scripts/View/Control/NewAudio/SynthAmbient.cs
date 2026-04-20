using System.Collections;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════════════
// SynthAmbient — Música de fondo procedural para Nodulus.
//
// Combina tres capas:
//   1. Acordes lentos vía PolifoniaV2 (síntesis aditiva)
//   2. LFO de amplitud (tremolo) que hace "respirar" el sonido
//   3. Percusión suave vía DrumMachine (patrón waltz a BPM bajo)
//
// El LFO se implementa aquí directamente porque OSC.cs no lo expone
// como módulo independiente. Modula la amplitud de la salida de audio
// entre (1 - depth) y 1.0, creando un pulso lento sin distorsionar
// la forma de onda subyacente.
// ═══════════════════════════════════════════════════════════════════════════════

[RequireComponent(typeof(AudioSource))]
public class SynthAmbient : MonoBehaviour
{
    [Header("Referencias")]
    public PolifoniaV2  chords;
    public DrumMachine   drums;

    [Header("BPM y compás")]
    [Range(30f, 80f)] public float bpm        = 45f;
    public int                     bars        = 999; // prácticamente infinito

    [Header("Volumen general")]
    [Range(0f, 1f)] public float masterVolume = 0.5f;

    [Header("LFO — Tremolo")]
    [Range(0.1f, 4f)] public float lfoFrequency = 0.8f;  // Hz — velocidad del pulso
    [Range(0f,   1f)] public float lfoDepth     = 0.3f;  // profundidad del tremolo

    [Header("Activar/desactivar capas")]
    public bool playChords = true;
    public bool playDrums  = true;

    // ── Estado interno ────────────────────────────────────────────────────────

    private float sampleRate;
    private float lfoPhase   = 0f;
    private bool  isRunning  = false;

    // Progresión armónica: Cmaj7 → Am7 → Fmaj7 → G7
    // Cada string[] es un acorde (notas simultáneas)
    private readonly string[][] progression = new string[][]
    {
        new[] { "C",  "E",  "G",  "B"  },   // Cmaj7
        new[] { "A",  "C",  "E",  "G"  },   // Am7
        new[] { "F",  "A",  "C",  "E"  },   // Fmaj7
        new[] { "G",  "B",  "D",  "F"  },   // G7
    };

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        sampleRate = AudioSettings.outputSampleRate;

        // AudioSource propio para el LFO — aplica sobre la salida combinada
        var aud         = GetComponent<AudioSource>();
        aud.clip        = AudioClip.Create("ambient_lfo", (int)sampleRate, 1, (int)sampleRate, false);
        aud.loop        = true;
        aud.playOnAwake = false;
        aud.volume      = 1f;
        aud.Play();
    }

    void Start()
    {
        StartAmbient();
    }

    // ── API pública ───────────────────────────────────────────────────────────

    public void StartAmbient()
    {
        if (isRunning) return;
        isRunning = true;
        StartCoroutine(AmbientLoop());
    }

    public void StopAmbient()
    {
        isRunning = false;
        StopAllCoroutines();

        if (chords != null) chords.ReleaseAll();
    }

    // ── Loop principal ────────────────────────────────────────────────────────

    private IEnumerator AmbientLoop()
    {
        float beatDuration  = 60f / bpm;
        float chordDuration = beatDuration * 4f; // cada acorde dura 4 beats

        // Arrancar percusión
        if (playDrums && drums != null)
            StartCoroutine(drums.PlayPattern_Waltz(bpm, bars * 4));

        int chordIndex = 0;

        while (isRunning)
        {
            // ── Tocar acorde actual ───────────────────────────────────────────
            if (playChords && chords != null)
            {
                // Soltar acorde anterior
                chords.ReleaseAll();

                // Presionar acorde nuevo
                string[] chord = progression[chordIndex % progression.Length];
                foreach (string note in chord)
                    chords.NoteOn(note);
            }

            chordIndex++;
            yield return new WaitForSeconds(chordDuration);
        }

        // Limpieza al salir
        if (chords != null) chords.ReleaseAll();
    }

    // ── LFO de amplitud (tremolo) — hilo de audio ─────────────────────────────
    // Modula la amplitud de TODA la salida de este AudioSource.
    // Como está en el mismo GameObject que la cámara de audio,
    // actúa como un post-proceso suave sobre la mezcla.
    //
    // Fórmula:
    //   lfoValue ∈ [0, 1]  →  gain ∈ [(1-depth), 1]
    //
    // Con lfoFrequency=0.8 Hz y lfoDepth=0.3:
    //   El volumen oscila entre 0.7 y 1.0 cada ~1.25 segundos
    //   → Efecto de "respiración" suave, casi imperceptible pero presente

    void OnAudioFilterRead(float[] data, int channels)
    {
        float phaseIncrement = 2f * Mathf.PI * lfoFrequency / sampleRate;

        for (int i = 0; i < data.Length; i += channels)
        {
            // LFO: seno normalizado a [0, 1]
            float lfoValue = 0.5f + 0.5f * Mathf.Sin(lfoPhase);
            float gain     = (1f - lfoDepth) + lfoDepth * lfoValue;

            data[i] *= gain * masterVolume;
            if (channels == 2) data[i + 1] *= gain * masterVolume;

            lfoPhase += phaseIncrement;
            if (lfoPhase > 2f * Mathf.PI) lfoPhase -= 2f * Mathf.PI;
        }
    }
}