using UnityEngine;

// 通用 Sprite 帧动画：挂在自动生成的 GameObject 上，按 fps 循环播放 frames
// 用法：new GameObject → AddComponent<Vfx_SpriteAnim>().Init(...)
// 销毁由外部控制（例：Player_HealState.Exit 时 Destroy）
public class Vfx_SpriteAnim : MonoBehaviour
{
    Sprite[]       frames;
    float          fps;
    float          elapsed;
    int            currentIndex;
    SpriteRenderer sr;

    public void Init(Sprite[] frames, float fps, Color tint, string sortingLayer, int sortingOrder)
    {
        this.frames = frames;
        this.fps    = Mathf.Max(0.01f, fps);

        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.color           = tint;
        sr.sortingLayerName = string.IsNullOrEmpty(sortingLayer) ? "Default" : sortingLayer;
        sr.sortingOrder    = sortingOrder;
        if (frames != null && frames.Length > 0) sr.sprite = frames[0];
    }

    void Update()
    {
        if (frames == null || frames.Length == 0) return;
        elapsed += Time.deltaTime;
        float frameTime = 1f / fps;
        while (elapsed >= frameTime)
        {
            elapsed -= frameTime;
            currentIndex = (currentIndex + 1) % frames.Length;
            sr.sprite = frames[currentIndex];
        }
    }
}
