using UnityEngine;


public class GameAudio : MonoBehaviour
{
    private const string MusicStatusKey = "music.status";
    private const string SfxStatusKey   = "sfx.status";

    [Header("Música de fondo")]
    public SynthAmbient synthAmbient;

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

    private void Start()
    {
        if (!PlayerPrefs.HasKey(MusicStatusKey)) PlayerPrefs.SetInt(MusicStatusKey, 0);
        if (!PlayerPrefs.HasKey(SfxStatusKey))   PlayerPrefs.SetInt(SfxStatusKey,   0);

        MusicEnabled = PlayerPrefs.GetInt(MusicStatusKey) == 0;
        SfxEnabled   = PlayerPrefs.GetInt(SfxStatusKey)   == 0;
    }

    public void Play(GameClip clip, float delay = 0f, float volume = 1f, float startTime = 0f)
    {
        // DIAGNÓSTICO — borra estas líneas después de confirmar
        Debug.Log($"[GameAudio] Play llamado: {clip} | SfxEnabled={SfxEnabled} | Bridge={Audiobridge.Instance != null}");
        
        if (!enabled || !SfxEnabled || Audiobridge.Instance == null) return;
        
        if (delay > 0f)
        {
            StartCoroutine(PlayDelayed(clip, delay, volume));
            return;
        }

        Audiobridge.Instance.Trigger(clip, volume);
    }
    private System.Collections.IEnumerator PlayDelayed(GameClip clip, float delay, float volume)
    {
        yield return new WaitForSeconds(delay);
        Audiobridge.Instance.Trigger(clip, volume);
    }

    public void Play(MusicClip clip, float fadeTime = 0f, float delay = 0f, float volume = 1f, float startTime = 0f)
    {
        if (!enabled || !MusicEnabled || synthAmbient == null) return;
        synthAmbient.masterVolume = volume * 0.5f;
        synthAmbient.StartAmbient();
    }
}

public enum GameClip
{
    GameStart, WinBoard, NodeEnter, NodeLeave,
    MovePushHigh, MovePullHigh, MovePullMid, MovePushMid,
    MovePullLow, MovePushLow, ArcMoveHigh, NodeRotate90,
    InvalidRotate, MenuSelect, GameEnd, LevelEnable,
    ArcMoveMid, ArcMoveLow
}

public enum MusicClip { Ambient01, Ambient02 }
