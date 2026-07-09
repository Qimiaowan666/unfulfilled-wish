using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class BackgroundFit : MonoBehaviour
{
    Camera cam;
    SpriteRenderer sr;
    float lastSize = -1f, lastAspect = -1f;   // 上次适配用的相机正交尺寸/宽高比;变了才重算

    void Awake() { sr = GetComponent<SpriteRenderer>(); }

    void LateUpdate()
    {
        if (cam == null) cam = Camera.main;                 // 惰性缓存 + 自愈(切场景换了相机也能重新拿到)
        if (cam == null || sr.sprite == null) return;       // 切场景没相机的那帧 / 精灵没赋值 → 跳过,别 NRE 刷屏

        // 相机正交尺寸或宽高比变了(改分辨率 / boss 演出 zoom)→ 重新适配,避免留黑边或溢出;
        // 不变则不重算。旧版只在第一帧 fit 一次、永不刷新。
        if (!Mathf.Approximately(cam.orthographicSize, lastSize) || !Mathf.Approximately(cam.aspect, lastAspect))
        {
            lastSize   = cam.orthographicSize;
            lastAspect = cam.aspect;
            float height = lastSize * 2f;
            float width  = height * lastAspect;
            Vector2 spriteSize = sr.sprite.bounds.size;     // 精灵资产本地尺寸(不受 transform.scale 影响)
            transform.localScale = new Vector3(width / spriteSize.x, height / spriteSize.y, 1f);
        }

        transform.position = new Vector3(cam.transform.position.x, transform.position.y, transform.position.z);
    }
}
