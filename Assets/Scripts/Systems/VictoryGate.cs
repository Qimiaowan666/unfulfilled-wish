using UnityEngine;
using UnityEngine.InputSystem;

// 通关门：boss 击破演出结束后由 LevelManager 调 Appear() 出现。
// 玩家靠近显示提示，按 F → 通关画面(VictoryUI)。默认隐藏。
public class VictoryGate : MonoBehaviour
{
    [Tooltip("门的视觉根（默认隐藏，Appear 时显示）")]
    public GameObject visual;
    [Tooltip("交互提示根（靠近时显示，可空）")]
    public GameObject prompt;
    public float interactRange = 2.8f;

    bool active;
    Transform player;

    void Awake()
    {
        if (visual != null) visual.SetActive(false);
        if (prompt != null) prompt.SetActive(false);
    }

    public void Appear()
    {
        active = true;
        if (visual != null) visual.SetActive(true);
    }

    void Update()
    {
        if (!active) return;

        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) return;
            player = p.transform;
        }

        bool inRange = Mathf.Abs(player.position.x - transform.position.x) <= interactRange &&
                       Mathf.Abs(player.position.y - transform.position.y) <= interactRange + 1.5f;
        if (prompt != null) prompt.SetActive(inRange);

        if (inRange && !VictoryUI.IsOpen &&
            Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            active = false;
            if (prompt != null) prompt.SetActive(false);
            VictoryUI.Instance?.Show();
        }
    }
}
