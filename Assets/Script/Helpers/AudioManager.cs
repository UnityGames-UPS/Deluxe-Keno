using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Optimized Audio Manager with pooling and volume control
/// Supports separate controls for SFX and Background Music
/// FIXED: Proper cleanup to prevent MissingReferenceException
/// NEW: Added scroll sound effects (button click and drag)
/// </summary>
public class AudioManager : MonoBehaviour
{
    #region Singleton
    private static AudioManager _instance;
    public static AudioManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("AudioManager");
                _instance = go.AddComponent<AudioManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    #endregion

    #region Audio Clips
    [Header("Sound Effects")]
    [SerializeField] private AudioClip _ballFallingSound;
    [SerializeField] private AudioClip _errorSound;
    [SerializeField] private AudioClip _gameStartSound;
    [SerializeField] private AudioClip _normalSpeedClickSound;
    [SerializeField] private AudioClip _turboSpeedClickSound;
    [SerializeField] private AudioClip _instantSpeedClickSound;
    [SerializeField] private AudioClip _buttonClickSound;
    [SerializeField] private AudioClip _playButtonSound;
    [SerializeField] private AudioClip _winBallSound;
    [SerializeField] private AudioClip _winPopupSound;

    [Header("Scroll Sounds")]
    [SerializeField] private AudioClip _scrollButtonSound;  // NEW: Plays once when using left/right buttons
    [SerializeField] private AudioClip _scrollDragSound;    // NEW: Plays continuously while dragging

    [Header("Background Music")]
    [SerializeField] private AudioClip _mainBGMusic;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource _bgMusicSource;
    [SerializeField] private AudioSource _sfxSource;
    #endregion

    #region Settings
    private const string PREF_SFX_ENABLED = "SFX_Enabled";
    private const string PREF_MUSIC_ENABLED = "Music_Enabled";
    private const string PREF_SFX_VOLUME = "SFX_Volume";
    private const string PREF_MUSIC_VOLUME = "Music_Volume";

    private bool _sfxEnabled = true;
    private bool _musicEnabled = true;
    private float _sfxVolume = 1f;
    private float _musicVolume = 0.5f;

    public bool SFXEnabled => _sfxEnabled;
    public bool MusicEnabled => _musicEnabled;
    public float SFXVolume => _sfxVolume;
    public float MusicVolume => _musicVolume;
    #endregion

    #region Object Pool
    private List<AudioSource> _sfxPool = new List<AudioSource>(10);
    private const int INITIAL_POOL_SIZE = 5;
    #endregion

