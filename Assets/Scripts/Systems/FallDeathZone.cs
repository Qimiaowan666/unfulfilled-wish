using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class FallDeathZone : MonoBehaviour
{
    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(Tags.Player)) return;

        var rb = other.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        other.GetComponent<PlayerStats>()?.Kill();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.35f);
        var col = GetComponent<Collider2D>();
        if (col != null)
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
    }
}
