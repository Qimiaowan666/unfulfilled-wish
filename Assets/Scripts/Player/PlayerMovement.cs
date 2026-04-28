using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerController))]
public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 6f;

    PlayerController controller;
    float horizontalInput;

    void Awake() => controller = GetComponent<PlayerController>();

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        horizontalInput = 0f;
        if (kb.leftArrowKey.isPressed || kb.aKey.isPressed)  horizontalInput = -1f;
        if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) horizontalInput =  1f;
    }

    void FixedUpdate()
    {
        if (!controller.CanMove) return;

        controller.Rb.linearVelocity = new Vector2(horizontalInput * moveSpeed, controller.Rb.linearVelocity.y);
        controller.SetFacing(horizontalInput);

        if (controller.State == PlayerState.Idle || controller.State == PlayerState.Running)
            controller.SetState(horizontalInput != 0f ? PlayerState.Running : PlayerState.Idle);
    }
}
