using UnityEngine;

// 教学触发区：玩家走进区域 → 显示教学提示；走出区域 → 隐藏。可反复进出反复显示。
[RequireComponent(typeof(Collider2D))]
public class TutorialTrigger : MonoBehaviour
{
    [Tooltip("标题(金色)，可留空")]
    public string title;
    [TextArea(2, 4)]
    [Tooltip("正文，支持富文本")]
    public string body;

    void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(Tags.Player)) return;
        TutorialPromptUI.Show(title, body);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(Tags.Player)) return;
        TutorialPromptUI.Hide();
    }

    void OnDrawGizmos()
    {
        var col = GetComponent<Collider2D>();
        if (col == null) return;
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.25f);
        Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
}
