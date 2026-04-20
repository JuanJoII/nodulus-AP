using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════════════
// PolifoniaV2 — Instancia OSCs dinámicamente para acordes.
//
// FIXES:
//  1. Parámetros de timbre propios e independientes del UIManager
//     → Los acordes tienen su propio timbre (más suave que la melodía)
//  2. Octava propia más baja que la melodía (octave = 3 por defecto)
//     → Los acordes suenan en el registro grave, como un acompañamiento real
//  3. Volumen más bajo que la melodía para no taparla
// ═══════════════════════════════════════════════════════════════════════════════

public class PolifoniaV2 : MonoBehaviour
{
    [Header("Prefab OSC")]
    public GameObject OSCprefab;

    [Header("Octava de los acordes (recomendado: 3, una octava bajo la melodía)")]
    public int octave = 3;

    [Header("Timbre del acompañamiento")]
    public int   waveType          = 4;      // 4 = síntesis aditiva
    public int   numberOfHarmonics = 6;
    public float attack             = 0.05f;  // ataque suave para acordes
    public float decay              = 0.2f;
    public float sustain            = 0.7f;
    public float release            = 0.4f;   // release más largo → acordes ligados
    public float volume             = 0.25f;  // más suave que la melodía

    // Amplitudes más suaves que la melodía — los acordes no deben tapar la melodía
    public float[] harmonicAmplitudes = new float[10]
        { 1f, 0.6f, 0.35f, 0.2f, 0.1f, 0.05f, 0f, 0f, 0f, 0f };

    // ── Estado interno ────────────────────────────────────────────────────────

    private Dictionary<string,OSC> activeOscillators = new Dictionary<string,OSC>();

    // ── NoteOn / NoteOff ──────────────────────────────────────────────────────

    public void NoteOn(string note)
    {
        if (activeOscillators.ContainsKey(note)) return;

        GameObject go  = Instantiate(OSCprefab, transform);
        go.name        = "OSC_chord_" + note;
        OSC osc        = go.GetComponent<OSC>();

        if (osc == null) { Destroy(go); return; }

        activeOscillators[note] = osc;
        ApplySettings(osc);
        osc.f = GetFrequency(note);

        StartCoroutine(StartNextFrame(osc));
    }

    private IEnumerator StartNextFrame(OSC osc)
    {
        yield return null;   // esperar un frame para que Unity registre el AudioSource

        osc.TimeIndex     = 0;
        osc.noteOnSample  = 0;
        osc.noteOffSample = 0;
        osc.envelopeValue = 0f;

        if (!osc.Aud.isPlaying) osc.Aud.Play();
        osc.isNoteOn = true;
    }

    public void NoteOff(string note)
    {
        if (!activeOscillators.ContainsKey(note)) return;

        OSC osc = activeOscillators[note];
        osc.noteOffSample = osc.TimeIndex;
        osc.isNoteOn      = false;

        Destroy(osc.gameObject, osc.release + 0.3f);
        activeOscillators.Remove(note);
    }

    public void ReleaseAll()
    {
        foreach (var kvp in activeOscillators)
        {
            kvp.Value.noteOffSample = kvp.Value.TimeIndex;
            kvp.Value.isNoteOn      = false;
            Destroy(kvp.Value.gameObject, kvp.Value.release + 0.3f);
        }
        activeOscillators.Clear();
    }

    // ── Configuración ─────────────────────────────────────────────────────────

    private void ApplySettings(OSC osc)
    {
        osc.waveType          = waveType;
        osc.numberOfHarmonics = numberOfHarmonics;
        osc.attack            = attack;
        osc.decay             = decay;
        osc.sustain           = sustain;
        osc.release           = release;
        osc.Aud.volume        = volume;

        for (int i = 0; i < harmonicAmplitudes.Length; i++)
            osc.harmonicAmplitudes[i] = harmonicAmplitudes[i];

        if (osc.useWavetable) osc.GenerateWavetable();
    }

    private float GetFrequency(string note)
    {
        float mul = Mathf.Pow(2f, octave);
        switch (note.ToUpper())
        {
            case "C":  return 16.3516f * mul;
            case "C#": return 17.3239f * mul;
            case "D":  return 18.3540f * mul;
            case "D#": return 19.4454f * mul;
            case "E":  return 20.6017f * mul;
            case "F":  return 21.8268f * mul;
            case "F#": return 23.1246f * mul;
            case "G":  return 24.4997f * mul;
            case "G#": return 25.9565f * mul;
            case "A":  return 27.5000f * mul;
            case "A#": return 29.1353f * mul;
            case "B":  return 30.8677f * mul;
            case "C2": return 32.7032f * mul;
            default:   return 440f;
        }
    }
}