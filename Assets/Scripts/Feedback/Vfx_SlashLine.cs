using UnityEngine;

// 刀光：月牙弧形 LineRenderer，挂在玩家右侧（按 facingDir 翻向）
// 用 sin(πt) 鼓起 + widthCurve 中间宽两头尖 → 月牙形
// 跟随 follow 目标移动；alpha 渐隐后自销毁
public class Vfx_SlashLine : MonoBehaviour
{
    const int SEGMENTS = 24;   // 弧线采样点数（越多越圆滑）

    Color baseColor;
    float duration;
    float elapsed;
    LineRenderer lr;
    Transform    follow;
    Vector2      localCenter;   // 月牙竖向中心相对玩家的偏移（X=离开玩家距离）
    float        arcHeight;     // 竖直跨度（月牙两尖端的距离）
    float        bulge;         // 凸出量（月牙腰部相对两尖端再多突出多少）
    int          facingDir;

    public void Init(Transform follow, Vector2 localCenter, float arcHeight, float bulge, int facingDir,
                     Color color, float width, float duration)
    {
        this.follow      = follow;
        this.localCenter = localCenter;
        this.arcHeight   = arcHeight;
        this.bulge       = bulge;
        this.facingDir   = facingDir == 0 ? 1 : facingDir;
        this.baseColor   = color;
        this.duration    = duration;

        lr = gameObject.AddComponent<LineRenderer>();
        lr.material       = new Material(Shader.Find("Sprites/Default"));
        lr.positionCount  = SEGMENTS;
        lr.useWorldSpace  = true;

        // 月牙：中间粗两头尖
        lr.widthCurve = new AnimationCurve(
            new Keyframe(0f,   0.05f),
            new Keyframe(0.5f, 1f),
            new Keyframe(1f,   0.05f));
        lr.widthMultiplier = width;
        lr.numCapVertices  = 4;
        lr.startColor      = lr.endColor = color;
        lr.sortingOrder    = 10;

        UpdatePositions();
    }

    void UpdatePositions()
    {
        if (follow == null || lr == null) return;
        Vector3 o = follow.position;
        int n = lr.positionCount;
        for (int i = 0; i < n; i++)
        {
            float t  = (float)i / (n - 1);                       // 0..1
            float y  = Mathf.Lerp(-arcHeight * 0.5f, arcHeight * 0.5f, t);
            float xB = bulge * Mathf.Sin(t * Mathf.PI);           // 腰部最鼓
            float x  = (localCenter.x + xB) * facingDir;          // 凸面朝 facing 方向
            lr.SetPosition(i, o + new Vector3(x, localCenter.y + y, 0f));
        }
    }

    void Update()
    {
        UpdatePositions();

        elapsed += Time.deltaTime;
        if (elapsed >= duration) { Destroy(gameObject); return; }

        Color c = baseColor;
        c.a = baseColor.a * (1f - elapsed / duration);   // 线性淡出
        lr.startColor = lr.endColor = c;
    }
}