    // FIXED: Track if we're being destroyed
    private bool _isBeingDestroyed = false;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeAudioSources();
        LoadSettings();
        InitializePool();
    }

    private void InitializeAudioSources()
    {
        if (_bgMusicSource == null)
        {
            _bgMusicSource = gameObject.AddComponent<AudioSource>();
            _bgMusicSource.loop = true;
            _bgMusicSource.playOnAwake = false;
        }

        if (_sfxSource == null)
        {
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
        }
    }

    private void InitializePool()
    {
        for (int i = 0; i < INITIAL_POOL_SIZE; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            _sfxPool.Add(source);
        }
    }

    private void LoadSettings()
    {
        _sfxEnabled = PlayerPrefs.GetInt(PREF_SFX_ENABLED, 1) == 1;
        _musicEnabled = PlayerPrefs.GetInt(PREF_MUSIC_ENABLED, 1) == 1;
        _sfxVolume = PlayerPrefs.GetFloat(PREF_SFX_VOLUME, 1f);
        _musicVolume = PlayerPrefs.GetFloat(PREF_MUSIC_VOLUME, 0.5f);

        ApplySettings();
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetInt(PREF_SFX_ENABLED, _sfxEnabled ? 1 : 0);
        PlayerPrefs.SetInt(PREF_MUSIC_ENABLED, _musicEnabled ? 1 : 0);
        PlayerPrefs.SetFloat(PREF_SFX_VOLUME, _sfxVolume);
        PlayerPrefs.SetFloat(PREF_MUSIC_VOLUME, _musicVolume);
        PlayerPrefs.Save();
    }

    private void ApplySettings()
    {
        if (_bgMusicSource != null)
            _bgMusicSource.volume = _musicVolume;

        if (_sfxSource != null)
            _sfxSource.volume = _sfxVolume;

        if (_musicEnabled && _mainBGMusic != null && _bgMusicSource != null && !_bgMusicSource.isPlaying)
            PlayBackgroundMusic();
        else if (!_musicEnabled)
            StopBackgroundMusic();
    }

    #region Public Audio Controls
    public void PlayBackgroundMusic()
    {
        if (_isBeingDestroyed || _bgMusicSource == null || !_musicEnabled || _mainBGMusic == null)
            return;

        _bgMusicSource.clip = _mainBGMusic;
        _bgMusicSource.volume = _musicVolume;
        _bgMusicSource.Play();
    }

    public void StopBackgroundMusic()
    {
        // FIXED: Check if source still exists before trying to stop
        if (_isBeingDestroyed || _bgMusicSource == null)
            return;

        try
        {
            if (_bgMusicSource != null)
                _bgMusicSource.Stop();
        }
        catch (MissingReferenceException)
        {
            // AudioSource was already destroyed, ignore
        }
    }

    public void PlayBallFalling() => PlaySFX(_ballFallingSound);
    public void PlayError() => PlaySFX(_errorSound);
    public void PlayGameStart() => PlaySFX(_gameStartSound);
    public void PlayNormalSpeedClick() => PlaySFX(_normalSpeedClickSound);
    public void PlayTurboSpeedClick() => PlaySFX(_turboSpeedClickSound);
    public void PlayInstantSpeedClick() => PlaySFX(_instantSpeedClickSound);
    public void PlayButtonClick() => PlaySFX(_buttonClickSound);
    public void PlayPlayButton() => PlaySFX(_playButtonSound);
    public void PlayWinBall() => PlaySFX(_winBallSound);
    public void PlayWinPopup() => PlaySFX(_winPopupSound);

    // NEW: Scroll sound methods
    public void PlayScrollButton() => PlaySFX(_scrollButtonSound);
    public void PlayScrollDrag() => PlaySFX(_scrollDragSound);

    private void PlaySFX(AudioClip clip)
    {
        if (_isBeingDestroyed || !_sfxEnabled || clip == null)
            return;

        AudioSource source = GetPooledAudioSource();
        if (source != null)
        {
            source.clip = clip;
            source.volume = _sfxVolume;
            source.Play();
        }
    }

    private AudioSource GetPooledAudioSource()
    {
        if (_isBeingDestroyed)
            return null;

        // Find available source
        foreach (AudioSource source in _sfxPool)
        {
            if (source != null && !source.isPlaying)
                return source;
        }

        // Create new source if pool exhausted
        if (gameObject != null)
        {
            AudioSource newSource = gameObject.AddComponent<AudioSource>();
            newSource.playOnAwake = false;
            _sfxPool.Add(newSource);
            return newSource;
        }

        return null;
    }
    #endregion

    #region Settings Controls
    public void ToggleSFX()
    {
        _sfxEnabled = !_sfxEnabled;
        SaveSettings();
        GameEvents.TriggerAudioSettingsChanged();
    }

    public void ToggleMusic()
    {
        _musicEnabled = !_musicEnabled;
        ApplySettings();
        SaveSettings();
        GameEvents.TriggerAudioSettingsChanged();
    }

    public void SetSFXVolume(float volume)
    {
        _sfxVolume = Mathf.Clamp01(volume);
        if (_sfxSource != null)
            _sfxSource.volume = _sfxVolume;
        SaveSettings();
    }

    public void SetMusicVolume(float volume)
    {
        _musicVolume = Mathf.Clamp01(volume);
        if (_bgMusicSource != null)
            _bgMusicSource.volume = _musicVolume;
        SaveSettings();
    }
    #endregion

    #region Cleanup
    private void OnDestroy()
    {
        // FIXED: Mark as being destroyed first
        _isBeingDestroyed = true;

        if (_instance == this)
        {
            // Stop music safely
            StopBackgroundMusic();

            // Clean up pool references
            if (_sfxPool != null)
            {
                _sfxPool.Clear();
                _sfxPool = null;
            }

            // Clear references
            _bgMusicSource = null;
            _sfxSource = null;
            _instance = null;
        }
    }

    // FIXED: Also handle when application quits
    private void OnApplicationQuit()
    {
        _isBeingDestroyed = true;
    }
    #endregion
}