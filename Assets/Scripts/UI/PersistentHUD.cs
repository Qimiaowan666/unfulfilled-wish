using UnityEngine;

// 让 PlayerHUD_Canvas 跨场景常驻、全局唯一（挂在 Bootstrap 的 PlayerHUD_Canvas 根上）。
// HUD 内部 PlayerHealthBarUI / BossHealthBarUI 会在每次 sceneLoaded 重新绑定当前场景的玩家/boss。
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
