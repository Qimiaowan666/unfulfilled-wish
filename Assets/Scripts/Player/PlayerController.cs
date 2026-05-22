using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerStats))]
public class PlayerController : Entity
{
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
    public Player_DeadState    deadState     { get; private set; }

    // ── Player-specific Components ───────────────────────────────────
    public PlayerStats       Stats       { get; private set; }
    public PlayerInputBuffer InputBuffer { get; private set; }
    public PlayerInput       Input       { get; private set; }

    // ── Cooldown Timers ──────────────────────────────────────────────
    public float dashCooldownTimer    { get; private set; }
    public float counterCooldownTimer { get; private set; }

    // ── Inspector Config ─────────────────────────────────────────────
    [Header("Movement")]
    public float moveSpeed = 6f;

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
    public LayerMask enemyLayer;

    [Header("Block")]
    public float perfectBlockWindow = 0.15f;

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
    public void ReceiveBlockHit(float damage) { if (IsBlocking) blockState.ReceiveAttack(damage); }

    // ── Lifecycle ────────────────────────────────────────────────────
    protected override void Awake()
    {
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
        deadState     = new Player_DeadState(this, stateMachine);

        stateMachine.Initialize(idleState);
        Stats.OnDeath += () => stateMachine.ChangeState(deadState);
    }

    protected override void Update()
    {
        base.Update();
        if (dashCooldownTimer    > 0f) dashCooldownTimer    -= Time.deltaTime;
        if (counterCooldownTimer > 0f) counterCooldownTimer -= Time.deltaTime;
        stateMachine.Update();
    }

    // ── Called by State Classes ──────────────────────────────────────
    public void StartDashCooldown()    => dashCooldownTimer    = dashCooldown;
    public void StartCounterCooldown() => counterCooldownTimer = counterCooldown;

    public void Stun(float duration)
    {
        stunnedState.Duration = duration;
        stateMachine.ChangeState(stunnedState);
    }

    // ── Called by PlayerAnimationEvents ─────────────────────────────
    public void AnimFinished()      => stateMachine.currentState?.OnAnimationFinished();
    public void AnimHitFrame()      => stateMachine.currentState?.OnHitFrame();
    public void AnimCounterClosed() => stateMachine.currentState?.OnCounterWindowClosed();

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();
        float dir = FacingRight ? 1f : -1f;
        Vector2 origin = (Vector2)transform.position +
                         new Vector2(hitboxOffset.x * dir, hitboxOffset.y);
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.5f);
        Gizmos.DrawCube(origin, hitboxSize);
        Gizmos.color = new Color(1f, 0.3f, 0f, 1f);
        Gizmos.DrawWireCube(origin, hitboxSize);
    }
}
