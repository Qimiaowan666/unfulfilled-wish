using UnityEngine;

// 复活点可视化：在编辑器/运行时用半透明主角贴图标出复活落点，方便肉眼摆位与调试。
// showPreview 控制显隐；previewSprite 留空时自动抓场景里主角当前的贴图。
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class RespawnPointPreview : MonoBehaviour
{
    [Header("显示开关")]
    public bool showPreview = true;

    [Header("外观")]
    public Sprite  previewSprite;                                  // 留空 = 自动用主角当前贴图
    public Color   tint         = new Color(0.4f, 1f, 0.6f, 0.45f);// 半透明绿
    public Vector3 previewScale = new Vector3(4f, 4f, 1f);         // 对齐主角大小
    public int     sortingOrder = -5;                             // 压在主角后面，不挡视线

    SpriteRenderer sr;

    void OnEnable()   => Apply();
    void OnValidate() => Apply();

    void Apply()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr == null) return;

        sr.sprite              = previewSprite != null ? previewSprite : FindPlayerSprite();
        sr.color               = tint;
        sr.sortingOrder        = sortingOrder;
        sr.enabled             = showPreview;
        transform.localScale   = previewScale;
    }

    Sprite FindPlayerSprite()
    {
        var player = FindAnyObjectByType<PlayerController>();
        if (player == null) return null;
        var psr = player.GetComponentInChildren<SpriteRenderer>();
        return psr != null ? psr.sprite : null;
    }
}
