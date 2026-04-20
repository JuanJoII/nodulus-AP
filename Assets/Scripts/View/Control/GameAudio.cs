using System.Collections;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════════════
// GameAudio — Reemplazo del sistema de audio original de Nodulus.
//
// Mantiene los mismos enums (GameClip, MusicClip) y las mismas firmas
// de Play() para no modificar ningún otro script del juego.
//
// Internamente reemplaza los AudioClips pregrabados por síntesis procedural:
//   • SynthSFX    → sonidos one-shot para eventos de juego y UI
//   • SynthAmbient → música de fondo con LFO + acordes procedurales
//   • Polifonia   → acordes de victoria (WinBoard) y arranque (GameStart)
//
// ═══════════════════════════════════════════════════════════════════════════════

namespace View.Control
{
    public class GameAudio : MonoBehaviour
    {
        private const string MusicStatusKey = "music.status";
        private const string SfxStatusKey   = "sfx.status";

        // ── Referencias a sintetizadores ──────────────────────────────────────
        [Header("Sintetizadores")]
        public SynthSFX    synthSFX;      // Para todos los SFX one-shot
        public SynthAmbient synthAmbient; // Música de fondo procedural
        public Polifonia   polifonia;     // Para WinBoard y GameStart (acordes)

        // ── Control de habilitación ───────────────────────────────────────────
        private bool _musicEnabled = true;
        public bool MusicEnabled
        {
            get { return _musicEnabled; }
            set
            {
                _musicEnabled = value;
                if (synthAmbient != null)
                {
                    if (value) synthAmbient.StartAmbient();
                    else       synthAmbient.StopAmbient();
                }
                PlayerPrefs.SetInt(MusicStatusKey, value ? 0 : 1);
            }
        }

        private bool _sfxEnabled = true;
        public bool SfxEnabled
        {
            get { return _sfxEnabled; }
            set
            {
                _sfxEnabled = value;
                PlayerPrefs.SetInt(SfxStatusKey, value ? 0 : 1);
            }
        }

        // ─────────────────────────────────────────────────────────────────────

        private void Start()
        {
            // Leer preferencias guardadas
            if (!PlayerPrefs.HasKey(MusicStatusKey)) PlayerPrefs.SetInt(MusicStatusKey, 0);
            if (!PlayerPrefs.HasKey(SfxStatusKey))   PlayerPrefs.SetInt(SfxStatusKey,   0);

            MusicEnabled = PlayerPrefs.GetInt(MusicStatusKey) == 0;
            SfxEnabled   = PlayerPrefs.GetInt(SfxStatusKey)   == 0;
        }

        // ── Play SFX ──────────────────────────────────────────────────────────

        /// <summary>
        /// Reproduce el sonido procedural correspondiente al evento de juego dado.
        /// Firma idéntica al GameAudio original para compatibilidad total.
        /// </summary>
        public void Play(GameClip clip, float delay = 0f, float volume = 1f, float startTime = 0f)
        {
            if (!enabled || !SfxEnabled || synthSFX == null) return;

            if (delay > 0f)
            {
                StartCoroutine(PlayDelayed(clip, delay, volume));
                return;
            }

            TriggerSFX(clip, volume);
        }

        private IEnumerator PlayDelayed(GameClip clip, float delay, float volume)
        {
            yield return new WaitForSeconds(delay);
            TriggerSFX(clip, volume);
        }

        // ── Play Music ────────────────────────────────────────────────────────

        /// <summary>
        /// Inicia la música de fondo procedural.
        /// Firma idéntica al GameAudio original — los parámetros de clip/fade
        /// se ignoran porque la música es generada, no un archivo.
        /// </summary>
        public void Play(MusicClip clip, float fadeTime = 0f, float delay = 0f, float volume = 1f, float startTime = 0f)
        {
            if (!enabled || !MusicEnabled || synthAmbient == null) return;
            synthAmbient.masterVolume = volume * 0.5f; // Calibrado para no saturar
            synthAmbient.StartAmbient();
        }

