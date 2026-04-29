using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System;

[RequireComponent(typeof(PlayerController))]
public class PlayerExecute : MonoBehaviour
{
    public float executeDamageMultiplier = 5f;
    public float executeDuration = 0.8f;
    public float executeRange = 1.5f;
    public LayerMask enemyLayer;

    PlayerController controller;
    PlayerInputBuffer inputBuffer;
    PlayerAnimationEvents animationEvents;
    bool isExecuting;
    bool hitFrameReached;
    bool actionFinished;
    bool executionApplied;
    EnemyBase pendingEnemy;

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        inputBuffer = GetComponent<PlayerInputBuffer>();
        if (inputBuffer == null) inputBuffer = gameObject.AddComponent<PlayerInputBuffer>();

        animationEvents = GetComponent<PlayerAnimationEvents>();
        if (animationEvents == null) animationEvents = gameObject.AddComponent<PlayerAnimationEvents>();
        animationEvents.HitFrameReached += OnAnimationHit;
        animationEvents.ActionFinished += OnAnimationFinish;
    }

    void OnDestroy()
    {
        if (animationEvents == null) return;
        animationEvents.HitFrameReached -= OnAnimationHit;
        animationEvents.ActionFinished -= OnAnimationFinish;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.fKey.wasPressedThisFrame)
            inputBuffer.Queue(BufferedPlayerAction.Execute, 0.22f);

        if (!inputBuffer.IsBuffered(BufferedPlayerAction.Execute)) return;
        if (!controller.CanStartAction) return;
        if (TryExecute()) inputBuffer.TryConsume(BufferedPlayerAction.Execute);
    }

    bool TryExecute()
    {
        Collider2D[] nearby = Physics2D.OverlapCircleAll(transform.position, executeRange, enemyLayer);
        foreach (var col in nearby)
        {
            var enemy = col.GetComponent<EnemyBase>();
            if (enemy != null && enemy.IsExecutable)
            {
                StartCoroutine(ExecuteRoutine(enemy));
                return true;
            }
        }

        return false;
    }

    IEnumerator ExecuteRoutine(EnemyBase enemy)
    {
        if (enemy == null || !enemy.IsExecutable) yield break;

        isExecuting = true;
        hitFrameReached = false;
        actionFinished = false;
        executionApplied = false;
        pendingEnemy = enemy;

        controller.SetState(PlayerState.Executing);
        controller.Stats.SetInvulnerable(true);
        AudioManager.Instance?.PlayExecuteDraw();

        yield return WaitForAnimationSignal(() => hitFrameReached, executeDuration * 0.65f);
        ApplyExecutionHit();

        yield return WaitForAnimationSignal(() => actionFinished, executeDuration * 1.2f);

        controller.Stats.SetInvulnerable(false);
        isExecuting = false;
        pendingEnemy = null;

        if (controller.IsGrounded) controller.SetLocomotionState();
        else controller.SetState(PlayerState.Falling);
    }

    IEnumerator WaitForAnimationSignal(Func<bool> signal, float fallbackSeconds)
    {
        float endTime = Time.time + Mathf.Max(0.05f, fallbackSeconds);
        while (!signal() && Time.time < endTime)
            yield return null;
    }

    void ApplyExecutionHit()
    {
        if (executionApplied) return;
        executionApplied = true;

        if (pendingEnemy != null && pendingEnemy.IsExecutable)
        {
            pendingEnemy.OnExecuted(controller.Stats.attack * executeDamageMultiplier);
            AudioManager.Instance?.PlayExecuteStrike();
        }
    }

    void OnAnimationHit()
    {
        if (!isExecuting) return;
        hitFrameReached = true;
    }

    void OnAnimationFinish()
    {
        if (!isExecuting) return;
        actionFinished = true;
    }

    void OnDisable()
    {
        if (!isExecuting || controller == null || controller.Stats == null) return;

        controller.Stats.SetInvulnerable(false);
        isExecuting = false;
        pendingEnemy = null;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, executeRange);
    }
}
