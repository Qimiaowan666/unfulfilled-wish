using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Combat SFX")]
    public AudioClip attackClip;
    public AudioClip hitClip;
    public AudioClip blockClip;
    public AudioClip perfectBlockClip;
    public AudioClip deathClip;
    public AudioClip executeClip;

    [Header("Movement SFX")]
    public AudioClip footstepClip;
    public AudioClip jumpClip;
    public AudioClip dashClip;

    [Header("UI SFX")]
    public AudioClip checkpointClip;

    [Header("BGM")]
    public AudioClip bgmClip;
    public AudioClip bossBGMClip;
    [Range(0f,1f)] public float bgmVolume = 0.5f;

    [Range(0f,1f)] public float sfxVolume = 0.8f;

    AudioSource source;
    AudioSource bgmSource;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;
        bgmSource.volume = bgmVolume;
    }

    void Start()
    {
        PlayBGM(bgmClip);
    }

    public void PlayBGM(AudioClip clip)
    {
        if (clip == null) return;
        if (bgmSource.clip == clip && bgmSource.isPlaying) return;
        bgmSource.clip = clip;
        bgmSource.volume = bgmVolume;
        bgmSource.Play();
    }

    public void PlayBossBGM() => PlayBGM(bossBGMClip);
    public void StopBGM() => bgmSource.Stop();

    public void Play(AudioClip clip)
    {
        if (clip == null) return;
        source.PlayOneShot(clip, sfxVolume);
    }

    public void PlayAttack()      => Play(attackClip);
    public void PlayHit()         => Play(hitClip);
    public void PlayBlock()       => Play(blockClip);
    public void PlayPerfectBlock()=> Play(perfectBlockClip);
    public void PlayDeath()       => Play(deathClip);
    public void PlayExecute()     => Play(executeClip);
    public void PlayJump()        => Play(jumpClip);
    public void PlayDash()        => Play(dashClip);
    public void PlayCheckpoint()  => Play(checkpointClip);
}
