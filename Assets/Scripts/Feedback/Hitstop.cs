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
        if (Time.timeScale <= scale + 0.001f) Time.timeScale = prev;   // 期间没被别的(如暂停)改过才恢复
        busy = false;
    }
}
