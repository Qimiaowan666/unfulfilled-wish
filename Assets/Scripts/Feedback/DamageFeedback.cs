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

    // 设置「基色」：受击闪白后恢复到的颜色 + 立即应用。用于持久染色（如 boss 二阶段怒气红）。
    public void SetBaseColor(Color color)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (i < originalColors.Length) originalColors[i] = color;
            if (renderers[i] != null) renderers[i].color = color;
        }
    }

    Coroutine knockbackRoutine;

    public void ApplyKnockback(Vector3 sourcePosition, float force)
    {
        if (rb == null || force <= 0f) return;
        float direction = Mathf.Sign(transform.position.x - sourcePosition.x);
        if (Mathf.Approximately(direction, 0f)) direction = 1f;
        if (knockbackRoutine != null) StopCoroutine(knockbackRoutine);
        knockbackRoutine = StartCoroutine(KnockbackRoutine(direction * force));
    }

    IEnumerator KnockbackRoutine(float horizontalSpeed)
    {
        rb.linearVelocity = new Vector2(horizontalSpeed, rb.linearVelocity.y);
        yield return new WaitForSeconds(0.12f);
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
            renderers[i].color = originalColors[i];
        }

        flashRoutine = null;
    }
}
