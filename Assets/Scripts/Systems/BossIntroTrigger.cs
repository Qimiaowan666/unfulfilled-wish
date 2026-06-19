using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

// boss 房入口触发区：玩家首次进入 → boss 沉睡 → 登场对话 → 推镜对准 boss + 吼叫(震屏/zoom punch/血条充满) → 正式开打。
// 演出期间用临时高优先级 vcam 推镜，靠 Cinemachine 平滑 blend 进/出，结束后混合回玩家 vcam。
[RequireComponent(typeof(Collider2D))]
public class BossIntroTrigger : MonoBehaviour
{
    [Header("剧情 / boss")]
    public DialogueSequence sequence;
    public EnemyBase boss;   // 任意 boss（通用，不绑定具体类型）
    [Tooltip("场景里预摆的登场过场相机(默认低优先级，框 boss)。可在编辑器里调它的取景/大小")]
    public CinemachineCamera bossIntroVcam;
    [Tooltip("九日式登场揭幕名牌(黑条+罗马名+大红中文名)，吼叫时扫入")]
    public BossNameCard nameCard;
    [Tooltip("雾门：开战时关上挡住出口，boss 死亡/读档重置时自动开")]
    public BossFogGate fogGate;

    [Header("登场演出")]
    [Tooltip("吼叫 + 血条充满阶段时长")]
    public float roarDuration = 1.8f;
    [Tooltip("血条缓缓露出并充满的时长")]
    public float barRevealDuration = 1.5f;
    [Tooltip("吼叫震屏强度（世界单位）")]
    public float roarShakeMagnitude = 0.22f;
    [Tooltip("推镜 / 拉回时长")]
    public float camMoveDuration = 0.5f;

    // 登场演出（对话 + 吼叫）期间为 true → 屏蔽玩家输入、禁止暂停
    public static bool Sequencing { get; private set; }

    bool played;

    void Start()
    {
        if (boss != null) boss.combatEnabled = false;   // 进场前先沉睡
        if (fogGate != null && fogGate.boss == null) fogGate.boss = boss;   // 让雾门看守同一个 boss
    }

    void OnEnable()  { SaveSystem.AfterApply += OnSaveApplied; }
    void OnDisable() { SaveSystem.AfterApply -= OnSaveApplied; }
    void OnDestroy() { Sequencing = false; }

    // 读档 / 重生重新 apply 后：boss 若被还原为存活，则重新沉睡 + 藏血条 + 重置触发，让登场可再次触发。
    void OnSaveApplied()
    {
        Sequencing = false;
        if (BossActive())
        {
            played = false;
            boss.combatEnabled = false;
            Bar()?.HideForIntro();
        }
        else
        {
            played = true;   // boss 已被击败 / 不在场 → 不再触发登场演出
        }
    }

