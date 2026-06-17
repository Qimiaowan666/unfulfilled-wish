using System.Collections;
using UnityEngine;

// 弓箭手(远程小怪)：复用 GroundEnemy 状态机/攻击池，AttackTrigger 改成发射箭矢。
// 不做持续风筝后退(把 retreatDistance 设 0)；改为生命首次跌破一半时，往后闪一次(dash 闪避带无敌)。
public class ArcherEnemy : GroundEnemy
{
    [Header("弓箭手")]
    public Arrow   arrowPrefab;
    public Vector2 firePointOffset = new Vector2(0.7f, 0.2f);   // 出箭点(x 随朝向翻转)
    public float   aimHeightOffset = 0f;                        // 瞄准点相对玩家 pivot(腰部)的垂直偏移(正=往上)

    [Header("特殊重箭 (识破防御 — special_attack 末发)")]
    public float heavyArrowDamageMultiplier = 2f;                  // 重箭伤害 = 基础攻击 × 此值
    public float heavyArrowScale = 1.4f;                           // 重箭放大(更显眼)
    public Color heavyArrowTint  = new Color(1f, 0.45f, 0.3f, 1f); // 偏红：提示"无法格挡，需识破"

    [Header("尘土特效")]
    public GameObject dustPrefab;               // 脚下尘土(SpriteOneShot 预制)，起跳/后撤步时生成
    public float      dustYOffset = 0.3f;       // 尘土高度微调(碰撞体底部之上；气太低就调大)

    [Header("大招悬空 + 重箭预警 (弦一郎式)")]
    public float specialLeapVelocity  = 7f;     // 起跳上冲速度
    public float specialRiseTime      = 0.22f;  // 上冲时长(× 速度 ≈ 悬空高度)，之后悬停到大招结束
    public float specialLeapBackward  = 0f;     // 起跳横向后撤(背对玩家；>0 当心跳下平台)
    public float heavyWarningDuration = 0.55f;  // 重箭红闪预警时长(覆盖到放箭)

    [Header("半血后撤步 (一次性)")]
    [Range(0f, 1f)] public float backstepHpThreshold = 0.5f;   // 生命跌破此比例触发
    public float backstepSpeed      = 12f;                      // 后撤速度
    public float backstepDuration   = 0.3f;                     // 后撤时长(× speed ≈ 距离)
    public bool  backstepInvincible = true;                     // 后撤期间无敌(真·闪避)

    bool dashedBack;   // 只闪一次
    bool dashing;
    bool  inSpecialAir;   // 大招悬空中(全程零重力)
    float baseGravity;    // 缓存原始重力，结束后恢复

    public override string MoveClip => "run";
    public override string HurtClip => "hurt";

    protected override void Awake()
    {
        base.Awake();
        if (Rb != null) baseGravity = Rb.gravityScale;
    }

    // 攻击动画里的 AttackTrigger 事件 → 放箭(覆盖基类近战命中)
    // 轻箭(可格挡) —— 动画事件 AttackTrigger(单发 attack + 三连射前三发)
    public override void AttackTrigger()
    {
        var a = CurrentAttack;
        FireArrow(attack * (a != null ? a.damageMultiplier : 1f), false, Color.white, 1f);
    }

    // 重箭(无法格挡，需识破) —— 动画事件 HeavyArrowTrigger(special_attack 末尾那发，拖拍后到)
    public void HeavyArrowTrigger()
    {
        FireArrow(attack * heavyArrowDamageMultiplier, true, heavyArrowTint, heavyArrowScale);
    }

    void FireArrow(float dmg, bool special, Color tint, float scale)
    {
        if (arrowPrefab == null) return;
        Vector3 spawn = transform.position + new Vector3(firePointOffset.x * FacingDir, firePointOffset.y, 0f);

        // 瞄准玩家身体中部(带高低差)；玩家不在时退化为水平
        Vector2 aim = Vector2.right * FacingDir;
        if (player != null)
        {
            aim = ((Vector2)player.position + Vector2.up * aimHeightOffset) - (Vector2)spawn;
            if (aim.sqrMagnitude < 0.01f) aim = Vector2.right * FacingDir;
        }

        var arrow = Instantiate(arrowPrefab, spawn, Quaternion.identity);
        if (!Mathf.Approximately(scale, 1f)) arrow.transform.localScale *= scale;
        var sr = arrow.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = tint;
        arrow.Launch(this, aim, dmg, special);
        AudioManager.Instance?.PlayBow(special);   // 放弦音(重箭用蓄力重音)
    }

