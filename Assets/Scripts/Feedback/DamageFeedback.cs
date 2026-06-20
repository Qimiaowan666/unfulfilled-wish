using System.Collections;
using UnityEngine;

public class DamageFeedback : MonoBehaviour
{
    public Color flashColor = Color.white;
    public float flashDuration = 0.1f;
    [Tooltip("命中闪白材质(纯白剪影 Custom/SpriteSolidColor)。空则退化为改 color(对白色无效，看不出闪)")]
    public Material flashMaterial;

    SpriteRenderer[] renderers;
    Color[] originalColors;
    Material[] originalMaterials;
    Rigidbody2D rb;
    Coroutine flashRoutine;
    bool warningHeld;   // Warn/WarnEnd 之间为 true：受击闪白后恢复成红而非原色，红不被打断

    void Awake()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>();
        originalColors = new Color[renderers.Length];
        originalMaterials = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            originalColors[i] = renderers[i].color;
            originalMaterials[i] = renderers[i].sharedMaterial;
        }

        rb = GetComponent<Rigidbody2D>();
    }

    // 还原闪白材质(把被换成 flashMaterial 的渲染器恢复原材质)。任何"打断闪白"的地方都要调,否则卡白剪影。
    void RestoreMaterials()
    {
        if (flashMaterial == null || renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null && i < originalMaterials.Length) renderers[i].sharedMaterial = originalMaterials[i];
    }

    // 失活兜底:闪白途中被禁用 → 协程停在白材质上,这里还原,避免再启用时卡白
    void OnDisable()
    {
        if (renderers == null) return;
        if (flashRoutine != null) { StopCoroutine(flashRoutine); flashRoutine = null; }
        RestoreMaterials();
        warningHeld = false;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null && i < originalColors.Length) renderers[i].color = originalColors[i];
    }

    // 命中闪白：换纯白材质(真正变白)
    public void Flash()
    {
        if (renderers.Length == 0) return;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine(flashColor, flashDuration, true));
    }

    // 预警红染：只改 color(tint 叠色，不换材质)
    public void FlashWarning(float duration = 0.4f)
    {
        if (renderers.Length == 0) return;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine(Color.red, duration, false));
    }

    // 持续红染：染红并保持，直到 ClearWarning()。给成对的 Warn / WarnEnd 动画事件用(时长由两事件位置决定)。
    public void HoldWarning()
    {
        if (renderers.Length == 0) return;
        if (flashRoutine != null) { StopCoroutine(flashRoutine); flashRoutine = null; }
        RestoreMaterials();   // 若正卡在闪白材质先还原,否则红染会变成白/红剪影
        warningHeld = true;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].color = Color.red;
    }

    // 解除红染：恢复原色(Warn 的收尾，也用作招式中断时的兜底)
    public void ClearWarning()
    {
        warningHeld = false;
        if (flashRoutine != null) { StopCoroutine(flashRoutine); flashRoutine = null; }
        RestoreMaterials();   // 中断闪白时也还原材质,避免卡白剪影
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null && i < originalColors.Length) renderers[i].color = originalColors[i];
    }

    // 设置「基色」：受击闪白后恢复到的颜色 + 立即应用。用于持久染色（如 boss 二阶段怒气红）。
    public void SetBaseColor(Color color)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (i < originalColors.Length) originalColors[i] = color;
            if (renderers[i] != null) renderers[i].color = color;
        }
    }

    [Tooltip("击退滑行/最短 flinch 时长(秒)：命中后水平速度在这段时间内线性衰减到 0(摩擦滑停)；普攻没配 hitStun 时也是这段失控时长")]
    public float knockbackDuration = 0.1f;
    Coroutine knockbackRoutine;

    public void ApplyKnockback(Vector3 sourcePosition, float force)
    {
        if (rb == null || force <= 0f) return;
        float direction = Mathf.Sign(transform.position.x - sourcePosition.x);
        if (Mathf.Approximately(direction, 0f)) direction = 1f;
        if (knockbackRoutine != null) StopCoroutine(knockbackRoutine);
        knockbackRoutine = StartCoroutine(KnockbackRoutine(direction * force));
    }

    // 速度 + 衰减(摩擦)：初速度按剩余时间比例线性衰减到 0，读起来是「被推一下然后滑停」。
    // 注意：要看到滑行，被击退者命中时必须进入「不清速度」的状态(玩家=StunnedState/KnockedState)，
    // 否则其 idle/move 状态每帧重设速度会盖掉这里的滑行 → 看不出击退。
    IEnumerator KnockbackRoutine(float horizontalSpeed)
    {
        float t = 0f;
        float dur = Mathf.Max(0.02f, knockbackDuration);
        while (t < dur)
        {
            float k = 1f - t / dur;                                  // 1 → 0 线性衰减
            rb.linearVelocity = new Vector2(horizontalSpeed * k, rb.linearVelocity.y);
            t += Time.deltaTime;
            yield return null;
        }
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        knockbackRoutine = null;
    }

    IEnumerator FlashRoutine(Color c, float d, bool useMaterial)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            if (useMaterial && flashMaterial != null) renderers[i].sharedMaterial = flashMaterial;
            renderers[i].color = c;
        }

        yield return new WaitForSeconds(d);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            if (flashMaterial != null) renderers[i].sharedMaterial = originalMaterials[i];   // 总是还原材质(无害)
            renderers[i].color = warningHeld ? Color.red : originalColors[i];                // 预警期间被打 → 回到红，不被清掉
        }

        flashRoutine = null;
    }
}
