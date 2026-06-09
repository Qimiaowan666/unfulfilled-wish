using UnityEngine;

// 简单循环帧动画：挂在 SpriteRenderer 物体上，Inspector 拖入 frames + 设 fps，自动循环播放
// 用于篝火、火把、旗帜等场景里循环动的装饰（不依赖 Animator）
[RequireComponent(typeof(SpriteRenderer))]
public class LoopedSpriteAnimation : MonoBehaviour
{
    [Tooltip("按顺序拖入切好的帧")]
    public Sprite[] frames;
    [Tooltip("帧率")]
    public float fps = 8f;

    SpriteRenderer sr;
    float timer;
    int index;

    void Awake() => sr = GetComponent<SpriteRenderer>();

    void Update()
    {
        if (frames == null || frames.Length == 0 || sr == null) return;

        timer += Time.deltaTime;
        float frameTime = 1f / Mathf.Max(0.01f, fps);
        while (timer >= frameTime)
        {
            timer -= frameTime;
            index = (index + 1) % frames.Length;
            sr.sprite = frames[index];
        }
    }
}