        // ── Mapa de eventos → síntesis ────────────────────────────────────────
        // Cada GameClip tiene una técnica, frecuencia y ADSR elegidos para
        // encajar con la identidad sonora de Nodulus: minimalista, cristalino,
        // espacial. Ver documento técnico para justificación completa.

        private void TriggerSFX(GameClip clip, float volumeScale)
        {
            switch (clip)
            {
                case GameClip.NodeEnter:
                    synthSFX.Play(880f, SynthSFX.SynthType.Sine,
                        atk: 0.01f, dec: 0.20f, sus: 0f, rel: 0.15f,
                        vol: 0.40f * volumeScale); // subido de 0.25 a 0.40
                    break;

                case GameClip.NodeLeave:
                    synthSFX.Play(600f, SynthSFX.SynthType.Sine,
                        atk: 0.01f, dec: 0.18f, sus: 0f, rel: 0.12f,
                        vol: 0.35f * volumeScale); // subido de 0.20 a 0.35
                    break;

                case GameClip.MovePushHigh:
                    synthSFX.Play(1047f, SynthSFX.SynthType.FM,
                        atk: 0.005f, dec: 0.35f, sus: 0f, rel: 0.30f, // decay más largo
                        vol: 0.55f * volumeScale,                       // más volumen
                        fmModFreq: 1047f, fmModIdx: 1.5f);             // índice más alto = más carácter
                    break;

                case GameClip.MovePushMid:
                    synthSFX.Play(523f, SynthSFX.SynthType.FM,
                        atk: 0.005f, dec: 0.35f, sus: 0f, rel: 0.30f,
                        vol: 0.50f * volumeScale,
                        fmModFreq: 523f, fmModIdx: 1.5f);
                    break;

                case GameClip.MovePushLow:
                    synthSFX.Play(261f, SynthSFX.SynthType.FM,
                        atk: 0.005f, dec: 0.40f, sus: 0f, rel: 0.35f,
                        vol: 0.50f * volumeScale,
                        fmModFreq: 261f, fmModIdx: 1.5f);
                    break;

                case GameClip.MovePullHigh:
                    synthSFX.Play(988f, SynthSFX.SynthType.FM,
                        atk: 0.005f, dec: 0.30f, sus: 0f, rel: 0.25f,
                        vol: 0.55f * volumeScale,
                        fmModFreq: 494f, fmModIdx: 2.0f); // modulador a mitad = timbre más interesante
                    break;

                case GameClip.MovePullMid:
                    synthSFX.Play(494f, SynthSFX.SynthType.FM,
                        atk: 0.005f, dec: 0.30f, sus: 0f, rel: 0.25f,
                        vol: 0.50f * volumeScale,
                        fmModFreq: 247f, fmModIdx: 2.0f);
                    break;

                case GameClip.MovePullLow:
                    synthSFX.Play(247f, SynthSFX.SynthType.FM,
                        atk: 0.005f, dec: 0.35f, sus: 0f, rel: 0.30f,
                        vol: 0.50f * volumeScale,
                        fmModFreq: 123f, fmModIdx: 2.0f);
                    break;

                case GameClip.NodeRotate90:
                    synthSFX.Play(800f, SynthSFX.SynthType.FM,
                        atk: 0.005f, dec: 0.50f, sus: 0f, rel: 0.30f,
                        vol: 0.55f * volumeScale,
                        fmModFreq: 400f, fmModIdx: 2.5f,
                        pitchSweep: true, sweepEnd: 250f, sweepDur: 0.5f); // sweep más dramático
                    break;

                case GameClip.InvalidRotate:
                    synthSFX.Play(120f, SynthSFX.SynthType.Saw,
                        atk: 0.005f, dec: 0.20f, sus: 0f, rel: 0.08f,
                        vol: 0.45f * volumeScale); // más audible
                    break;

                case GameClip.WinBoard:
                    StartCoroutine(PlayWinArpeggio(volumeScale));
                    break;

                // ── Inicio de partida — Arpeggio ascendente ───────────────────
                case GameClip.GameStart:
                    StartCoroutine(PlayStartArpeggio(volumeScale));
                    break;

                // ── Fin de juego — FM descendente ─────────────────────────────
                case GameClip.GameEnd:
                    synthSFX.Play(440f, SynthSFX.SynthType.FM,
                        atk: 0.01f, dec: 0.60f, sus: 0f, rel: 0.50f,
                        vol: 0.35f * volumeScale,
                        fmModFreq: 220f, fmModIdx: 1.0f,
                        pitchSweep: true, sweepEnd: 220f, sweepDur: 0.8f);
                    break;

                // ── Menú — Onda cuadrada ───────────────────────────────────────
                // Retro, muy corto, claramente diferenciado de SFX de juego.
                case GameClip.MenuSelect:
                    synthSFX.Play(660f, SynthSFX.SynthType.Square,
                        atk: 0.005f, dec: 0.08f, sus: 0f, rel: 0.05f,
                        vol: 0.20f * volumeScale);
                    break;

                // ── Nivel habilitado — Seno con wavetable ─────────────────────
                // Cálido, invita al jugador. Wavetable basada en aditiva suave.
                case GameClip.LevelEnable:
                    synthSFX.numberOfHarmonics = 4;
                    synthSFX.Play(440f, SynthSFX.SynthType.Wavetable,
                        atk: 0.02f, dec: 0.30f, sus: 0.20f, rel: 0.40f,
                        vol: 0.25f * volumeScale);
                    break;
            }
        }

