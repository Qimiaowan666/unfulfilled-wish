using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerStats))]
public class PlayerController : Entity
{
    public static PlayerController Instance { get; private set; }   // 常驻单例:贯穿所有游玩场景,数据天然不丢

    Coroutine queuedAttackCo;

    // ── State Machine ────────────────────────────────────────────────
    public PlayerStateMachine  stateMachine  { get; private set; }
    public Player_IdleState    idleState     { get; private set; }
    public Player_MoveState    moveState     { get; private set; }
    public Player_JumpState    jumpState     { get; private set; }
    public Player_FallState    fallState     { get; private set; }
    public Player_DashState    dashState     { get; private set; }
    public Player_AttackState  attackState   { get; private set; }
    public Player_BlockState   blockState    { get; private set; }
    public Player_CounterState counterState  { get; private set; }
    public Player_ExecuteState executeState  { get; private set; }
    public Player_StunnedState stunnedState  { get; private set; }
    public Player_KnockedState knockedState  { get; private set; }
    public Player_DashStrikeState dashStrikeState { get; private set; }
    public Player_HealState    healState     { get; private set; }
    public Player_DeadState    deadState     { get; private set; }

    // 各状态收尾统一回归：落地 → idle，半空 → fall
    public PlayerBaseState GroundedOrFall => IsGrounded ? (PlayerBaseState)idleState : fallState;

    // ── Player-specific Components ───────────────────────────────────
    public PlayerStats         Stats        { get; private set; }
    public PlayerInputBuffer   InputBuffer  { get; private set; }
    public PlayerInput         Input        { get; private set; }
    public Player_SkillManager SkillManager { get; private set; }   // 子节点 "Skills" 上挂

    // ── Cooldown Timers ──────────────────────────────────────────────
    public float dashCooldownTimer    { get; private set; }
    public float counterCooldownTimer { get; private set; }

    // ── Inspector Config ─────────────────────────────────────────────
    [Header("Movement")]
    public float moveSpeed = 6f;
    [Tooltip("空中移速 = moveSpeed × 此系数(跳跃/下落时的左右移动)")]
    public float airMoveSpeedMultiplier = 0.8f;

    [Header("Jump")]
    public float jumpForce = 12f;

    [Header("Dash")]
    public float dashSpeed    = 20f;
    public float dashDuration = 0.15f;
    public float dashCooldown = 1f;

    [Header("Attack")]
    public int   maxComboStep   = 3;
    public float comboResetTime = 0.8f;
    public float[] attackDurations         = { 0.3f, 0.3f, 0.4f };
    public float[] attackDamageMultipliers = { 1f,   1f,   1.2f };
    public float[] attackPoiseDamage       = { 8f,   10f,  14f  };
    public Vector2  hitboxOffset = new Vector2(0.6f, 0f);
    public Vector2  hitboxSize   = new Vector2(0.8f, 0.6f);
    public bool     showAttackHitbox = true;   // ⬛ 显示/隐藏 本体普攻 hitbox Gizmo
    public LayerMask enemyLayer;

    [Header("Block")]
    public float perfectBlockWindow          = 0.15f;   // 完美格挡基础窗口(每次按下开窗)
    public float perfectBlockMinWindow        = 0.03f;   // 连续狂按时窗口下限(防刷)
    public float perfectBlockShrinkPerPress   = 0.05f;   // 每次近期重按缩小的窗口
    public float perfectBlockPenaltyResetTime = 0.5f;    // 静默多久惩罚清零(成功精防也会清零)

    [Header("Counter")]
    public float counterWindow      = 0.3f;
    public float counterCooldown    = 0.5f;
    public float counterPoiseDamage = 60f;

    [Header("Execute")]
    public float executeDamageMultiplier = 5f;
    public float executeDuration         = 0.8f;
    public float executeRange            = 1.5f;

    // ── External Queries ─────────────────────────────────────────────
    public bool IsBlocking           => stateMachine.currentState == blockState;
    public bool IsCountering         => stateMachine.currentState == counterState;
    public bool InPerfectBlockWindow => IsBlocking && blockState.InPerfectWindow;

    public bool TryCounter(EnemyBase enemy)   => counterState.TryCounter(enemy);
    public void ReceiveBlockHit(float damage, EnemyBase attacker) { if (IsBlocking) blockState.ReceiveAttack(damage, attacker); }

