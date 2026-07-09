using System.Collections;
using UnityEngine;

// 全屏放射状吼叫特效(漫画集中线 / 爆发):贴在相机上、以某世界点(boss)为中心的全屏覆盖层,
// 吼叫瞬间从中心向外爆开 → 淡出。在世界之上、HUD(Screen Space Overlay)之下。
// 放射线由 RoarLines shader 程序化生成(材质 roarMat 字段拖 RoarLinesMat),代码只喂中心/角度/透明度。
// 常驻单例(摆在 Bootstrap.unity 的 FxSystems 上),用 unscaledDeltaTime 播放,顿帧/慢放里也照常运行。
public class ScreenRoarFx : MonoBehaviour
{
    static ScreenRoarFx _inst;

    // RoarLines 材质模板(拖 RoarLinesMat;每波克隆一个实例 → 各自独立动画不打架)。
    // 摆在 Bootstrap 的 FxSystems 上,Awake 注册 + DontDestroyOnLoad,不再自动创建、不再走 Resources。
    [Tooltip("RoarLines 材质模板(拖 RoarLinesMat)")]
    public Material roarMat;

    void Awake()
    {
        if (_inst != null && _inst != this) { Destroy(gameObject); return; }
        _inst = this;
        DontDestroyOnLoad(gameObject);
    }

    // 承载 shader 的全屏 quad:一张【真 1x1】白贴图做的 sprite,提供几何 + 完整 0~1 UV(shader 靠 UV 算放射线,不采样这张贴图)。
    // 注意:不能用 Texture2D.whiteTexture(它是 4x4,截 Rect(0,0,1,1) 会得到 UV 只有 [0,0.25] → 放射中心落在 UV 外 → 变平行斜带)。
    Sprite quadSprite;
    Sprite QuadSprite
    {
        get
        {
            if (quadSprite == null)
            {
                var tex = new Texture2D(1, 1);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                quadSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);   // rect=整张 → UV 0~1
            }
            return quadSprite;
        }
    }

    // focusWorld:放射中心(一般传 boss 中心);null = 屏幕中心。duration 每波时长;peakAlpha 峰值不透明度。
    // pulses:连放几波(每波随机旋转角,错峰叠成滚动的集中线风暴);pulseInterval 两波间隔。
    public static void Burst(Vector3? focusWorld = null, float duration = 0.7f, float peakAlpha = 0.9f,
                             Color? tint = null, int pulses = 7, float pulseInterval = 0.22f)
    {
        if (_inst != null) _inst.StartCoroutine(_inst.PulsesRoutine(focusWorld, duration, peakAlpha, tint ?? Color.black, Mathf.Max(1, pulses), pulseInterval));
    }

    IEnumerator PulsesRoutine(Vector3? focusWorld, float duration, float peakAlpha, Color tint, int pulses, float interval)
    {
        for (int p = 0; p < pulses; p++)
        {
            StartCoroutine(BurstRoutine(focusWorld, duration, peakAlpha, tint, Random.Range(0f, Mathf.PI * 2f)));
            if (p < pulses - 1) yield return new WaitForSecondsRealtime(interval);
        }
    }

    IEnumerator BurstRoutine(Vector3? focusWorld, float duration, float peakAlpha, Color tint, float baseAngle)
    {
        var tmpl = roarMat;
        var cam  = Camera.main;
        if (tmpl == null || cam == null) yield break;

        var go = new GameObject("ScreenRoar_Lines");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = QuadSprite;
        sr.sortingLayerID = TopSortingLayer();
        sr.sortingOrder = 30050;                 // 世界之上;HUD 是 Overlay Canvas 仍在其上
        var mat = new Material(tmpl);            // 克隆:每波各自动画
        sr.material = mat;

        // 方形 quad 贴在相机正前方 depth 处,边长取屏幕较大边 → 盖满屏 + 放射线保持圆形(不被 16:9 拉椭圆)。
        // 屏幕世界尺寸用 ViewportToWorldPoint 求(正交/透视都对);中心用 WorldToViewportPoint(同样两种投影都对)。
        go.transform.SetParent(cam.transform, false);
        const float depth = 1f;
        Vector3 bl = cam.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
        Vector3 tr = cam.ViewportToWorldPoint(new Vector3(1f, 1f, depth));
        float screenW = Mathf.Abs(tr.x - bl.x);
        float screenH = Mathf.Abs(tr.y - bl.y);
        float cover   = Mathf.Max(screenW, screenH);
        go.transform.localPosition = new Vector3(0f, 0f, depth);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale    = new Vector3(cover, cover, 1f);

        // _Center:boss 屏幕视口位置(0~1)→ 换算到方形 quad 的 UV(quad 比屏幕高,y 要按 screenH/cover 压缩)→ 放射线从 boss 炸开
        Vector2 center = new Vector2(0.5f, 0.5f);
        if (focusWorld.HasValue)
        {
            Vector3 vp = cam.WorldToViewportPoint(focusWorld.Value);
            center = new Vector2(vp.x, 0.5f + (vp.y - 0.5f) * (screenH / cover));
        }
        mat.SetVector("_Center", center);
        mat.SetFloat("_Angle", baseAngle);       // 每波随机旋转,叠出风暴感
        mat.SetColor("_Tint", tint);

        // 淡入极快、淡出缓(和原节奏一致);unscaledDeltaTime → 顿帧/慢放里照常跑
        float t = 0f;
        float fadeIn = Mathf.Min(0.08f, duration * 0.2f);
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = (t < fadeIn) ? t / fadeIn : 1f - (t - fadeIn) / Mathf.Max(0.0001f, duration - fadeIn);
            mat.SetFloat("_Alpha", Mathf.Clamp01(a) * peakAlpha);
            yield return null;
        }

        Destroy(go);
        Destroy(mat);
    }

    static int TopSortingLayer()
    {
        var layers = SortingLayer.layers;
        return layers.Length > 0 ? layers[layers.Length - 1].id : 0;
    }
}
