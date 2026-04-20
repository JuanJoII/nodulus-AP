using System.Collections.Generic;
using UnityEngine;

public class Polifonia : MonoBehaviour
{
 
    [Header("Octava de la melodía (recomendado: 4 o 5)")]
    public int octave = 4;

    [Header("Timbre de la melodía")]
    public int   waveType          = 4;
    public int   numberOfHarmonics = 5;
    public float attack             = 0.02f;
    public float decay              = 0.1f;
    public float sustain            = 0.8f;
    public float release            = 0.15f;
    public float volume             = 0.6f;
    public float[] harmonicAmplitudes = new float[10]
        { 1f, 0.5f, 0.3f, 0.15f, 0.07f, 0f, 0f, 0f, 0f, 0f };

    private List<OSC>              oscPool           = new List<OSC>();
    private Dictionary<string,OSC> activeOscillators = new Dictionary<string,OSC>();

    void Start()
    {
        foreach (Transform child in transform)
        {
            OSC osc = child.GetComponent<OSC>();
            if (osc != null)
            {
                osc.isNoteOn = false;
                oscPool.Add(osc);
            }
        }
    }

    private OSC GetFreeOsc()
    {
        foreach (OSC osc in oscPool)
            if (!activeOscillators.ContainsValue(osc))
                return osc;
        return null;
    }

    // ── NoteOn / NoteOff ──────────────────────────────────────────────────────

    public void NoteOn(string note)
    {
        if (activeOscillators.ContainsKey(note)) return;

        OSC osc = GetFreeOsc();
        if (osc == null) return;

        activeOscillators[note] = osc;
        ApplySettings(osc);
        osc.f = GetFrequency(note);

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

        activeOscillators.Remove(note);
    }

    public void ReleaseAll()
    {
        foreach (var kvp in activeOscillators)
        {
            kvp.Value.noteOffSample = kvp.Value.TimeIndex;
            kvp.Value.isNoteOn      = false;
        }
        activeOscillators.Clear();
    }

    // ── FIX PRESETS: propaga el timbre actual a TODOS los OSCs del pool ───────
    // Llama esto desde UIManager.ChangePreset() después de escribir los campos.
    // Sin esto, los OSCs del pool conservan los valores del preset anterior
    // grabados en sus propios campos, y el cambio no se escucha hasta el
    // próximo NoteOn — que en canciones rápidas puede no llegar nunca.
    public void ApplyToAll()
    {
        foreach (OSC osc in oscPool)
            ApplySettings(osc);
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