        // ── Arpeggio de victoria ──────────────────────────────────────────────
        // C → E → G → C (octava superior) con 80ms entre notas

        private IEnumerator PlayWinArpeggio(float vol)
        {
            // Arpeggio más lento y con sustain — da tiempo a escuchar cada nota
            float[] freqs   = { 523f, 659f, 784f, 1047f };
            float[] decays  = { 0.5f, 0.5f, 0.6f, 0.9f  };

            for (int i = 0; i < freqs.Length; i++)
            {
                synthSFX.harmonicAmplitudes = new float[]
                    { 1f, 0.6f, 0.4f, 0.2f, 0.1f, 0f, 0f, 0f, 0f, 0f };
                synthSFX.numberOfHarmonics = 5;
                synthSFX.Play(freqs[i], SynthSFX.SynthType.Additive,
                    atk: 0.01f, dec: decays[i], sus: 0.1f, rel: 0.6f,
                    vol: 0.55f * vol);
                yield return new WaitForSeconds(0.13f); // más lento que antes
            }
        }

        // ── Arpeggio de inicio ────────────────────────────────────────────────
        // C → G → C2 ascendente, apertura energética

        private IEnumerator PlayStartArpeggio(float vol)
        {
            float[] freqs = { 261f, 392f, 523f }; // C4, G4, C5
            foreach (float f in freqs)
            {
                synthSFX.Play(f, SynthSFX.SynthType.Additive,
                    atk: 0.01f, dec: 0.25f, sus: 0f, rel: 0.20f,
                    vol: 0.35f * vol);
                yield return new WaitForSeconds(0.12f);
            }
        }
    }

    // ── Enums — idénticos al original ─────────────────────────────────────────

    public enum GameClip
    {
        GameStart,
        WinBoard,
        NodeEnter,
        NodeLeave,
        MovePushHigh,
        MovePullHigh,
        MovePullMid,
        MovePushMid,
        MovePullLow,
        MovePushLow,
        ArcMoveHigh,
        NodeRotate90,
        InvalidRotate,
        MenuSelect,
        GameEnd,
        LevelEnable,
        ArcMoveMid,
        ArcMoveLow
    }

    public enum MusicClip
    {
        Ambient01,
        Ambient02
    }
}