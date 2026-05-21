using UnityEngine;

public class AudioMgr : MonoBehaviour
{
    public static AudioMgr Instance;

    [SerializeField] private AudioClip _cutSFX;
    [SerializeField] private AudioClip _failedCutSFX;
    [SerializeField] private AudioClip _beepSFX;
    [SerializeField] private AudioClip _goSFX;

    [SerializeField] private AudioClip _mainMusic;

    [SerializeField, Range(0f, 1f)] private float _musicVolume = 0.5f;

    private AudioSource _musicSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // dedicated AudioSource for looping main music
        _musicSource = GetComponent<AudioSource>();
        if (_musicSource == null)
        {
            _musicSource = gameObject.AddComponent<AudioSource>();
        }
        _musicSource.playOnAwake = false;
        _musicSource.loop = true;
        _musicSource.volume = _musicVolume;
        _musicSource.spatialBlend = 0f; // non-spatial for music
    }

    private void Start()
    {
        PlayMainMusic();  
    }

    public void PlaySFX(string sfxName)
    {
        AudioClip clip = null;
        switch (sfxName)
        {
            case "cut":
                clip = _cutSFX;
                break;
            case "failedCut":
                clip = _failedCutSFX;
                break;
            case "beep":
                clip = _beepSFX;
                break;
            case "go":
                clip = _goSFX;
                break;
            default:
                Debug.LogWarning($"AudioMgr: Unknown SFX name '{sfxName}'");
                return;
        }
        if (clip != null)
        {
            var cam = Camera.main;
            Vector3 pos = cam != null ? cam.transform.position : Vector3.zero;
            AudioSource.PlayClipAtPoint(clip, pos);
        }
    }

    public void PlayMainMusic()
    {
        if (_mainMusic == null)
        {
            Debug.LogWarning("AudioMgr: Main music clip not set");
            return;
        }

        if (_musicSource.isPlaying && _musicSource.clip == _mainMusic)
        {
            return;
        }

        _musicSource.clip = _mainMusic;
        _musicSource.loop = true;
        _musicSource.volume = _musicVolume;
        _musicSource.pitch = 1f; 
        _musicSource.Play();
    }

    public void StopMainMusic()
    {
        if (_musicSource.isPlaying)
        {
            _musicSource.Stop();
        }
    }

    public void SpeedUpMusic(float pitchIncrease = 0.1f)
    {
        _musicSource.pitch += pitchIncrease;
    }
}
