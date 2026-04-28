using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerController))]
public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 6f;

    PlayerController controller;

    void Awake() => controller = GetComponent<PlayerController>();

    void Update()
    {
        if (!controller.CanMove) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        float h = 0f;
        if (kb.leftArrowKey.isPressed || kb.aKey.isPressed)  h = -1f;
        if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) h =  1f;

        controller.Rb.linearVelocity = new Vector2(h * moveSpeed, controller.Rb.linearVelocity.y);
        controller.SetFacing(h);

        if (controller.State == PlayerState.Idle || controller.State == PlayerState.Running)
            controller.SetState(h != 0f ? PlayerState.Running : PlayerState.Idle);
    }
}
