using UnityEngine;
using UnityEngine.UI;

// 液体条：配 UI/LiquidFill 材质。SetFill(目标进度) → 弹簧追随(欠阻尼=晃荡到新位置)。
// 液面波幅由"液面运动速度"驱动：动得快→波大，静止→几乎无波。
// 好处：突然受击/花体力会荡，而体力持续缓慢回复只有极轻涟漪，不会一直猛晃。
[RequireComponent(typeof(Image))]
public class LiquidBar : MonoBehaviour
{
    [Tooltip("弹簧刚度(追目标的力，越大越快到位)")]   public float stiffness = 80f;
    [Tooltip("弹簧阻尼(越小晃得越久，<2√k 才会过冲)")] public float damping   = 7f;
    [Tooltip("液面运动→波幅 换算(越大同样速度波越猛)")] public float velToWave = 0.3f;
    [Tooltip("波幅消退速度(运动停下后)")]              public float waveDecay = 6f;
    [Tooltip("波幅上限(占条厚度比例)")]                public float waveMax   = 0.7f;
    [Tooltip("常驻轻微波动(占条厚度比例)")]            public float idleWave  = 0.04f;

    Material mat;
    RectTransform rt;
    bool vertical;
    float current = 1f, target = 1f, vel, wave;

    void Awake()
    {
        rt = transform as RectTransform;
        var img = GetComponent<Image>();
        if (img != null && img.material != null)
        {
            mat = new Material(img.material);   // 每条独立一份 _Fill
            img.material = mat;
            vertical = mat.HasProperty("_Vertical") && mat.GetFloat("_Vertical") > 0.5f;
        }
    }

    // 设目标进度(0~1)。instant=true 直接到位不晃(初始化/读档用)。
    public void SetFill(float t, bool instant = false)
    {
        target = Mathf.Clamp01(t);
        if (instant) { current = target; vel = 0f; wave = 0f; Push(); }
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f) return;
        // 弹簧（欠阻尼 → 过冲晃荡）
        float force = (target - current) * stiffness - vel * damping;
        vel += force * dt;
        current += vel * dt;
        // 波幅 = 液面运动速度驱动；平滑衰减(取速度目标与衰减残留的较大者，避免抖)
        float wTarget = Mathf.Min(waveMax, Mathf.Abs(vel) * velToWave);
        wave = Mathf.Max(wTarget, wave * Mathf.Exp(-waveDecay * dt));
        Push();
    }

    void Push()
    {
        if (mat == null) return;
        mat.SetFloat("_Fill", Mathf.Clamp01(current));
        mat.SetFloat("_Wave", idleWave + wave);
        // 波幅按"厚度/长度"换算，宽扁条上波长才不会被拉成长尖刺
        if (rt != null)
        {
            float w = Mathf.Max(1f, rt.rect.width), h = Mathf.Max(1f, rt.rect.height);
            mat.SetFloat("_Aspect", vertical ? (w / h) : (h / w));
        }
    }
}
