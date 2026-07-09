using System.Collections;
using UnityEngine;

// 受击反馈:闪白 + 红色预警染色 + 击退。
// 渲染走 HitFlash shader(Unlit):常态照常显示精灵(_FlashAmount=_WarnAmount=0),受击时用 MaterialPropertyBlock
// 拉 _FlashAmount(闪白)、持续预警时拉 _WarnAmount(染红)。两者在 shader 里各走一条 Lerp,互不干扰、可同时叠加
// (闪白盖在红上,闪完仍是红)。底色仍走 SpriteRenderer.color(顶点色),SetBaseColor 改它(如 boss 二阶段怒气红)。
// MPB 逐渲染器隔离 → 多个敌人共用同一份 HitFlash 材质、各闪各的,不产生材质实例。
// 不再换材质 → 没有旧版"卡白剪影"那套还原逻辑。
public class DamageFeedback : MonoBehaviour
{
    [Tooltip("闪白颜色(驱动 shader _FlashColor)")]
    public Color flashColor = Color.white;
    [Tooltip("闪白时长(秒)")]
    public float flashDuration = 0.1f;
    [Tooltip("预警染色(驱动 shader _WarnColor)")]
    public Color warnColor = Color.red;
    [Tooltip("HitFlash 材质(Shader Graphs/HitFlash)。Awake 时套到所有子 SpriteRenderer;空则不闪(退化为普通渲染)")]
    public Material hitFlashMaterial;

    SpriteRenderer[] renderers;
    MaterialPropertyBlock mpb;
    Rigidbody2D rb;
    Coroutine flashRoutine;

    static readonly int FlashColorId  = Shader.PropertyToID("_FlashColor");
    static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
    static readonly int WarnColorId   = Shader.PropertyToID("_WarnColor");
    static readonly int WarnAmountId  = Shader.PropertyToID("_WarnAmount");

    void Awake()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>();
        mpb = new MaterialPropertyBlock();
        rb  = GetComponent<Rigidbody2D>();

        // 永久套上 HitFlash 材质(共享资产,不产生实例;各自的闪白/染红状态走 MPB 逐渲染器隔离)。
        if (hitFlashMaterial != null)
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].sharedMaterial = hitFlashMaterial;
    }

    // 失活兜底:清掉闪白/染红,避免(池化)再启用时卡在某个状态
    void OnDisable()
    {
        if (renderers == null || mpb == null) return;
        if (flashRoutine != null) { StopCoroutine(flashRoutine); flashRoutine = null; }
        SetFloatAll(FlashAmountId, 0f);
        SetFloatAll(WarnAmountId, 0f);
    }

    // 命中闪白:_FlashAmount 拉满一小会儿再归零。用缩放时间 → 顿帧期间白闪照样保持,和顿帧叠一起。
    public void Flash()
    {
        if (renderers == null || renderers.Length == 0) return;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        SetFlash(1f);
        yield return new WaitForSeconds(flashDuration);
        SetFlash(0f);
        flashRoutine = null;
    }

    // 持续红染:拉起 _WarnAmount 并保持,直到 ClearWarning()。给成对的 Warn / WarnEnd 动画事件用(时长由两事件位置决定)。
    // 和闪白独立:预警期间挨打 → 闪白叠在红上,闪完仍是红(shader 两条 Lerp 各管各的),不再需要旧版 warningHeld 协调。
    public void HoldWarning()
    {
        if (renderers == null || renderers.Length == 0) return;
        SetWarn(1f);
    }

    // 解除红染(Warn 的收尾,也用作招式中断时的兜底)
    public void ClearWarning()
    {
        if (renderers == null) return;
        SetWarn(0f);
    }

    // 设置底色(顶点色):受击闪白 / 预警红染都叠在它之上。用于持久染色(如 boss 二阶段怒气红)。
    public void SetBaseColor(Color color)
    {
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].color = color;
    }

    // ── MPB 写入(逐渲染器 Get→改→Set:保留另一条通道已有的值,使闪白/染红可独立叠加)──
    void SetFlash(float amount)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            r.GetPropertyBlock(mpb);
            mpb.SetColor(FlashColorId, flashColor);
            mpb.SetFloat(FlashAmountId, amount);
            r.SetPropertyBlock(mpb);
        }
    }

    void SetWarn(float amount)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            r.GetPropertyBlock(mpb);
            mpb.SetColor(WarnColorId, warnColor);
            mpb.SetFloat(WarnAmountId, amount);
            r.SetPropertyBlock(mpb);
        }
    }

    void SetFloatAll(int id, float v)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            r.GetPropertyBlock(mpb);
            mpb.SetFloat(id, v);
            r.SetPropertyBlock(mpb);
        }
    }

    // ── 击退(与渲染无关,原样保留)──
    [Tooltip("击退滑行/最短 flinch 时长(秒)：命中后水平速度在这段时间内线性衰减到 0(摩擦滑停)；普攻没配 hitStun 时也是这段失控时长")]
    public float knockbackDuration = 0.1f;
    [Tooltip("霸体:免疫击退位移(boss / 重型怪打不动)。只挡位移,受击闪白 / 扣血 / 削韧 / 破韧处决都照常")]
    public bool immuneToKnockback;
    Coroutine knockbackRoutine;

    public void ApplyKnockback(Vector3 sourcePosition, float force)
    {
        if (immuneToKnockback || rb == null || force <= 0f) return;
        float direction = Mathf.Sign(transform.position.x - sourcePosition.x);
        if (Mathf.Approximately(direction, 0f)) direction = 1f;
        if (knockbackRoutine != null) StopCoroutine(knockbackRoutine);
        knockbackRoutine = StartCoroutine(KnockbackRoutine(direction * force));
    }

    // 速度 + 衰减(摩擦)：初速度按剩余时间比例线性衰减到 0，读起来是「被推一下然后滑停」。
    // 注意：要看到滑行，被击退者命中时必须进入「不清速度」的状态(玩家=StunnedState)，
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
}
