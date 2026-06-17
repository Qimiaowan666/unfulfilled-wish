using System.Collections;
using UnityEngine;

// 全屏放射状吼叫特效（漫画集中线 / 爆发）：贴在相机上、以某世界点（boss）为中心的全屏覆盖层，
// 吼叫瞬间从中心向外爆开 → 淡出。在世界之上、HUD（Screen Space Overlay）之下，与参考一致。
// 懒加载常驻单例（首次 Burst 自动生成），用 unscaledDeltaTime 播放，顿帧/慢放里也照常运行。
// 放射线贴图为程序化生成，放在 Resources: Vfx/RoarLines。
public class ScreenRoarFx : MonoBehaviour
{
    static ScreenRoarFx _inst;
    static ScreenRoarFx Inst
    {
        get
        {
            if (_inst == null)
            {
                var go = new GameObject("ScreenRoarFx");
                _inst = go.AddComponent<ScreenRoarFx>();
                DontDestroyOnLoad(go);
            }
            return _inst;
        }
    }

    void Awake()
    {
        if (_inst != null && _inst != this) { Destroy(gameObject); return; }
        _inst = this;
    }

    Sprite linesSprite;
    Sprite Lines => linesSprite != null ? linesSprite : (linesSprite = Resources.Load<Sprite>("Vfx/RoarLines"));

    // focusWorld：放射中心（一般传 boss 中心）；null = 屏幕中心。duration 每次脉冲时长；peakAlpha 峰值不透明度。
    // pulses：连续放几次（每次随机起始角度，线条各不相同，错峰叠成滚动的集中线风暴）；pulseInterval 两次间隔。
    public static void Burst(Vector3? focusWorld = null, float duration = 0.7f, float peakAlpha = 0.9f,
                             Color? tint = null, int pulses = 4, float pulseInterval = 0.22f)
    {
        var s = Inst;
        if (s != null) s.StartCoroutine(s.PulsesRoutine(focusWorld, duration, peakAlpha, tint ?? Color.white, Mathf.Max(1, pulses), pulseInterval));
    }

    IEnumerator PulsesRoutine(Vector3? focusWorld, float duration, float peakAlpha, Color tint, int pulses, float interval)
    {
        for (int p = 0; p < pulses; p++)
        {
            StartCoroutine(BurstRoutine(focusWorld, duration, peakAlpha, tint, Random.Range(0f, 360f)));
            if (p < pulses - 1) yield return new WaitForSecondsRealtime(interval);
        }
    }

    IEnumerator BurstRoutine(Vector3? focusWorld, float duration, float peakAlpha, Color tint, float baseRot)
    {
        var sprite = Lines;
        var cam = Camera.main;
        if (sprite == null || cam == null) yield break;

        var go = new GameObject("ScreenRoar_Lines");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerID = TopSortingLayer();
        sr.sortingOrder = 30050;   // 世界之上；HUD 是 Overlay Canvas，仍在其上

        // 贴在相机平面（z=1），中心对齐 boss 的屏幕位置 → 放射线从 boss 发出。
        go.transform.SetParent(cam.transform, false);
        float worldH = (cam.orthographic ? cam.orthographicSize : 8f) * 2f;
        float worldW = worldH * cam.aspect;
        Vector2 offset = Vector2.zero;
        if (focusWorld.HasValue)
        {
            Vector3 vp = cam.WorldToViewportPoint(focusWorld.Value);
            offset = new Vector2((vp.x - 0.5f) * worldW, (vp.y - 0.5f) * worldH);
        }
        go.transform.localPosition = new Vector3(offset.x, offset.y, 1f);

        // 覆盖余量要足够大：中心偏到 boss 时，对角仍要盖满 → 取较大边 * 2.6
        float spriteSize = Mathf.Max(0.001f, sprite.bounds.size.x);
        float baseCover = Mathf.Max(worldW, worldH) * 2.6f / spriteSize;

        float fadeIn = Mathf.Min(0.08f, duration * 0.2f);
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);

            // alpha：极快进、缓出
            float a = (t < fadeIn ? t / fadeIn : 1f - (t - fadeIn) / Mathf.Max(0.0001f, duration - fadeIn));
            a = Mathf.Clamp01(a) * peakAlpha;

            // scale：从略小爆开到略大（线条向外扫）；以 boss 为中心缩放
            float zoom = Mathf.Lerp(0.82f, 1.4f, 1f - (1f - k) * (1f - k));   // ease-out
            float sc = baseCover * zoom;
            go.transform.localScale = new Vector3(sc, sc, 1f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, baseRot + k * 5f);   // 每次脉冲随机起始角 + 微旋

            var c = tint; c.a = a; sr.color = c;
            yield return null;
        }
        Destroy(go);
    }

    static int TopSortingLayer()
    {
        var layers = SortingLayer.layers;
        return layers.Length > 0 ? layers[layers.Length - 1].id : 0;
    }
}
