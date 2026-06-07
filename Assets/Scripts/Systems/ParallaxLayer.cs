using UnityEngine;

// 视差层：根据相机移动按 parallaxFactor 决定这层跟着走多少
// 挂在每个背景 GameObject 上（不是 Tilemap，是单张 SpriteRenderer 那种）
// parallaxFactor 越小 = 越远（屏幕里滚得越慢）
// parallaxFactor 越大 = 越近（接近 1 = 跟地面一样）
public class ParallaxLayer : MonoBehaviour
{
    [Header("Parallax")]
    [Range(0f, 1.5f)]
    [Tooltip("0=屏幕里完全不动(看起来无穷远); 1=跟地面一样滚(无视差); >1=前景比地面快")]
    public float parallaxFactor = 0.5f;

    [Tooltip("Y 方向也要视差吗？横版一般不勾")]
    public bool parallaxY = false;

    Transform camTransform;
    Vector3   lastCamPos;

    void Start()
    {
        ResolveCamera();
        if (camTransform != null) lastCamPos = camTransform.position;
    }

    void LateUpdate()
    {
        if (camTransform == null) { ResolveCamera(); if (camTransform == null) return; lastCamPos = camTransform.position; }

        Vector3 delta = camTransform.position - lastCamPos;
        float dx = delta.x * (1f - parallaxFactor);
        float dy = parallaxY ? delta.y * (1f - parallaxFactor) : 0f;
        transform.position += new Vector3(dx, dy, 0f);
        lastCamPos = camTransform.position;
    }

    void ResolveCamera()
    {
        if (camTransform != null) return;
        if (Camera.main != null) camTransform = Camera.main.transform;
    }
}
