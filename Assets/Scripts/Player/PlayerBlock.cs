using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerController))]
public class PlayerBlock : MonoBehaviour
{
    public float perfectBlockWindow = 0.15f;
    PlayerController controller;
    float perfectBlockTimer;

    public bool IsBlocking => controller.State == PlayerState.Blocking;
    public bool InPerfectWindow => perfectBlockTimer > 0f;

    void Awake() => controller = GetComponent<PlayerController>();

    void Update()
    {
        if (perfectBlockTimer > 0f) perfectBlockTimer -= Time.deltaTime;

        var mouse = Mouse.current;
        if (mouse == null) return;

        bool holdingBlock = mouse.rightButton.isPressed && !mouse.leftButton.isPressed;

        if (holdingBlock && (controller.CanStartAction || controller.State == PlayerState.Blocking))
        {
            if (controller.State != PlayerState.Blocking)
            {
                controller.SetState(PlayerState.Blocking);
                perfectBlockTimer = perfectBlockWindow;
            }
        }
        else if (!holdingBlock && controller.State == PlayerState.Blocking)
        {
            if (controller.IsGrounded) controller.SetLocomotionState();
            else controller.SetState(PlayerState.Falling);
        }
    }

    public void ReceiveAttack(float damage)
    {
        if (!IsBlocking) return;

        if (InPerfectWindow)
        {
            controller.Stats.OnPerfectBlock();
            AudioManager.Instance?.PlayPerfectBlock();
            CameraShake.Instance?.Shake(0.08f, 0.03f);
        }
        else
        {
            controller.Stats.OnNormalBlock(damage);
            AudioManager.Instance?.PlayBlock();
            CameraShake.Instance?.Shake(0.06f, 0.025f);
        }
    }
}
