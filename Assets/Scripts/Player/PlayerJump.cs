using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerController))]
public class PlayerJump : MonoBehaviour
{
    public float jumpForce = 12f;

    PlayerController controller;
    bool jumpPressed;

    void Awake() => controller = GetComponent<PlayerController>();

    void Update()
    {
        if (!controller.CanStartAction) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.spaceKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)
            jumpPressed = true;
    }

    void FixedUpdate()
    {
        if (!jumpPressed) return;
        jumpPressed = false;

        if (!controller.IsGrounded) return;
        if (!controller.CanStartAction) return;

        controller.Rb.linearVelocity = new Vector2(controller.Rb.linearVelocity.x, jumpForce);
        controller.SetState(PlayerState.Jumping);
        AudioManager.Instance?.PlayJump();
    }
}
