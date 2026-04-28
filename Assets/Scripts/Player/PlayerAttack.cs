using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

[RequireComponent(typeof(PlayerController))]
public class PlayerAttack : MonoBehaviour
{
    [Header("Combo")]
    public int maxComboStep = 3;
    public float comboResetTime = 0.8f;

    [Header("Hitbox")]
    public Vector2 hitboxOffset = new Vector2(0.6f, 0f);
    public Vector2 hitboxSize = new Vector2(0.8f, 0.6f);
    public LayerMask enemyLayer;

    [Header("Attack Timing")]
    public float[] attackDurations = { 0.3f, 0.3f, 0.4f };
    public float attackBufferTime = 0.35f;

    PlayerController controller;
    PlayerInputBuffer inputBuffer;
    int comboStep;
    float comboTimer;
    bool isAttacking;

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        inputBuffer = GetComponent<PlayerInputBuffer>();
        if (inputBuffer == null) inputBuffer = gameObject.AddComponent<PlayerInputBuffer>();
    }

    void Update()
    {
        if (comboTimer > 0f)
        {
            comboTimer -= Time.deltaTime;
            if (comboTimer <= 0f) comboStep = 0;
        }

        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame && !mouse.rightButton.isPressed)
            inputBuffer.Queue(BufferedPlayerAction.Attack, attackBufferTime);
    }

    void FixedUpdate()
    {
        if (!inputBuffer.IsBuffered(BufferedPlayerAction.Attack)) return;
        if (!controller.CanStartAction) return;
        if (isAttacking) return;

        inputBuffer.TryConsume(BufferedPlayerAction.Attack);
        StartCoroutine(AttackRoutine());
    }

    IEnumerator AttackRoutine()
    {
        isAttacking = true;
        controller.SetAnimInteger("AttackStep", comboStep + 1);
        controller.SetState(PlayerState.Attacking);
        controller.Rb.linearVelocity = new Vector2(0f, controller.Rb.linearVelocity.y);

        yield return new WaitForSeconds(attackDurations[comboStep] * 0.4f);
        DoHit();

        yield return new WaitForSeconds(attackDurations[comboStep] * 0.6f);

        comboStep = (comboStep + 1) % maxComboStep;
        comboTimer = comboResetTime;

        if (controller.IsGrounded) controller.SetLocomotionState();
        else controller.SetState(PlayerState.Falling);
        isAttacking = false;
    }

    void DoHit()
    {
        AudioManager.Instance?.PlayAttack();

        float dir = controller.FacingRight ? 1f : -1f;
        Vector2 origin = (Vector2)transform.position + new Vector2(hitboxOffset.x * dir, hitboxOffset.y);

        Collider2D[] hits = Physics2D.OverlapBoxAll(origin, hitboxSize, 0f, enemyLayer);
        foreach (var hit in hits)
        {
            var enemy = hit.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                enemy.TakeDamage(controller.Stats.attack);
                hit.GetComponent<DamageFeedback>()?.ApplyKnockback(transform.position, 4f);
                controller.Stats.OnAttackHit();
                AudioManager.Instance?.PlayHit();
            }
        }
    }

    void OnDrawGizmos()
    {
#if UNITY_EDITOR
        if (Application.isPlaying) return;

        Gizmos.color = new Color(1f, 0.15f, 0.1f, 0.95f);
        float dir = GetEditorFacingDirection();
        Vector2 origin = (Vector2)transform.position + new Vector2(hitboxOffset.x * dir, hitboxOffset.y);

        Vector3 lineStart = transform.position;
        Vector3 lineEnd = origin + new Vector2(hitboxSize.x * 0.5f * dir, 0f);
        Gizmos.DrawLine(lineStart, lineEnd);
        Gizmos.DrawWireCube(origin, hitboxSize);
#endif
    }

    float GetEditorFacingDirection()
    {
        if (controller == null) controller = GetComponent<PlayerController>();
        if (controller != null) return controller.FacingRight ? 1f : -1f;

        return transform.lossyScale.x >= 0f ? 1f : -1f;
    }
}
