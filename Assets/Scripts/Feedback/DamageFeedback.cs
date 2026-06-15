using System.Collections;
using UnityEngine;

public class DamageFeedback : MonoBehaviour
{
    public Color flashColor = Color.white;
    public float flashDuration = 0.08f;

    SpriteRenderer[] renderers;
    Color[] originalColors;
    Rigidbody2D rb;
    Coroutine flashRoutine;

    void Awake()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>();
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            originalColors[i] = renderers[i].color;

        rb = GetComponent<Rigidbody2D>();
    }

    public void Flash()
    {
        if (renderers.Length == 0) return;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine());
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

    public void FlashWarning(float duration = 0.4f)
    {
        if (renderers.Length == 0) return;
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine(Color.red, duration));
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

    IEnumerator FlashRoutine(Color? color = null, float duration = -1f)
    {
        Color c = color ?? flashColor;
        float d = duration > 0f ? duration : flashDuration;

        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].color = c;

        yield return new WaitForSeconds(d);

        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].color = originalColors[i];

        flashRoutine = null;
    }
}