    // ── Lifecycle ────────────────────────────────────────────────────
    protected override void Awake()
    {
        if (Instance != null && Instance != this)   // 新场景里的重复玩家 → 把常驻玩家挪到这个出生点后自毁
        {
            Instance.transform.position = transform.position;            // 默认落点 = 本场景出生点(随后传送门/读档会再覆盖)
            if (Instance.Rb != null) Instance.Rb.linearVelocity = Vector2.zero;
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoadedCleanup;

        base.Awake();
        Stats       = GetComponent<PlayerStats>();
        InputBuffer = GetComponent<PlayerInputBuffer>();
        if (InputBuffer == null) InputBuffer = gameObject.AddComponent<PlayerInputBuffer>();
        Input = GetComponent<PlayerInput>();
        if (Input == null) Input = gameObject.AddComponent<PlayerInput>();

        stateMachine  = new PlayerStateMachine();
        idleState     = new Player_IdleState(this, stateMachine);
        moveState     = new Player_MoveState(this, stateMachine);
        jumpState     = new Player_JumpState(this, stateMachine);
        fallState     = new Player_FallState(this, stateMachine);
        dashState     = new Player_DashState(this, stateMachine);
        attackState   = new Player_AttackState(this, stateMachine);
        blockState    = new Player_BlockState(this, stateMachine);
        counterState  = new Player_CounterState(this, stateMachine);
        executeState  = new Player_ExecuteState(this, stateMachine);
        stunnedState  = new Player_StunnedState(this, stateMachine);
        knockedState  = new Player_KnockedState(this, stateMachine);
        dashStrikeState = new Player_DashStrikeState(this, stateMachine);
        healState     = new Player_HealState(this, stateMachine);
        deadState     = new Player_DeadState(this, stateMachine);

        SkillManager = GetComponentInChildren<Player_SkillManager>();   // 可空（还没建 Skills 子节点时）

        stateMachine.Initialize(idleState);
        Stats.OnDeath += () => stateMachine.ChangeState(deadState);
    }

    protected override void Update()
    {
        if (stateMachine == null) return;   // 未初始化(自毁中的重复实例)兜底,防空引用
        base.Update();
        if (dashCooldownTimer    > 0f) dashCooldownTimer    -= Time.deltaTime;
        if (counterCooldownTimer > 0f) counterCooldownTimer -= Time.deltaTime;
        stateMachine.Update();
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoadedCleanup;
    }

    // 回主菜单 / Bootstrap → 销毁常驻玩家(新游戏/继续会在游玩场景重新生成,保证全新状态)
    void OnSceneLoadedCleanup(Scene s, LoadSceneMode m)
    {
        if (SceneNames.IsNonGameplay(s.name)) { Destroy(gameObject); return; }
        // 常驻玩家带着死亡态进了新场景(重生/读档)→ 解死复活(HP/死标已由 SaveSystem.LoadSavedVitals 复原)
        if (stateMachine != null && stateMachine.currentState == deadState) Revive();
    }

    // 复活:解开死亡态(恢复物理 + 解状态机锁),回到 idle。deadState.Exit 会自动清掉 isDead 动画。
    public void Revive()
    {
        if (Rb != null) { Rb.simulated = true; Rb.linearVelocity = Vector2.zero; }
        stateMachine.Unlock();
        stateMachine.ChangeState(idleState);
    }

    // ── Called by State Classes ──────────────────────────────────────
    public void StartDashCooldown()    => dashCooldownTimer    = dashCooldown;
    public void StartCounterCooldown() => counterCooldownTimer = counterCooldown;

    public void EnterAttackStateWithDelay()
    {
        if (queuedAttackCo != null) StopCoroutine(queuedAttackCo);
        queuedAttackCo = StartCoroutine(EnterAttackStateWithDelayCo());
    }

    IEnumerator EnterAttackStateWithDelayCo()
    {
        yield return new WaitForEndOfFrame();
        stateMachine.ChangeState(attackState);
        queuedAttackCo = null;
    }

    public void Stun(float duration)
    {
        stunnedState.Duration = duration;
        stateMachine.ChangeState(stunnedState);
    }

    // ── 被挑起：跟随 boss 升空/下落，但不锁状态机 → 空中仍可格挡/识破/翻滚挣脱 ──
    public bool IsLifted { get; private set; }

    public void BeginLift()
    {
        if (IsLifted) return;
        IsLifted = true;
        Rb.bodyType       = RigidbodyType2D.Kinematic;   // 关重力，由 boss 控位移
        Rb.linearVelocity = Vector2.zero;
    }

    public void SetLiftPosition(Vector2 pos)
    {
        if (IsLifted) transform.position = pos;
    }

    public void EndLift()
    {
        if (!IsLifted) return;
        IsLifted = false;
        Rb.bodyType = RigidbodyType2D.Dynamic;           // 恢复重力，自然落地
    }

    // ── Called by PlayerAnimationEvents ─────────────────────────────
    public void AnimFinished()      => stateMachine.currentState?.OnAnimationFinished();
    public void AnimHitFrame()      => stateMachine.currentState?.OnHitFrame();
    public void AnimCounterClosed() => stateMachine.currentState?.OnCounterWindowClosed();

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();
        if (!showAttackHitbox) return;
        float dir = FacingRight ? 1f : -1f;
        Vector2 origin = (Vector2)transform.position +
                         new Vector2(hitboxOffset.x * dir, hitboxOffset.y);
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.5f);
        Gizmos.DrawCube(origin, hitboxSize);
        Gizmos.color = new Color(1f, 0.3f, 0f, 1f);
        Gizmos.DrawWireCube(origin, hitboxSize);
    }
}
