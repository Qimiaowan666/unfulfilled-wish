using System.Collections;
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
    public AudioClip dashStrikeClip;   // 突进斩技能

    [Header("Guard SFX")]
    public AudioClip blockClip;
    public AudioClip perfectBlockClip;
    public AudioClip perfectBlockTailClip;
    public AudioClip counterClip;
    public AudioClip counterWhiffClip;   // 识破挥空（没接到攻击）的格挡挥击声

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
    public AudioClip healClip;      // 治疗技能
    public AudioClip victoryClip;   // 通关 / 胜利

    [Header("World Interaction SFX")]
    public AudioClip checkpointClip;
    public AudioClip keyPickupClip;
    public AudioClip doorOpenClip;
    public AudioClip shopBuyClip;
    public AudioClip shopFailClip;
    public AudioClip uiClickClip;
    public AudioClip uiOpenClip;    // 打开面板/商店/暂停
    public AudioClip uiEquipClip;   // 装备/卸下
    public AudioClip uiUseClip;     // 使用道具

    [Header("Enemy SFX")]
    public AudioClip enemyAttackClip;
    public AudioClip enemyHitClip;
    public AudioClip enemyDeathClip;

    [Header("Boss SFX")]
    public AudioClip bossAttackClip;
    public AudioClip bossRushClip;
    public AudioClip bossPhaseChangeClip;
    public AudioClip bossRoarClip;     // 转阶段吼叫（空则用 phaseChange 兜底）
    public AudioClip bossChargeClip;   // 转阶段蓄力充能
    public AudioClip bossExplodeClip;  // 转阶段爆开
    public AudioClip bossRageRoarClip; // 爆开后强化登场的更响亮吼叫
    public AudioClip bossDefeatClip;   // 击破 boss 重音/sting
    public AudioClip bossNameStampClip; // 登场名牌逐字砸出的重音

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

    void PlayDefaultBGMForScene(string sceneName, bool restart = false)
    {
        if (sceneName == "MainMenu")
        {
            PlayBGM(menuBGMClip != null ? menuBGMClip : bgmClip, restart);
            return;
        }

        // ForsakenShrine 进场放区域曲(area1)；boss 曲只在吼叫开打那刻由 BossIntroTrigger.StartCombat 放。
        PlayBGM(bgmClip, restart);
    }

    // restart=true 时即使同一首也从头重播（读档用）；默认 false 保持过场连续，不重启同曲。
    public void PlayBGM(AudioClip clip, bool restart = false)
    {
        if (clip == null) return;
        if (!restart && bgmSource.clip == clip && bgmSource.isPlaying) return;
        bgmSource.clip = clip;
        bgmSource.volume = bgmVolume;
        bgmSource.Play();
    }

    // 读档是“重置”时刻 → 强制 BGM 从头放
    public void RefreshSceneBGM() => PlayDefaultBGMForScene(SceneManager.GetActiveScene().name, restart: true);
    public void PlayBossBGM() => PlayBGM(bossBGMClip);
    public void PlayAreaBGM() => PlayBGM(bgmClip);   // 普通区域 BGM（boss 击破后恢复用）
    public void PlayFutureBossBGM() => PlayBGM(futureBossBGMClip);
    public void StopBGM()     => bgmSource.Stop();

    public void SetBGMVolume(float v) { bgmVolume = Mathf.Clamp01(v); if (bgmSource != null) bgmSource.volume = bgmVolume; }
    public void SetSFXVolume(float v) { sfxVolume = Mathf.Clamp01(v); }

    public void Play(AudioClip clip)
    {
        if (clip == null) return;
        source.PlayOneShot(clip, sfxVolume);
    }

    // 带音量系数（0~1）的播放：某些音想比全局 SFX 音量更轻/更响时用
    public void Play(AudioClip clip, float volumeScale)
    {
        if (clip == null) return;
        source.PlayOneShot(clip, sfxVolume * Mathf.Clamp01(volumeScale));
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
    public void PlayDashStrike()     => Play(dashStrikeClip != null ? dashStrikeClip : dashClip);
    public void PlayBlock()          => Play(blockClip);
    public void PlayPerfectBlock()   { Play(perfectBlockClip); Play(perfectBlockTailClip); }
    // 成功识破：兵刃相交稍延后于提刀（~0.08s），错开避免被提刀盖住，形成“提刀→当啷”的先后层次
    public void PlayCounter()        => PlayDelayed(counterClip, 0.08f);
    public void PlayCounterWhiff()   => Play(counterWhiffClip);

    public void PlayDelayed(AudioClip clip, float delay, float volumeScale = 1f)
    {
        if (clip == null) return;
        StartCoroutine(PlayDelayedRoutine(clip, delay, volumeScale));
    }

    IEnumerator PlayDelayedRoutine(AudioClip clip, float delay, float volumeScale)
    {
        yield return new WaitForSecondsRealtime(delay);
        Play(clip, volumeScale);
    }
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
    public void PlayUIOpen()         => Play(uiOpenClip  != null ? uiOpenClip  : uiClickClip);
    public void PlayUIEquip()        => Play(uiEquipClip != null ? uiEquipClip : uiClickClip);
    public void PlayUIUse()          => Play(uiUseClip  != null ? uiUseClip  : uiClickClip);
    public void PlayEnemyAttack()    => Play(enemyAttackClip);
    public void PlayEnemyHit()       => Play(enemyHitClip);
    public void PlayEnemyDeath()     => Play(enemyDeathClip);
    public void PlayBossAttack()     => Play(bossAttackClip);
    public void PlayBossRush()       => Play(bossRushClip);
    public void PlayBossPhaseChange()=> Play(bossPhaseChangeClip);
    public void PlayBossRoar()       => Play(bossRoarClip != null ? bossRoarClip : bossPhaseChangeClip);
    public void PlayBossCharge()     => Play(bossChargeClip);
    // 返回所播片段时长（秒），方便调用方按时长串行等待
    public float PlayBossExplode()   { var c = bossExplodeClip  != null ? bossExplodeClip  : bossPhaseChangeClip; Play(c); return c != null ? c.length : 0f; }
    public float PlayBossRageRoar()  { var c = bossRageRoarClip != null ? bossRageRoarClip : bossRoarClip;        Play(c); return c != null ? c.length : 0f; }
    public void PlayBossDefeat()     => Play(bossDefeatClip);
    public void PlayBossNameStamp()  => Play(bossNameStampClip, 0.5f);   // 砸字音轻一点
    public void PlayHeal()           => Play(healClip);
    public void PlayVictory()        => Play(victoryClip);

    public void PlayFootstep()
    {
        if (Time.time - lastFootstepTime < 0.15f) return;
        lastFootstepTime = Time.time;
        Play(footstepClip);
    }
}
