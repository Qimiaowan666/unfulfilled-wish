using System.Collections;
using UnityEngine;

public class MinotaurBoss : EnemyBase
{
    // ── State Machine ────────────────────────────────────────────────
    public BossStateMachine        stateMachine        { get; private set; }
    public Boss_IdleState          idleState           { get; private set; }
    public Boss_BattleState        battleState         { get; private set; }
    public Boss_ChaseState         chaseState          { get; private set; }
    public Boss_WaitState          waitState           { get; private set; }
    public Boss_NormalAttackState  normalAttackState   { get; private set; }
    public Boss_SpecialAttackState specialAttackState  { get; private set; }
    public Boss_EnragedState       enragedState        { get; private set; }
    public Boss_StunnedState       stunnedState        { get; private set; }
    public Boss_StaggerState       staggerState        { get; private set; }
    public Boss_RepositionState    repositionState     { get; private set; }
    public Boss_JumpSlashState     jumpSlashState      { get; private set; }
    public Boss_Atk3State          atk3State           { get; private set; }
    public Boss_DeadState          deadState           { get; private set; }

    // ── Inspector Config ─────────────────────────────────────────────
    [Header("Boss Movement")]
    public float moveSpeed        = 2.2f;
    public float phase2SpeedBonus = 1.2f;

    [Header("Phase 2")]
    public float phase2HPThreshold      = 0.5f;
    public float phase2AttackMultiplier = 1.5f;

    [Header("Counter Reaction")]
    public float staggerDuration        = 0.6f;   // 被识破后的停顿时长

    [Header("Combo Patterns")]
    public ComboPattern[] comboPatterns;

    [Header("Teleport (闪身)")]
    public float teleportDuration  = 0.35f;   // 瞬移总时长（淡出 + 淡入）
    public float teleportOffset    = 0.8f;    // 现身在玩家身后多远
    public float landingRecovery   = 0.12f;   // 闪身现身后到出招的硬直窗口
    public LayerMask teleportWallLayer;       // 闪身落点墙体检测层（Inspector 设成 Ground；留空则按名字找 "Ground"）
    public float teleportClearance = 0.6f;    // 落点距墙安全距离（≈ boss 碰撞半宽）

    [Header("Jump Slash (二段跳劈)")]
    public float jumpHeight       = 3f;      // 跳跃顶点高度
    public float jumpAirborneTime = 0.75f;   // 起跳→落地用时（对齐 atk2 第9帧 AnimHitOpen = 砸地瞬间）

    [Header("Atk3 (横劈挑飞 + 跳劈)")]
    public float    atk3RiseHeight     = 4f;     // 挑飞升空高度
    public float    atk3LaunchTime     = 0.4f;   // 升空用时（玩家与 boss 共用的上升时长）
    public float    atk3BossJumpDelay  = 0.25f;  // boss 比玩家晚多久起跳（玩家先飞，boss 后追）
    public float    atk3SlamTime       = 0.25f;  // 下劈砸回地面用时
    public float    atk3AnimSpeed      = 0.7f;   // atk3 动画播放速度（<1 变慢）
    public float    atk3HoldDistance   = 2f;     // 挑飞时把玩家固定在 boss 正前方多远（避免重叠）
    public float    atk3SlamMultiplier = 2f;     // 落地伤害倍率

    [Header("Attack Hitboxes")]
    public Hitbox[] hitboxes;

    [Header("Special Attack Indicator")]
    public bool           showAttackIndicator    = true;
    public SpriteRenderer indicatorRenderer;
    public Vector2        indicatorSize          = new Vector2(2f, 1.5f);
    public Vector2        indicatorOffset        = new Vector2(2f, 0f);
    public Color          indicatorWarningColor  = new Color(1f, 0.2f, 0.2f, 0.35f);
    public Color          indicatorImpactColor   = new Color(1f, 0.7f, 0.2f, 0.85f);

