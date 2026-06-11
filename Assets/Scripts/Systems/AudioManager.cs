using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Attack SFX")]
    public AudioClip attack1WhooshClip;
    public AudioClip attack2WhooshClip;
    public AudioClip attack3WhooshClip;
    public AudioClip attack3ImpactClip;

    [Header("Guard SFX")]
    public AudioClip blockClip;
    public AudioClip perfectBlockClip;
    public AudioClip perfectBlockTailClip;
    public AudioClip counterClip;

    [Header("Hit SFX")]
    public AudioClip hitLightClip;
    public AudioClip hitHeavyClip;

    [Header("Execute SFX")]
    public AudioClip executeDrawClip;
    public AudioClip executeStrikeClip;
    public AudioClip executeTailClip;

    [Header("Movement SFX")]
    public AudioClip footstepClip;
    public AudioClip jumpClip;
    public AudioClip dashClip;
    public AudioClip landClip;

    [Header("Misc SFX")]
    public AudioClip deathClip;

    [Header("World Interaction SFX")]
    public AudioClip checkpointClip;
    public AudioClip keyPickupClip;
    public AudioClip doorOpenClip;
    public AudioClip shopBuyClip;
    public AudioClip shopFailClip;
    public AudioClip uiClickClip;

    [Header("Enemy SFX")]
    public AudioClip enemyAttackClip;
    public AudioClip enemyHitClip;
    public AudioClip enemyDeathClip;

    [Header("Boss SFX")]
    public AudioClip bossAttackClip;
    public AudioClip bossRushClip;
    public AudioClip bossPhaseChangeClip;

    [Header("BGM")]
    public AudioClip menuBGMClip;
    public AudioClip bgmClip;
    public AudioClip bossBGMClip;
    public AudioClip futureBossBGMClip;
    [Range(0f,1f)] public float bgmVolume = 0.5f;

    [Range(0f,1f)] public float sfxVolume = 0.8f;

    AudioSource source;
    AudioSource bgmSource;
    float lastFootstepTime;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        bgmVolume = PlayerPrefs.GetFloat("set_bgm", bgmVolume);   // 读上次设置的音量
        sfxVolume = PlayerPrefs.GetFloat("set_sfx", sfxVolume);

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;
        bgmSource.volume = bgmVolume;
        bgmSource.spatialBlend = 0f;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        PlayDefaultBGMForScene(SceneManager.GetActiveScene().name);
    }

    void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlayDefaultBGMForScene(scene.name);
    }

    void PlayDefaultBGMForScene(string sceneName)
    {
        if (sceneName == "MainMenu")
        {
            PlayBGM(menuBGMClip != null ? menuBGMClip : bgmClip);
            return;
        }

        if (sceneName == "ForsakenShrine")
        {
            PlayBossBGM();
            return;
        }

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

    public void RefreshSceneBGM() => PlayDefaultBGMForScene(SceneManager.GetActiveScene().name);
    public void PlayBossBGM() => PlayBGM(bossBGMClip);
    public void PlayFutureBossBGM() => PlayBGM(futureBossBGMClip);
    public void StopBGM()     => bgmSource.Stop();

    public void SetBGMVolume(float v) { bgmVolume = Mathf.Clamp01(v); if (bgmSource != null) bgmSource.volume = bgmVolume; }
    public void SetSFXVolume(float v) { sfxVolume = Mathf.Clamp01(v); }

    public void Play(AudioClip clip)
    {
        if (clip == null) return;
        source.PlayOneShot(clip, sfxVolume);
    }

    public void PlayAttackWhoosh(int comboStep)
    {
        switch (comboStep)
        {
            case 0: Play(attack1WhooshClip); break;
            case 1: Play(attack2WhooshClip); break;
            case 2: Play(attack3WhooshClip); break;
        }
    }

    // PlayPerfectBlock fires both the clang and the Eastern instrument tail simultaneously
    public void PlayAttack3Impact()  => Play(attack3ImpactClip);
    public void PlayBlock()          => Play(blockClip);
    public void PlayPerfectBlock()   { Play(perfectBlockClip); Play(perfectBlockTailClip); }
    public void PlayCounter()        => Play(counterClip);
    public void PlayHitLight()       => Play(hitLightClip);
    public void PlayHitHeavy()       => Play(hitHeavyClip);
    public void PlayExecuteDraw()    => Play(executeDrawClip);
    // PlayExecuteStrike fires both strike impact and resonance tail simultaneously
    public void PlayExecuteStrike()  { Play(executeStrikeClip); Play(executeTailClip); }
    public void PlayJump()           => Play(jumpClip);
    public void PlayDash()           => Play(dashClip);
    public void PlayLand()           => Play(landClip);
    public void PlayDeath()          => Play(deathClip);
    public void PlayCheckpoint()     => Play(checkpointClip);
    public void PlayKeyPickup()      => Play(keyPickupClip);
    public void PlayDoorOpen()       => Play(doorOpenClip);
    public void PlayShopBuy()        => Play(shopBuyClip);
    public void PlayShopFail()       => Play(shopFailClip);
    public void PlayUIClick()        => Play(uiClickClip);
    public void PlayEnemyAttack()    => Play(enemyAttackClip);
    public void PlayEnemyHit()       => Play(enemyHitClip);
    public void PlayEnemyDeath()     => Play(enemyDeathClip);
    public void PlayBossAttack()     => Play(bossAttackClip);
    public void PlayBossRush()       => Play(bossRushClip);
    public void PlayBossPhaseChange()=> Play(bossPhaseChangeClip);

    public void PlayFootstep()
    {
        if (Time.time - lastFootstepTime < 0.15f) return;
        lastFootstepTime = Time.time;
        Play(footstepClip);
    }
}
