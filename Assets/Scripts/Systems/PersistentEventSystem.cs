using UnityEngine;
using UnityEngine.EventSystems;

// 让 EventSystem 全局唯一、跨场景常驻（业界标准：一个项目只该有一个 EventSystem）
// 挂在 Bootstrap 的 EventSystem 上。其它场景自带的 EventSystem 应删掉，统一用这个常驻的。
[RequireComponent(typeof(EventSystem))]
public class PersistentEventSystem : MonoBehaviour
{
    static PersistentEventSystem instance;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }
}
