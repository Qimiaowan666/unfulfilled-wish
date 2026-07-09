using UnityEngine;

// 让 PlayerHUD_Canvas 跨场景常驻、全局唯一（挂在 Bootstrap 的 PlayerHUD_Canvas 根上）。
// HUD 内部的 PlayerHealthBarUI 每次 sceneLoaded 重绑当前场景玩家;BossHealthBarUI 是场景级独立对象(不在本 HUD 内),靠 Start/SaveSystem.AfterApply/Update 自愈重绑。
// 仿 PersistentEventSystem。
public class PersistentHUD : MonoBehaviour
{
    static PersistentHUD instance;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }
}
