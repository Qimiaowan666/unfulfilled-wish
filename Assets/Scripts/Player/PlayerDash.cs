using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System;

[RequireComponent(typeof(PlayerController))]
public class PlayerDash : MonoBehaviour
{
    public float dashSpeed = 20f;
    public float dashDuration = 0.15f;
    public float dashCooldown = 1f;

    PlayerController controller;
    PlayerInputBuffer inputBuffer;
    PlayerAnimationEvents animationEvents;
    float cooldownTimer;
    bool isDashing;
    bool actionFinished;

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        inputBuffer = GetComponent<PlayerInputBuffer>();
        if (inputBuffer == null) inputBuffer = gameObject.AddComponent<PlayerInputBuffer>();

        animationEvents = GetComponent<PlayerAnimationEvents>();
        if (animationEvents == null) animationEvents = gameObject.AddComponent<PlayerAnimationEvents>();
        animationEvents.ActionFinished += OnAnimationFinish;
    }

    void OnDestroy()
    {
        if (animationEvents == null) return;
        animationEvents.ActionFinished -= OnAnimationFinish;
    }

    void Update()
    {
        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;

        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame)
            inputBuffer.Queue(BufferedPlayerAction.Dash, 0.2f);

        if (!inputBuffer.IsBuffered(BufferedPlayerAction.Dash)) return;
        if (!controller.CanStartAction) return;
        if (cooldownTimer > 0f) return;

        inputBuffer.TryConsume(BufferedPlayerAction.Dash);
        StartCoroutine(DashRoutine());
    }

    IEnumerator DashRoutine()
    {
        cooldownTimer = dashCooldown;
        isDashing = true;
        actionFinished = false;
        controller.SetState(PlayerState.Dashing);

        float dir = GetDashDirection();
        controller.SetFacing(dir);
        controller.Rb.linearVelocity = new Vector2(dir * dashSpeed, controller.Rb.linearVelocity.y);

        yield return WaitForAnimationSignal(() => actionFinished, dashDuration * 1.5f);

        controller.Rb.linearVelocity = new Vector2(0f, controller.Rb.linearVelocity.y);
        if (controller.IsGrounded) controller.SetLocomotionState();
        else controller.SetState(PlayerState.Falling);

        isDashing = false;
    }

    IEnumerator WaitForAnimationSignal(Func<bool> signal, float fallbackSeconds)
    {
        float endTime = Time.time + Mathf.Max(0.05f, fallbackSeconds);
        while (!signal() && Time.time < endTime)
            yield return null;
    }

    void OnAnimationFinish()
    {
        if (!isDashing) return;
        actionFinished = true;
    }

    float GetDashDirection()
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
