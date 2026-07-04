using System.Collections;
using UnityEngine;

// 顿帧:命中瞬间把时间压到极慢一小会儿(realtime 计时),增强打击感。懒加载常驻单例。
// 用法:Hitstop.Do();  或 Hitstop.Do(0.12f, 0.04f);
public class Hitstop : MonoBehaviour
{
    static Hitstop inst;
    static bool busy;

    static Hitstop Inst
    {
        get
        {
            if (inst == null) { var go = new GameObject("Hitstop"); inst = go.AddComponent<Hitstop>(); DontDestroyOnLoad(go); }
            return inst;
        }
    }

    public static void Do(float duration = 0.1f, float scale = 0.04f)
    {
        if (busy) return;
        Inst.StartCoroutine(Inst.Run(duration, scale));
    }

    IEnumerator Run(float d, float scale)
    {
        if (Time.timeScale <= 0.001f) yield break;   // 已经暂停(弹窗/商店)就不顿
        busy = true;
        float prev = Time.timeScale;
        Time.timeScale = scale;
        yield return new WaitForSecondsRealtime(Mathf.Max(0.02f, d));
        // 还原前确认 timeScale 还停在"我们的顿帧值附近(0.001~0.041)":被暂停成 0 或被别的系统改过都不碰，
        // 避免顿帧期间开菜单(timeScale=0)被这里误还原成 prev=1 → 把暂停解掉。
        if (Time.timeScale > 0.001f && Time.timeScale <= scale + 0.001f) Time.timeScale = prev;
        busy = false;
    }
}
