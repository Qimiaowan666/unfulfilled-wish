using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInput : MonoBehaviour
{
    public Vector2 MoveInput     { get; private set; }
    public bool    JumpPressed   { get; private set; }
    public bool    AttackPressed { get; private set; }
    public bool    BlockHeld     { get; private set; }
    public bool    DashPressed   { get; private set; }
    public bool    CounterPressed { get; private set; }
    public bool    ExecutePressed { get; private set; }
    public bool    Skill1Pressed  { get; private set; }   // Q：技能槽 1
    public bool    Skill2Pressed  { get; private set; }   // E：技能槽 2

    // 暂停 / 游戏结束 / 商店 / 角色面板 / 存读档菜单打开时，屏蔽全部玩家输入
    bool InputBlocked =>
        (GameManager.Instance != null && (GameManager.Instance.IsPaused || GameManager.Instance.IsGameOver)) ||
        ShopUI.IsOpen || CharacterPanelUI.IsOpen || PauseMenu.IsOpen || VictoryUI.IsOpen ||
        DialogueUI.IsPlaying || BossIntroTrigger.Sequencing || BossFinishUI.IsPlaying ||
        MinotaurBoss.PhaseTransition;

    void Update()
    {
        if (InputBlocked)
        {
            MoveInput = Vector2.zero;
            JumpPressed = AttackPressed = BlockHeld = DashPressed =
                CounterPressed = ExecutePressed = Skill1Pressed = Skill2Pressed = false;
            return;
        }

        var kb    = Keyboard.current;
        var mouse = Mouse.current;

        float h = 0f;
        if (kb != null)
        {
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h =  1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  h = -1f;
        }
        MoveInput = new Vector2(h, 0f);

        JumpPressed    = kb != null && (kb.spaceKey.wasPressedThisFrame ||
                                        kb.wKey.wasPressedThisFrame     ||
                                        kb.upArrowKey.wasPressedThisFrame);
        DashPressed    = kb != null && (kb.leftShiftKey.wasPressedThisFrame ||
                                        kb.rightShiftKey.wasPressedThisFrame);
        ExecutePressed = kb != null && kb.rKey.wasPressedThisFrame;   // R：处决
        Skill1Pressed  = kb != null && kb.qKey.wasPressedThisFrame;
        Skill2Pressed  = kb != null && kb.eKey.wasPressedThisFrame;

        bool leftDown  = mouse != null && mouse.leftButton.wasPressedThisFrame;
        bool rightDown = mouse != null && mouse.rightButton.wasPressedThisFrame;
        bool leftHeld  = mouse != null && mouse.leftButton.isPressed;
        bool rightHeld = mouse != null && mouse.rightButton.isPressed;

        AttackPressed  = leftDown  && !rightHeld;
        BlockHeld      = rightHeld && !leftHeld;
        CounterPressed = (leftDown && rightHeld) || (rightDown && leftHeld);
    }
}
