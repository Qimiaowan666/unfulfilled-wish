using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

[RequireComponent(typeof(PlayerController))]
public class PlayerDodge : MonoBehaviour
{
    public float dodgeSpeed = 14f;
    public float dodgeDuration = 0.2f;
    public float dodgeCooldown = 1.5f;
    public float invincibleDuration = 0.15f;

    PlayerController controller;
    PlayerInputBuffer inputBuffer;
    Collider2D col;
    float cooldownTimer;

    public bool IsInvincible { get; private set; }

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        inputBuffer = GetComponent<PlayerInputBuffer>();
        if (inputBuffer == null) inputBuffer = gameObject.AddComponent<PlayerInputBuffer>();
        col = GetComponent<Collider2D>();
    }

    void Update()
    {
        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;

        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame)
            inputBuffer.Queue(BufferedPlayerAction.Dodge, 0.22f);

        if (!inputBuffer.IsBuffered(BufferedPlayerAction.Dodge)) return;
        if (!controller.CanStartAction) return;
        if (cooldownTimer > 0f) return;

        inputBuffer.TryConsume(BufferedPlayerAction.Dodge);
        StartCoroutine(DodgeRoutine());
    }

    IEnumerator DodgeRoutine()
    {
        cooldownTimer = dodgeCooldown;
        controller.SetState(PlayerState.Dodging);
        IsInvincible = true;

        float dir = GetDodgeDirection();
        controller.SetFacing(dir);
        controller.Rb.linearVelocity = new Vector2(dir * dodgeSpeed, controller.Rb.linearVelocity.y);

        yield return new WaitForSeconds(invincibleDuration);
        IsInvincible = false;

        yield return new WaitForSeconds(dodgeDuration - invincibleDuration);

        controller.Rb.linearVelocity = new Vector2(0f, controller.Rb.linearVelocity.y);
        if (controller.IsGrounded) controller.SetLocomotionState();
        else controller.SetState(PlayerState.Falling);
    }

    float GetDodgeDirection()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) return -1f;
            if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) return 1f;
        }

        return controller.FacingRight ? 1f : -1f;
    }
}
