using System.Collections;
using UnityEngine;

// 轻量一次性精灵动画：按帧播一遍 frames，播完销毁。用于尘土/小特效这种不值得上粒子系统的。
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteOneShot : MonoBehaviour
{
    public Sprite[] frames;
    public float    frameTime = 0.06f;

    SpriteRenderer sr;

    void Awake() => sr = GetComponent<SpriteRenderer>();

    void OnEnable() => StartCoroutine(Play());

    IEnumerator Play()
    {
        if (frames == null || frames.Length == 0) { Destroy(gameObject); yield break; }
        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] != null) sr.sprite = frames[i];
            yield return new WaitForSeconds(frameTime);
        }
        Destroy(gameObject);
    }
}
