using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System;

[RequireComponent(typeof(PlayerController))]
public class PlayerCounter : MonoBehaviour
{
    public float counterWindow = 0.3f;
    public float counterCooldown = 0.5f;
    public float poiseDamageOnSuccess = 60f;
    PlayerController controller;
    PlayerInputBuffer inputBuffer;
    PlayerAnimationEvents animationEvents;
    float cooldownTimer;
    bool counterWindowClosed;
    bool actionFinished;

    public bool IsCountering { get; private set; }

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        inputBuffer = GetComponent<PlayerInputBuffer>();
        if (inputBuffer == null) inputBuffer = gameObject.AddComponent<PlayerInputBuffer>();

        animationEvents = GetComponent<PlayerAnimationEvents>();
        if (animationEvents == null) animationEvents = gameObject.AddComponent<PlayerAnimationEvents>();
        animationEvents.CounterWindowClosed += OnAnimationCounterWindowClosed;
        animationEvents.ActionFinished += OnAnimationFinish;
    }

    void OnDestroy()
    {
        if (animationEvents == null) return;
        animationEvents.CounterWindowClosed -= OnAnimationCounterWindowClosed;
        animationEvents.ActionFinished -= OnAnimationFinish;
    }

    void Update()
    {
        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;

        var mouse = Mouse.current;
        if (mouse == null) return;

        bool chordPressed =
            (mouse.leftButton.wasPressedThisFrame && mouse.rightButton.isPressed) ||
            (mouse.rightButton.wasPressedThisFrame && mouse.leftButton.isPressed);

        if (chordPressed)
            inputBuffer.Queue(BufferedPlayerAction.Counter, 0.2f);

        bool canCounter = controller.CanStartAction || controller.State == PlayerState.Blocking;
        if (!inputBuffer.IsBuffered(BufferedPlayerAction.Counter)) return;
        if (!canCounter) return;
        if (cooldownTimer > 0f) return;

        inputBuffer.TryConsume(BufferedPlayerAction.Counter);
        StartCoroutine(CounterRoutine());
    }

    IEnumerator CounterRoutine()
    {
        cooldownTimer = counterCooldown;
        counterWindowClosed = false;
        actionFinished = false;
        controller.SetState(PlayerState.Countering);
        IsCountering = true;

        yield return WaitForAnimationSignal(() => counterWindowClosed, counterWindow);

        IsCountering = false;
        yield return WaitForAnimationSignal(() => actionFinished, counterWindow);

        if (controller.IsGrounded) controller.SetLocomotionState();
        else controller.SetState(PlayerState.Falling);
    }

    IEnumerator WaitForAnimationSignal(Func<bool> signal, float fallbackSeconds)
    {
        float endTime = Time.time + Mathf.Max(0.05f, fallbackSeconds);
        while (!signal() && Time.time < endTime)
            yield return null;
    }

    void OnAnimationCounterWindowClosed()
    {
        if (!IsCountering) return;
        counterWindowClosed = true;
    }

    void OnAnimationFinish()
    {
        if (!IsCountering && !counterWindowClosed) return;
        actionFinished = true;
    }

    public bool TryCounter(EnemyBase enemy)
    {
        if (!IsCountering) return false;

        AudioManager.Instance?.PlayCounter();
        var poise = enemy.GetComponent<PoiseMeter>();
        if (poise != null) poise.TakePoiseDamage(poiseDamageOnSuccess);

        return true;
    }
}
