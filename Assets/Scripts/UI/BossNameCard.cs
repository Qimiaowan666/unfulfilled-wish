using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// boss 登场揭幕名牌（通用模板）：黑条展开 → 白罗马名淡入 → 大号红色中文名【逐字弹入】→ 停留 → 整体淡出。
// 内容由 BossProfile 数据驱动：Play(profile, hold) 时按 profile.romanSprite + nameChars[] 动态排版（任意字数）。
// 文字用烘焙 PNG 渲染，绕开中文 TMP 被字体材质乘成黑色的坑。模板挂在场景里（自带 Overlay Canvas）。
public class BossNameCard : MonoBehaviour
{
    [Header("模板引用")]
    public CanvasGroup group;          // 整体淡入淡出（根 CanvasGroup）
    public RectTransform bar;          // 黑条（localScale.x 0→1 展开）
    public Image romanImage;           // 罗马名 Image（运行时设 sprite）
    public CanvasGroup romanGroup;     // 罗马名淡入
    public RectTransform zhContainer;  // 中文名容器（运行时往里塞单字）

    [Header("布局")]
    public float charSize = 130f;      // 单字格边长
    public float charGap = 14f;        // 字间距
    public float romanHeight = 56f;    // 罗马名高度（宽度按 sprite 比例算）

    [Header("参数")]
    [Tooltip("黑条展开时长")]               public float barDur      = 0.25f;
    [Tooltip("罗马名淡入时长")]             public float romanDur    = 0.5f;
    [Tooltip("相邻两个字弹入的间隔")]        public float charStagger = 0.38f;
    [Tooltip("单字弹入时长")]               public float charPopDur  = 0.24f;
    [Tooltip("单字弹入起始放大倍数")]        public float charPopScale = 1.3f;
    [Tooltip("整体淡出时长")]               public float fadeOutDur  = 0.45f;

    readonly List<CanvasGroup> charGroups = new List<CanvasGroup>();
    readonly List<RectTransform> charRects = new List<RectTransform>();
    readonly List<GameObject> spawned = new List<GameObject>();

    void Awake()
    {
        if (group != null) { group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false; }
    }

    // profile = 当前 boss 的展示数据；hold = 从触发到开始淡出的总时长（一般传 roarDuration）。
    public void Play(BossProfile profile, float hold)
    {
        if (profile == null) return;
        StopAllCoroutines();
        Build(profile);
        StartCoroutine(Reveal(hold));
    }

    // 按 profile 动态构建罗马名 + 逐字
    void Build(BossProfile profile)
    {
        // 罗马名
        if (romanImage != null)
        {
            romanImage.sprite = profile.romanSprite;
            romanImage.enabled = profile.romanSprite != null;
            if (profile.romanSprite != null)
                romanImage.rectTransform.sizeDelta =
                    new Vector2(romanHeight * profile.romanSprite.rect.width / profile.romanSprite.rect.height, romanHeight);
        }

        // 清旧字格
        foreach (var go in spawned) if (go != null) Destroy(go);
        spawned.Clear(); charGroups.Clear(); charRects.Clear();

        var chars = profile.nameChars ?? new Sprite[0];
        int n = chars.Length;
        float totalW = n > 0 ? n * charSize + (n - 1) * charGap : 0f;
        if (zhContainer != null) zhContainer.sizeDelta = new Vector2(totalW, charSize);
        float startX = -totalW * 0.5f + charSize * 0.5f;

        for (int i = 0; i < n; i++)
        {
            var go = new GameObject("Zh_" + i, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(zhContainer, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(charSize, charSize);
            rt.anchoredPosition = new Vector2(startX + i * (charSize + charGap), 0f);
            var img = go.AddComponent<Image>();
            img.sprite = chars[i]; img.color = profile.nameColor; img.raycastTarget = false; img.preserveAspect = true;
            var cg = go.AddComponent<CanvasGroup>(); cg.alpha = 0f;
            spawned.Add(go); charGroups.Add(cg); charRects.Add(rt);
        }
    }

    IEnumerator Reveal(float hold)
    {
        if (group != null) group.alpha = 1f;
        if (bar != null)        bar.localScale = new Vector3(0f, 1f, 1f);
        if (romanGroup != null) romanGroup.alpha = 0f;
        ResetChars();

        // A) 黑条从中心展开
        yield return Anim(barDur, k => { float e = 1f - Mathf.Pow(1f - k, 3f); if (bar != null) bar.localScale = new Vector3(e, 1f, 1f); });

        // B) 罗马名淡入 + 中文逐字弹入（并行）
        if (romanGroup != null) StartCoroutine(Anim(romanDur, k => romanGroup.alpha = k));
        int n = charGroups.Count;
        for (int i = 0; i < n; i++) StartCoroutine(PopChar(i, i * charStagger));
        float zhTotal = n > 0 ? (n - 1) * charStagger + charPopDur : 0f;
        float waitB = Mathf.Max(romanDur, zhTotal);
        yield return new WaitForSecondsRealtime(waitB);

        // C) 停留（填满 hold 剩余时间）
        float h = Mathf.Max(0f, hold - barDur - waitB);
        yield return new WaitForSecondsRealtime(h);

        // D) 整体淡出
        yield return Anim(fadeOutDur, k => { if (group != null) group.alpha = 1f - k; });
        if (group != null) group.alpha = 0f;
    }

    void ResetChars()
    {
        for (int i = 0; i < charGroups.Count; i++)
        {
            if (charGroups[i] != null) charGroups[i].alpha = 0f;
            if (charRects[i] != null) charRects[i].localScale = new Vector3(charPopScale, charPopScale, 1f);
        }
    }

    IEnumerator PopChar(int i, float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        AudioManager.Instance?.PlayBossNameStamp();   // 每个字砸出来时响一声
        var cg = i < charGroups.Count ? charGroups[i] : null;
        var rt = i < charRects.Count ? charRects[i] : null;
        float t = 0f;
        while (t < charPopDur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / charPopDur);
            float e = 1f - Mathf.Pow(1f - k, 3f);
            if (cg != null) cg.alpha = k;
            if (rt != null) { float s = Mathf.Lerp(charPopScale, 1f, e); rt.localScale = new Vector3(s, s, 1f); }
            yield return null;
        }
        if (cg != null) cg.alpha = 1f;
        if (rt != null) rt.localScale = Vector3.one;
    }

    IEnumerator Anim(float dur, System.Action<float> step)
    {
        float t = 0f;
        while (t < dur) { t += Time.unscaledDeltaTime; step(Mathf.Clamp01(t / dur)); yield return null; }
        step(1f);
    }
}
