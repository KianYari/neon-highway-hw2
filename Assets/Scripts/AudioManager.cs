using UnityEngine;

// Music, engine loop and one-shot effects.
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Clips")]
    public AudioClip music;
    public AudioClip engine;
    public AudioClip crash;
    public AudioClip coin;
    public AudioClip fuel;
    public AudioClip lowFuel;
    public AudioClip firework;
    public AudioClip gameOver;
    public AudioClip levelUp;
    public AudioClip click;

    [Range(0f, 1f)] public float musicVolume = 0.45f;
    [Range(0f, 1f)] public float engineVolume = 0.35f;

    AudioSource musicSource;
    AudioSource engineSource;
    AudioSource sfxSource;

    void Awake()
    {
        Instance = this;

        musicSource = CreateSource(music, true, musicVolume);
        engineSource = CreateSource(engine, true, 0f);
        sfxSource = CreateSource(null, false, 1f);
        musicSource.Play();
        engineSource.Play();
    }

    AudioSource CreateSource(AudioClip clip, bool loop, float volume)
    {
        AudioSource s = gameObject.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = loop;
        s.volume = volume;
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        return s;
    }

    void Update()
    {
        GameManager gm = GameManager.Instance;
        bool driving = gm.State == GameState.Playing || gm.State == GameState.Menu;

        float targetVolume = driving ? engineVolume * Mathf.Lerp(0.5f, 1f, gm.Speed01) : 0f;
        engineSource.volume = Mathf.MoveTowards(engineSource.volume, targetVolume, Time.deltaTime * 1.5f);
        engineSource.pitch = Mathf.Lerp(0.7f, 2.1f, gm.Speed01);

        float musicTarget = gm.State == GameState.GameOver ? musicVolume * 0.35f : musicVolume;
        musicSource.volume = Mathf.MoveTowards(musicSource.volume, musicTarget, Time.deltaTime * 0.5f);
    }

    void PlayOne(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return;
        sfxSource.pitch = pitch;
        sfxSource.PlayOneShot(clip, volume);
    }

    public void PlayCrash() => PlayOne(crash, 1f);
    public void PlayCoin() => PlayOne(coin, 0.7f, Random.Range(0.95f, 1.1f));
    public void PlayFuel() => PlayOne(fuel, 0.9f);
    public void PlayLowFuel() => PlayOne(lowFuel, 0.5f);
    public void PlayFirework() => PlayOne(firework, 0.25f, Random.Range(0.8f, 1.2f));
    public void PlayGameOver() => PlayOne(gameOver, 0.9f);
    public void PlayLevelUp() => PlayOne(levelUp, 0.8f);
    public void PlayClick() => PlayOne(click, 0.7f);
}
