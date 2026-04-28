using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

[RequireComponent(typeof(PlayerController))]
public class PlayerCounter : MonoBehaviour
{
    public float counterWindow = 0.3f;
    public float counterCooldown = 0.5f;
    public float poiseDamageOnSuccess = 60f;
    PlayerController controller;
    PlayerInputBuffer inputBuffer;
    float cooldownTimer;

    public bool IsCountering { get; private set; }

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        inputBuffer = GetComponent<PlayerInputBuffer>();
        if (inputBuffer == null) inputBuffer = gameObject.AddComponent<PlayerInputBuffer>();
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
        controller.SetState(PlayerState.Countering);
        IsCountering = true;

        yield return new WaitForSeconds(counterWindow);

        IsCountering = false;
        if (controller.IsGrounded) controller.SetLocomotionState();
        else controller.SetState(PlayerState.Falling);
    }

    public bool TryCounter(EnemyBase enemy)
    {
        if (!IsCountering) return false;

        var poise = enemy.GetComponent<PoiseMeter>();
        if (poise != null) poise.TakePoiseDamage(poiseDamageOnSuccess);

        return true;
    }
}
