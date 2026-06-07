using UnityEngine;

// 残影：复制当前 sprite 放在某点，alpha 渐隐后自销毁
// 用法：new GameObject → AddComponent<Vfx_Afterimage>() → Init(sprite, scale, tintColor, duration)
public class Vfx_Afterimage : MonoBehaviour
{
    Color baseColor;
    float duration;
    float elapsed;
    SpriteRenderer sr;

    // 传入 source（通常是 player 的 SpriteRenderer）：自动抄 sprite + scale + sortingLayer + sortingOrder
    // 这样残影一定跟玩家在同一渲染层，不会被背景压下去
    public void Init(SpriteRenderer source, Color tint, float duration)
    {
        this.baseColor = tint;
        this.duration  = duration;

        transform.localScale = source.transform.localScale;
        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite          = source.sprite;
        sr.color           = tint;
        sr.flipX           = source.flipX;            // 抄翻转（玩家用 SpriteRenderer.flipX + transform.scale 双层翻转）
        sr.flipY           = source.flipY;
        sr.sortingLayerID  = source.sortingLayerID;   // 抄玩家的排序层
        sr.sortingOrder    = source.sortingOrder + 1; // 略高于玩家，避免被背景压下去
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        if (elapsed >= duration) { Destroy(gameObject); return; }

        Color c = baseColor;
        c.a = baseColor.a * (1f - elapsed / duration);
        sr.color = c;
    }
}
