using UnityEngine;
using UnityEngine.InputSystem;

// 通关门：boss 击破演出结束后由 LevelManager 调 Appear() 出现。
// 玩家靠近显示提示，按 F → 通关画面(VictoryUI)。默认隐藏。
// 读档时按“场上有没有存活 boss”自动收起/出现：门只放在 boss 场景，
//   · 读到未击破档(boss 复活) → Hide（修门残留、避免不打 boss 通关）
//   · 读到已击破档(boss 已死)   → Appear（修死 boss 无门导致卡死）
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

    void OnEnable()  { SaveSystem.AfterApply += OnSaveApplied; }
    void OnDisable() { SaveSystem.AfterApply -= OnSaveApplied; }

    // 读档/重生 apply 后：有存活 boss → 收起门；没有(已击破) → 出现门
    void OnSaveApplied()
    {
        if (BossAlive()) Hide();
        else             Appear();
    }

    bool BossAlive()
    {
        foreach (var e in Object.FindObjectsByType<EnemyBase>(FindObjectsSortMode.None))
            if (e != null && e.isBoss && e.gameObject.activeInHierarchy && e.CurrentHP > 0f)
                return true;
        return false;
    }

    public void Appear()
    {
        active = true;
        if (visual != null) visual.SetActive(true);
    }

    public void Hide()
    {
        active = false;
        if (visual != null) visual.SetActive(false);
        if (prompt != null) prompt.SetActive(false);
    }

    void Update()
    {
        if (!active) return;

        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag(Tags.Player);
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
