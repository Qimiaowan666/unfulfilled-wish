using System.Collections;
using UnityEngine;

// boss 房入口触发区：玩家首次进入 → boss 沉睡 → 登场对话 → 推镜对准 boss + 吼叫(震屏/zoom punch/血条充满) → 正式开打。
// 演出期间直接接管主相机（关掉 Cinemachine / RoomCamera），结束后再交还。
[RequireComponent(typeof(Collider2D))]
public class BossIntroTrigger : MonoBehaviour
{
    [Header("剧情 / boss")]
    public DialogueSequence sequence;
    public MinotaurBoss boss;

    [Header("登场演出")]
    [Tooltip("吼叫 + 血条充满阶段时长")]
    public float roarDuration = 1.8f;
    [Tooltip("血条缓缓露出并充满的时长")]
    public float barRevealDuration = 1.5f;
    [Tooltip("吼叫震屏强度（世界单位）")]
    public float roarShakeMagnitude = 0.22f;
    [Tooltip("推镜 / 拉回时长")]
    public float camMoveDuration = 0.5f;
    [Tooltip("boss 占视野高度的比例，越大越贴满（0.8≈占大半屏）")]
    public float framePadding = 0.8f;

    // 登场演出（对话 + 吼叫）期间为 true → 屏蔽玩家输入、禁止暂停
    public static bool Sequencing { get; private set; }

    bool played;

    void Start()
    {
        if (boss != null) boss.combatEnabled = false;   // 进场前先沉睡
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
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && PlayerPastTrigger(player.transform.position))
        {
            played = true;
            WakeImmediate();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (played || !other.CompareTag("Player")) return;
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

        var cam = Camera.main;
        // 直接接管相机：关掉 Cinemachine brain 与 RoomCamera（用字符串取组件，避免硬依赖具体相机插件类型）
        var brain   = cam != null ? cam.GetComponent("CinemachineBrain") as Behaviour : null;
        var roomCam = cam != null ? cam.GetComponent("RoomCamera") as Behaviour : null;
        bool brainWas = brain != null && brain.enabled;
        bool roomWas  = roomCam != null && roomCam.enabled;
        if (brain != null)   brain.enabled = false;
        if (roomCam != null) roomCam.enabled = false;

        Vector3 camStart  = cam != null ? cam.transform.position : Vector3.zero;
        float   sizeStart = cam != null && cam.orthographic ? cam.orthographicSize : 8f;

        // 用 boss 实际渲染包围盒自适应取景，保证整只 boss 入画
        var sr = boss.GetComponentInChildren<SpriteRenderer>();
        Bounds bnd = sr != null ? sr.bounds : new Bounds(boss.transform.position, Vector3.one * 8f);
        Vector3 focus = new Vector3(bnd.center.x, bnd.center.y, camStart.z);
        float fitSize = Mathf.Clamp(bnd.extents.y / Mathf.Max(0.3f, framePadding), 6f, 16f);

        // 1) 刚进事件 → 立刻把镜头推到 boss（timeScale 仍为 1）
        yield return CamMove(cam, camStart, focus, sizeStart, fitSize, camMoveDuration);

        // 2) 对话（镜头停在 boss 上不动；DialogueUI 内部会把 timeScale 置 0）
        bool dialogueDone = false;
        if (DialogueUI.Instance != null && sequence != null)
            DialogueUI.Instance.Play(sequence, () => dialogueDone = true);
        else
            dialogueDone = true;
        while (!dialogueDone) yield return null;

        // 3) 吼叫：音效 + 血条充满 + boss 弹一下 + 强震 + 轻 zoom 抖
        AudioManager.Instance?.PlayBossPhaseChange();
        Bar()?.Reveal(barRevealDuration);
        StartCoroutine(BossScalePunch());

        float t = 0f;
        while (t < roarDuration)
        {
            t += Time.deltaTime;
            float decay = 1f - Mathf.Clamp01(t / roarDuration);
            if (cam != null)
            {
                Vector2 sh = Random.insideUnitCircle * (roarShakeMagnitude * decay);
                float zp = Mathf.Sin(t * 16f) * 0.18f * decay;
                cam.transform.position = focus + new Vector3(sh.x, sh.y, 0f);
                if (cam.orthographic) cam.orthographicSize = fitSize - zp;
            }
            yield return null;
        }

        // 4) 拉回到进场前的相机位置 + 交还 Cinemachine（此时 boss 仍沉睡、玩家仍被锁）
        if (cam != null) yield return CamMove(cam, cam.transform.position, camStart, cam.orthographicSize, sizeStart, camMoveDuration);
        if (brain != null)   brain.enabled = brainWas;
        if (roomCam != null) roomCam.enabled = roomWas;

        // 5) 相机完全复原后，双方才开始行动
        StartCombat();   // boss.Activate() + BGM + 解锁玩家
    }

    IEnumerator CamMove(Camera cam, Vector3 fromP, Vector3 toP, float fromS, float toS, float dur)
    {
        if (cam == null) yield break;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            cam.transform.position = Vector3.Lerp(fromP, toP, k);
            if (cam.orthographic) cam.orthographicSize = Mathf.Lerp(fromS, toS, k);
            yield return null;
        }
        cam.transform.position = toP;
        if (cam.orthographic) cam.orthographicSize = toS;
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
        if (boss != null) boss.Activate();
        AudioManager.Instance?.PlayBossBGM();
    }

    void FaceBossAtPlayer()
    {
        if (boss == null) return;
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            boss.SetFacing(Mathf.Sign(player.transform.position.x - boss.transform.position.x));
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
