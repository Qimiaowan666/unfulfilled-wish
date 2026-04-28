using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

[RequireComponent(typeof(PlayerController))]
public class PlayerExecute : MonoBehaviour
{
    public float executeDamageMultiplier = 5f;
    public float executeDuration = 0.8f;
    public LayerMask enemyLayer;

    PlayerController controller;
    PlayerInputBuffer inputBuffer;

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        inputBuffer = GetComponent<PlayerInputBuffer>();
        if (inputBuffer == null) inputBuffer = gameObject.AddComponent<PlayerInputBuffer>();
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
        Collider2D[] nearby = Physics2D.OverlapCircleAll(transform.position, 1.5f, enemyLayer);
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
        controller.SetState(PlayerState.Executing);

        enemy.OnExecuted(controller.Stats.attack * executeDamageMultiplier);

        yield return new WaitForSeconds(executeDuration);

        if (controller.IsGrounded) controller.SetLocomotionState();
        else controller.SetState(PlayerState.Falling);
    }
}
