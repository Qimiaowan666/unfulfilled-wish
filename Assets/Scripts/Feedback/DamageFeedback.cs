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

    public void ApplyKnockback(Vector3 sourcePosition, float force)
    {
        if (rb == null || force <= 0f) return;
        float direction = Mathf.Sign(transform.position.x - sourcePosition.x);
        if (Mathf.Approximately(direction, 0f)) direction = 1f;
        rb.AddForce(new Vector2(direction * force, force * 0.25f), ForceMode2D.Impulse);
    }

    IEnumerator FlashRoutine()
    {
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].color = flashColor;

        yield return new WaitForSeconds(flashDuration);

        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].color = originalColors[i];

        flashRoutine = null;
    }
}