    // ── Aliases for state classes（语义化别名 → 父类字段）─────────────
    public float normalAttackCooldown   => attackCooldown;
    public float specialWarningDuration => specialAttackWarningDuration;

    // ── Runtime ──────────────────────────────────────────────────────
    public bool  IsPhase2     { get; private set; }

    DamageFeedback damageFeedback;
    Coroutine      indicatorRoutine;
    SpriteRenderer spriteRenderer;

    public DamageFeedback DamageFeedback => damageFeedback;

    public enum AttackId { Atk1, Atk2, Special, JumpSlash, Atk3 }
    public enum HitboxKey { Atk1, Atk2, Special, Atk3 }

    public enum MovementType { None, TeleportBehind, TeleportOtherSide }

    [System.Serializable]
    public class ComboStep
    {
        public AttackId     attack;
        public MovementType preMove = MovementType.None;   // 出招前的位移
    }

    [System.Serializable]
    public class ComboPattern
    {
        public string      patternName;       // 调试用
        public ComboStep[] steps;             // 按顺序：每段 = 位移 + 攻击
        public float       weight = 1f;       // pattern 之间的权重
        public bool        phase2Only = false;
    }

    [System.Serializable]
    public class Hitbox : AttackHitbox
    {
        public HitboxKey key;
    }

    public override AttackHitbox GetHitbox(string id)
    {
        if (hitboxes == null || !System.Enum.TryParse(id, out HitboxKey key)) return null;
        foreach (var hb in hitboxes)
            if (hb != null && hb.key == key) return hb;
        return null;
    }

    // ── SetAnimBool（所有 8 个 bool）──────────────────────────────────
    static readonly string[] boolNames =
    {
        "isIdle", "isMoving", "isAttacking", "isAttacking2",
        "isSpecialAttacking", "isDefending", "isHit", "isDead"
    };

    public override void SetAnimBool(string boolName)
    {
        if (Anim == null || Anim.runtimeAnimatorController == null) return;
        foreach (var name in boolNames)
            Anim.SetBool(name, name == boolName);
    }

    [Header("登场 / 沉睡")]
    [Tooltip("false = boss 沉睡，不主动进入战斗；由 BossIntroTrigger 在登场对话结束后 Activate() 唤醒")]
    public bool combatEnabled = true;
    public void Activate() => combatEnabled = true;

    // ── Lifecycle ────────────────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();
        damageFeedback = GetComponent<DamageFeedback>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (NeedsDefaultPatterns())
            comboPatterns = DefaultComboPatterns();

        stateMachine        = new BossStateMachine();
        idleState           = new Boss_IdleState(this, stateMachine);
        battleState         = new Boss_BattleState(this, stateMachine);
        chaseState          = new Boss_ChaseState(this, stateMachine);
        waitState           = new Boss_WaitState(this, stateMachine);
        normalAttackState   = new Boss_NormalAttackState(this, stateMachine);
        specialAttackState  = new Boss_SpecialAttackState(this, stateMachine);
        enragedState        = new Boss_EnragedState(this, stateMachine);
        stunnedState        = new Boss_StunnedState(this, stateMachine);
        staggerState        = new Boss_StaggerState(this, stateMachine);
        repositionState     = new Boss_RepositionState(this, stateMachine);
        jumpSlashState      = new Boss_JumpSlashState(this, stateMachine);
        atk3State           = new Boss_Atk3State(this, stateMachine);
        deadState           = new Boss_DeadState(this, stateMachine);

        stateMachine.Initialize(idleState);