    void Update()
    {
        if (played || !BossActive()) return;
        // 兜底：读档/复活点已落在 boss 一侧 → 直接亮条开打、不演出。
        var player = GameObject.FindGameObjectWithTag(Tags.Player);
        if (player != null && PlayerPastTrigger(player.transform.position))
        {
            played = true;
            WakeImmediate();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (played || !other.CompareTag(Tags.Player)) return;
        if (!BossActive()) { played = true; return; }   // boss 已死/不在 → 不演出
        played = true;
        StartCoroutine(IntroSequence());
    }

    bool BossActive() => boss != null && boss.gameObject.activeInHierarchy && boss.CurrentHP > 0f;

    // 一进事件就推镜对准 boss → 对话(镜头停在 boss) → 吼叫 → 拉回相机 → 相机完全复原后双方才开始行动
    IEnumerator IntroSequence()
    {
        Sequencing = true;
        AudioManager.Instance?.StopBGM();   // 遭遇触发 → 立刻静音；对话/吼叫在安静中进行，开打才起 boss 曲
        FaceBossAtPlayer();

        // 进剧情即冻住玩家：清速度 + 取消当前动作(回 idle) + 停物理，整段演出原地不动。StartCombat 再解冻。
        var playerGo = GameObject.FindGameObjectWithTag(Tags.Player);
        var pc  = playerGo != null ? playerGo.GetComponent<PlayerController>() : null;
        var prb = playerGo != null ? playerGo.GetComponent<Rigidbody2D>() : null;
        if (prb != null) prb.linearVelocity = Vector2.zero;
        if (pc  != null) pc.stateMachine.ChangeState(pc.idleState);
        if (prb != null) prb.simulated = false;

        var cam = Camera.main;
        // 不再手动接管/关 Brain —— 改用临时高优先级 vcam，让 Cinemachine 平滑 blend 进/出，
        // 交还时由 Cinemachine 混合回玩家 vcam（不存在“冷启动硬切”的突跳）。

        // 把 Brain 默认混合时长临时设成 camMoveDuration（场景默认是 2s，太慢）
        var brain = cam != null ? cam.GetComponent<CinemachineBrain>() : null;
        CinemachineBlendDefinition prevBlend = default;
        if (brain != null)
        {
            prevBlend = brain.DefaultBlend;
            var b = brain.DefaultBlend;
            b.Style = CinemachineBlendDefinition.Styles.EaseInOut;
            b.Time  = camMoveDuration;
            brain.DefaultBlend = b;
        }

        // 抬高场景里预摆的过场 vcam 的优先级 → Cinemachine 平滑 blend 推进到 boss。
        // 取景(位置/大小)和抖动基准都在那台 vcam 上、可在编辑器里直接调。
        Vector3 vcamHome = bossIntroVcam != null ? bossIntroVcam.transform.position : Vector3.zero;
        float   baseSize = bossIntroVcam != null ? bossIntroVcam.Lens.OrthographicSize : 8f;
        int     prevPrio = 0;
        if (bossIntroVcam != null)
        {
            var p = bossIntroVcam.Priority; prevPrio = p.Value; p.Value = 100; bossIntroVcam.Priority = p;
        }

        // 1) 等推进 blend 完成（timeScale 仍为 1）
        yield return new WaitForSecondsRealtime(camMoveDuration);

        // 2) 对话（镜头停在 boss 上；DialogueUI 内部会把 timeScale 置 0）
        bool dialogueDone = false;
        if (DialogueUI.Instance != null && sequence != null)
            DialogueUI.Instance.Play(sequence, () => dialogueDone = true);
        else
            dialogueDone = true;
        while (!dialogueDone) yield return null;

        // 3) 吼叫：音效 + 揭幕名牌扫入 + 血条充满 + boss 弹一下 + 强震 + 轻 zoom 抖（抖 introVcam，不直接动相机）+ 全屏放射状吼叫爆发
        AudioManager.Instance?.PlayBossPhaseChange();
        if (nameCard != null) nameCard.Play(boss != null ? boss.profile : null, roarDuration);
        Bar()?.Reveal(barRevealDuration);
        StartCoroutine(BossScalePunch());
        ScreenRoarFx.Burst(BossSpriteCenter(), 0.85f, 0.88f, Color.black);   // 连放 4 波集中线（黑色）

        float t = 0f;
        while (t < roarDuration)
        {
            t += Time.deltaTime;
            float decay = 1f - Mathf.Clamp01(t / roarDuration);
            if (bossIntroVcam != null)
            {
                Vector2 sh = Random.insideUnitCircle * (roarShakeMagnitude * decay);
                bossIntroVcam.transform.position = vcamHome + new Vector3(sh.x, sh.y, 0f);
                float zp = Mathf.Sin(t * 16f) * 0.18f * decay;
                var lr = bossIntroVcam.Lens; lr.OrthographicSize = baseSize - zp; bossIntroVcam.Lens = lr;
            }
            yield return null;
        }
        if (bossIntroVcam != null)
        {
            bossIntroVcam.transform.position = vcamHome;
            var lf = bossIntroVcam.Lens; lf.OrthographicSize = baseSize; bossIntroVcam.Lens = lf;
        }

        // 4) 降回过场 vcam 优先级 → Cinemachine 平滑 blend 回玩家 vcam（正确取景、无冷切突跳）
        if (bossIntroVcam != null) { var p = bossIntroVcam.Priority; p.Value = prevPrio; bossIntroVcam.Priority = p; }
        yield return new WaitForSecondsRealtime(camMoveDuration);

        // 5) 还原 Brain 混合设置
        if (brain != null) brain.DefaultBlend = prevBlend;

        // 6) 双方开始行动
        StartCombat();   // boss.Activate() + BGM + 解锁玩家
    }

    // boss 吼叫弹一下（轻微 squash 模拟发力），保留朝向用的 localScale 符号
    IEnumerator BossScalePunch()
    {
        if (boss == null) yield break;
        Vector3 baseS = boss.transform.localScale;
        Vector3 punch = new Vector3(baseS.x * 1.05f, baseS.y * 1.05f, baseS.z);
        float t = 0f, dur = 0.4f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI);
            boss.transform.localScale = Vector3.Lerp(baseS, punch, p);
            yield return null;
        }
        boss.transform.localScale = baseS;
    }

    void WakeImmediate()
    {
        Bar()?.Reveal(0.4f);
        StartCombat();
    }

    void StartCombat()
    {
        Sequencing = false;
        // 解冻玩家（登场演出里冻住了它）+ 清残速，从静止开打
        var pgo = GameObject.FindGameObjectWithTag(Tags.Player);
        var prb = pgo != null ? pgo.GetComponent<Rigidbody2D>() : null;
        if (prb != null) { prb.simulated = true; prb.linearVelocity = Vector2.zero; }
        if (boss != null) boss.Activate();
        if (fogGate != null) fogGate.Close();   // 关上雾门，封住出口
        AudioManager.Instance?.PlayBossBGM();
    }

    // boss 精灵中心（放射吼叫的发出点）
    Vector3 BossSpriteCenter()
    {
        if (boss == null) return Vector3.zero;
        var sr = boss.GetComponentInChildren<SpriteRenderer>();
        return sr != null ? sr.bounds.center : boss.transform.position;
    }

    void FaceBossAtPlayer()
    {
        if (boss == null) return;
        var player = GameObject.FindGameObjectWithTag(Tags.Player);
        if (player != null)
            boss.FaceToward(player.transform.position);
    }

    BossHealthBarUI Bar() => FindAnyObjectByType<BossHealthBarUI>();

    bool PlayerPastTrigger(Vector3 playerPos)
    {
        if (boss == null) return false;
        float toBoss   = boss.transform.position.x - transform.position.x;
        float toPlayer = playerPos.x - transform.position.x;
        return Mathf.Abs(toPlayer) > 1.5f && Mathf.Sign(toPlayer) == Mathf.Sign(toBoss);
    }
}
