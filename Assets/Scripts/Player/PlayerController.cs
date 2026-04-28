using UnityEngine;

public enum PlayerState
{
    Idle,
    Running,
    Jumping,
    Falling,
    Dashing,
    Dodging,
    Attacking,
    Blocking,
    Countering,
    Executing,
    Stunned,
    Dead
}

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerStats))]
public class PlayerController : MonoBehaviour
{
    public PlayerState State { get; private set; } = PlayerState.Idle;

    public bool IsGrounded { get; private set; }
    public bool FacingRight { get; private set; } = true;
    public bool CanAct => State != PlayerState.Stunned && State != PlayerState.Dead && State != PlayerState.Executing;
    public bool CanMove => State == PlayerState.Idle || State == PlayerState.Running || State == PlayerState.Jumping || State == PlayerState.Falling;
    public bool CanStartAction => State == PlayerState.Idle || State == PlayerState.Running || State == PlayerState.Jumping || State == PlayerState.Falling;

    [Header("Ground Detection")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.1f;
    public LayerMask groundLayer;

    public Rigidbody2D Rb { get; private set; }
    public PlayerStats Stats { get; private set; }
    public Animator Animator { get; private set; }
    bool wasGrounded;

    void Awake()
    {
        Rb = GetComponent<Rigidbody2D>();
        Stats = GetComponent<PlayerStats>();
        Animator = GetComponent<Animator>();

        Stats.OnDeath += () => SetState(PlayerState.Dead);
    }

    void Update()
    {
        CheckGrounded();
        UpdateLandingState();
        UpdateFallState();
    }

    void CheckGrounded()
    {
        wasGrounded = IsGrounded;
        if (groundCheck == null) return;
        IsGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    void UpdateLandingState()
    {
        if (!IsGrounded) return;
        if (State != PlayerState.Falling && State != PlayerState.Jumping) return;

        bool justLanded = !wasGrounded || Mathf.Abs(Rb.linearVelocity.y) < 0.05f;
        if (justLanded) SetLocomotionState();
    }

    void UpdateFallState()
    {
        if (!IsGrounded && Rb.linearVelocity.y < -0.1f && State == PlayerState.Jumping)
            SetState(PlayerState.Falling);
    }

    public void SetState(PlayerState newState)
    {
        if (State == PlayerState.Dead && newState != PlayerState.Dead) return;
        if (State == newState) return;
        State = newState;
        if (Animator != null && Animator.runtimeAnimatorController != null)
            Animator.SetInteger("State", (int)newState);
    }

    public void SetLocomotionState()
    {
        SetState(Mathf.Abs(Rb.linearVelocity.x) > 0.05f ? PlayerState.Running : PlayerState.Idle);
    }

    public void SetAnimInteger(string name, int value)
    {
        if (Animator == null || Animator.runtimeAnimatorController == null) return;

        foreach (var parameter in Animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Int && parameter.name == name)
            {
                Animator.SetInteger(name, value);
                return;
            }
        }
    }

    public void SetAnimTrigger(string name)
    {
        if (Animator == null || Animator.runtimeAnimatorController == null) return;

        foreach (var parameter in Animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == name)
            {
                Animator.SetTrigger(name);
                return;
            }
        }
    }

    public void SetFacing(float horizontalInput)
    {
        if (horizontalInput > 0 && !FacingRight) Flip();
        else if (horizontalInput < 0 && FacingRight) Flip();
    }

    void Flip()
    {
        FacingRight = !FacingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