    // scene 里画尘土生成点(黄色小圈)，方便对齐 dustYOffset（运行时尘土本身才可见）
    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();
        var col = GetComponent<Collider2D>();
        float footY = (col != null ? col.bounds.min.y : transform.position.y) + dustYOffset;
        Vector3 p = new Vector3(transform.position.x, footY, 0f);
        Gizmos.color = new Color(1f, 0.85f, 0.4f, 0.9f);
        Gizmos.DrawWireSphere(p, 0.12f);
        Gizmos.DrawLine(p + Vector3.left * 0.35f, p + Vector3.right * 0.35f);
    }

    // 脚下生成尘土(起跳/后撤步)
    void SpawnFootDust()
    {
        if (dustPrefab == null) return;
        var col = GetComponent<Collider2D>();
        float footY = (col != null ? col.bounds.min.y : transform.position.y) + dustYOffset;
        Instantiate(dustPrefab, new Vector3(transform.position.x, footY, 0f), Quaternion.identity);
    }

    // 动画事件：大招起跳并悬空(special_attack 开头) — 升到位后零重力悬停，全程在空中放箭
    public void SpecialLeap()
    {
        if (Rb == null) return;
        SpawnFootDust();
        inSpecialAir = true;
        Rb.gravityScale = 0f;
        StartCoroutine(RiseThenHover());
    }

    IEnumerator RiseThenHover()
    {
        Rb.linearVelocity = new Vector2(-FacingDir * specialLeapBackward, specialLeapVelocity);
        yield return new WaitForSeconds(specialRiseTime);
        if (inSpecialAir && Rb != null) Rb.linearVelocity = Vector2.zero;   // 升到位 → 悬停
    }

    // 动画事件：重箭"危"提示(拉满→放箭之间染红，告诉玩家这发要识破)
    public void HeavyWarning()
    {
        var fb = GetComponent<DamageFeedback>();
        if (fb != null) fb.FlashWarning(heavyWarningDuration);
    }

    // 生命首次跌破一半 → 触发一次后撤步
    public override void TakeDamage(float damage, float poiseDamage)
    {
        base.TakeDamage(damage, poiseDamage);
        if (!dashedBack && !dashing && CurrentHP > 0f && CurrentHP <= maxHP * backstepHpThreshold)
            StartCoroutine(BackstepRoutine());
    }

    IEnumerator BackstepRoutine()
    {
        if (inSpecialAir) { inSpecialAir = false; if (Rb != null) Rb.gravityScale = baseGravity; }   // 悬空中被打断先恢复重力
        dashing    = true;
        dashedBack = true;
        SpawnFootDust();
        // 远离玩家的方向(背对玩家)；保持面向玩家不翻身，像后跳
        int away = player != null ? (player.position.x > transform.position.x ? -1 : 1) : -FacingDir;
        PlayClip("dash");
        if (backstepInvincible) Invincible = true;

        float t = 0f;
        while (t < backstepDuration)
        {
            if (LedgeAhead(away)) break;   // 别闪下平台掉进岩浆
            if (Rb != null) Rb.linearVelocity = new Vector2(away * backstepSpeed, Rb.linearVelocity.y);
            t += Time.deltaTime;
            yield return null;
        }

        if (Rb != null) Rb.linearVelocity = new Vector2(0f, Rb.linearVelocity.y);
        Invincible = false;
        dashing    = false;
        stateMachine.ChangeState(player != null ? (EnemyBaseState)chaseState : idleState);
    }

    protected override void Update()
    {
        if (dashing) return;   // 闪避位移由协程接管，不跑状态机
        // 大招悬空结束(正常结束或被打断切走攻击态) → 恢复重力落地
        if (inSpecialAir && stateMachine.currentState != attackState)
        {
            inSpecialAir = false;
            if (Rb != null) Rb.gravityScale = baseGravity;
        }
        // 攻击中持续面向玩家：远程要追着玩家射，玩家绕到另一边也转身(否则朝反方向放箭)
        if (player != null && stateMachine.currentState == attackState)
            SetFacing(Mathf.Sign(player.position.x - transform.position.x));
        base.Update();
    }

    // 复活/读档：重置后撤步 + 悬空状态
    protected override void ResetForm()
    {
        dashedBack = false;
        dashing    = false;
        if (inSpecialAir && Rb != null) Rb.gravityScale = baseGravity;
        inSpecialAir = false;
    }
}