        if (indicatorRenderer != null) indicatorRenderer.gameObject.SetActive(false);
    }

    protected override void Update()
    {
        base.Update();
        if (CurrentHP <= 0f) return;

        if (attackCooldownTimer > 0f) attackCooldownTimer -= Time.deltaTime;

        CheckPhase2();
        stateMachine.Update();
    }

    void CheckPhase2()
    {
        if (IsPhase2) return;
        if (CurrentHP / maxHP >= phase2HPThreshold) return;
        if (stateMachine.currentState == deadState || stateMachine.currentState == enragedState) return;

        IsPhase2 = true;
        // 二阶段开放的连段由 ComboPattern.phase2Only 控制，无需在此切换权重
        stateMachine.ChangeState(enragedState);
    }

    protected override void OnDeath()   => stateMachine.ChangeState(deadState);
    protected override void OnRespawn()
    {
        IsPhase2 = false;
        stateMachine.ChangeState(idleState);
    }

    protected override void OnPoiseBroken()
    {
        if (CurrentHP <= 0f) return;
        stateMachine.ChangeState(stunnedState);
    }

    public override void OnCountered()
    {
        if (CurrentHP <= 0f) return;
        stateMachine.ChangeState(staggerState);
    }

    // ── Animation Event Entry Points（动画事件直接调这里）─────────────
    public void AnimHitOpen()  => (stateMachine.currentState as BossBaseState)?.OnHitWindowOpen();
    public void AnimHitClose() => (stateMachine.currentState as BossBaseState)?.OnHitWindowClose();
    public void AnimFinish()   => (stateMachine.currentState as BossBaseState)?.OnAnimationFinished();

    // ── Combo Runtime ────────────────────────────────────────────────
    ComboPattern currentPattern;
    int          comboIndex;

    // 攻击状态打完一段后调：返回当前连段的下一段；连段结束则刷新冷却 + 回 battle 等间隔
    public ComboStep AdvanceCombo()
    {
        if (currentPattern != null && comboIndex < currentPattern.steps.Length)
        {
            var step = currentPattern.steps[comboIndex];
            comboIndex++;
            if (IsAttackAvailable(step.attack)) return step;
        }
        EndCombo();   // 连段打完（或下一段不可用）→ 刷新冷却，回 battle 间隔
        return null;
    }

    // BattleState 冷却好后调：挑一套新连段，返回第一段
    public ComboStep StartNewCombo()
    {
        currentPattern = PickAttack(comboPatterns, p =>
        {
            if (p?.steps == null || p.steps.Length == 0) return 0f;
            if (p.phase2Only && !IsPhase2) return 0f;
            foreach (var s in p.steps)
                if (!IsAttackAvailable(s.attack)) return 0f;
            return p.weight;
        });

        if (currentPattern == null) return null;

        comboIndex = 1;
        Debug.Log($"[Boss] 连段 [{currentPattern.patternName}] · {currentPattern.steps.Length} 段");
        return currentPattern.steps[0];
    }

    void EndCombo()
    {
        ResetCombo();
        StartAttackCooldown();   // 连段结束 → 刷新冷却，保证下一套连段前有间隔
    }

    // 执行一段：有位移先走 RepositionState，无位移直接出招
    public void ExecuteStep(ComboStep step)
    {
        if (step.preMove != MovementType.None)
        {
            Vector2 target = GetMovementTarget(step.preMove);
            repositionState.Configure(step.preMove, target, step.attack);
            stateMachine.ChangeState(repositionState);
        }
        else
        {
            RefreshFacingToPlayer();
            EnterAttack(step.attack);
        }
    }

    public void ResetCombo()
    {
        currentPattern = null;
        comboIndex     = 0;
    }

    public void RefreshFacingToPlayer()
    {
        if (player == null) return;
        float dir = Mathf.Sign(player.position.x - transform.position.x);
        SetFacing(dir);
    }

    bool IsAttackAvailable(AttackId id)
    {
        switch (id)
        {
            case AttackId.Atk1:      return true;
            case AttackId.Atk2:      return true;
            case AttackId.Special:   return true;   // 统一冷却：大招不再单独冷却
            case AttackId.JumpSlash: return true;
            case AttackId.Atk3:      return true;
        }
        return false;
    }

    // ── Reposition helpers（被 Boss_RepositionState 调用）──────────────
    public Vector2 GetMovementTarget(MovementType move)
    {
        if (player == null) return transform.position;
        float bossY = transform.position.y;

        switch (move)
        {
            case MovementType.TeleportBehind:
            {
                // 玩家面朝方向的反向 → 玩家身后
                int playerFacing = 1;
                var pc = player.GetComponent<PlayerController>();
                if (pc != null) playerFacing = pc.FacingDir;
                return ClampTeleportTarget(new Vector2(player.position.x - playerFacing * teleportOffset, bossY));
            }
            case MovementType.TeleportOtherSide:
            {
                // 翻到玩家的另一侧（相对 boss 当前所在侧）
                float side = Mathf.Sign(transform.position.x - player.position.x);
                if (side == 0f) side = 1f;
                return ClampTeleportTarget(new Vector2(player.position.x - side * teleportOffset, bossY));
            }
            default:
                return transform.position;
        }
    }

    // 把闪身落点限制在墙内侧：从玩家位置朝落点探墙，挡住就翻另一侧 / 贴墙前，避免 boss 被传送进墙。
    // 关键：安全距离用 boss 碰撞体的实际半宽（运行时读取），boss 越大留得越远，半身才不会插进墙。
    Vector2 ClampTeleportTarget(Vector2 desired)
    {
        if (player == null) return desired;

        LayerMask mask = teleportWallLayer.value != 0 ? teleportWallLayer : LayerMask.GetMask("Ground");
        if (mask.value == 0) return desired;   // 没有可用的墙层 → 不 clamp（安全降级）

        var col = GetComponent<Collider2D>();
        float halfW = (col != null ? col.bounds.extents.x : 0.5f) + teleportClearance;  // boss 半身宽 + 额外余量

        // 射线在 boss 中心高度水平探（避开脚下地面，只探竖直墙面）
        Vector2 origin = new Vector2(player.position.x, desired.y);
        float dx = desired.x - origin.x;
        float dist = Mathf.Abs(dx);
        if (dist < 0.01f) return desired;
        float sign = Mathf.Sign(dx);

        // 探测距离 = 位移距离 + 半身宽：覆盖 boss 朝向那一侧的整个身体宽度
        float probe = dist + halfW;

        // 目标侧（玩家身后）通畅 → 直接用
        if (!Physics2D.Raycast(origin, new Vector2(sign, 0f), probe, mask))
            return desired;

        // 目标侧被墙挡 → 翻到玩家另一侧（若那侧通畅）
        if (!Physics2D.Raycast(origin, new Vector2(-sign, 0f), probe, mask))
            return new Vector2(origin.x - sign * teleportOffset, desired.y);

        // 两侧都有墙（窄道）→ 落在目标侧墙前，留出整整一个半身宽，确保不插墙
        var hit = Physics2D.Raycast(origin, new Vector2(sign, 0f), probe, mask);
        return new Vector2(hit.point.x - sign * halfW, desired.y);
    }

    public void SetInvincible(bool on) => Invincible = on;

    public void SetSpriteVisible(bool visible)
    {
        if (spriteRenderer != null) spriteRenderer.enabled = visible;
    }

    public void SetSpriteAlpha(float a)
    {
        if (spriteRenderer == null) return;
        var c = spriteRenderer.color;
        c.a = Mathf.Clamp01(a);
        spriteRenderer.color = c;
    }

    // 空数组、或所有 pattern 的 steps 都为空（旧 sequence 数据迁移失效）→ 用默认值
    bool NeedsDefaultPatterns()
    {
        if (comboPatterns == null || comboPatterns.Length == 0) return true;
        foreach (var p in comboPatterns)
            if (p != null && p.steps != null && p.steps.Length > 0) return false;
        return true;
    }

    // Inspector 数据结构变更后的兜底默认值
    ComboPattern[] DefaultComboPatterns()
    {
        ComboStep S(AttackId a, MovementType m = MovementType.None) =>
            new ComboStep { attack = a, preMove = m };

        return new ComboPattern[]
        {
            new ComboPattern { patternName = "单段普攻", weight = 5f, steps = new[] { S(AttackId.Atk1) } },
            new ComboPattern { patternName = "大招独立", weight = 3f, steps = new[] { S(AttackId.Special) } },
            new ComboPattern { patternName = "二段跳劈", weight = 3f, steps = new[] { S(AttackId.JumpSlash) } },
            new ComboPattern { patternName = "跳劈接大招", weight = 2f, steps = new[] { S(AttackId.JumpSlash), S(AttackId.Special) } },
            new ComboPattern { patternName = "挑飞接大招", weight = 2f, steps = new[] { S(AttackId.Atk3), S(AttackId.Special) } },
            new ComboPattern { patternName = "闪身斩",   weight = 3f, steps = new[] { S(AttackId.Atk1), S(AttackId.Atk1, MovementType.TeleportOtherSide), S(AttackId.Atk1, MovementType.TeleportOtherSide) } },
            new ComboPattern { patternName = "三段全连", weight = 2f, phase2Only = true,
                               steps = new[] { S(AttackId.Atk1), S(AttackId.Atk2), S(AttackId.Special) } },
            new ComboPattern { patternName = "瞬移大招", weight = 2f, phase2Only = true,
                               steps = new[] { S(AttackId.Atk1), S(AttackId.Special, MovementType.TeleportBehind) } },
        };
    }

    // 路由：根据选中的 AttackId 切到对应 state
    public void EnterAttack(AttackId id)
    {
        switch (id)
        {
            case AttackId.Atk1:
                normalAttackState.Configure(useAtk2: false);
                stateMachine.ChangeState(normalAttackState);
                break;
            case AttackId.Atk2:
                normalAttackState.Configure(useAtk2: true);
                stateMachine.ChangeState(normalAttackState);
                break;
            case AttackId.Special:
                stateMachine.ChangeState(specialAttackState);
                break;
            case AttackId.JumpSlash:
                stateMachine.ChangeState(jumpSlashState);
                break;
            case AttackId.Atk3:
                stateMachine.ChangeState(atk3State);
                break;
        }
    }

    // ── Attack Indicator ─────────────────────────────────────────────
    public void ShowIndicator(float duration)
    {
        if (!showAttackIndicator || indicatorRenderer == null) return;
        if (indicatorRoutine != null) StopCoroutine(indicatorRoutine);
        indicatorRoutine = StartCoroutine(IndicatorRoutine(duration));
    }

    IEnumerator IndicatorRoutine(float duration)
    {
        indicatorRenderer.gameObject.SetActive(true);
        indicatorRenderer.transform.localScale = new Vector3(indicatorSize.x, indicatorSize.y, 1f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            Vector3 worldOffset = new Vector3(
                indicatorOffset.x * FacingDir,
                indicatorOffset.y,
                0f
            );
            indicatorRenderer.transform.position = transform.position + worldOffset;

            float t = elapsed / duration;
            indicatorRenderer.color = Color.Lerp(indicatorWarningColor, indicatorImpactColor, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        indicatorRenderer.gameObject.SetActive(false);
        indicatorRoutine = null;
    }

    // ── Gizmos ───────────────────────────────────────────────────────
    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();   // 父类画 detection / attack range

        // 每个攻击的 hitbox（按 showGizmo flag）
        if (hitboxes != null)
            foreach (var hb in hitboxes) DrawHitboxGizmo(hb);

        if (showAttackIndicator)
        {
            int dir = FacingDir;
            Vector3 center = transform.position + new Vector3(indicatorOffset.x * dir, indicatorOffset.y, 0f);
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.4f);
            Gizmos.DrawCube(center, new Vector3(indicatorSize.x, indicatorSize.y, 0.1f));
            Gizmos.color = new Color(1f, 0.4f, 0f, 1f);
            Gizmos.DrawWireCube(center, new Vector3(indicatorSize.x, indicatorSize.y, 0.1f));
        }
    }
}
