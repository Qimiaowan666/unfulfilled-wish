using UnityEngine;
using UnityEngine.UI;

public class ExecutePromptUI : MonoBehaviour
{
    public PlayerExecute playerExecute;
    public Text promptText;
    public CanvasGroup canvasGroup;
    public string prompt = "F 处决";
    public Vector3 worldOffset = new Vector3(0f, 1.2f, 0f);
    public float pulseScale = 0.08f;
    public float pulseSpeed = 6f;

    Canvas canvas;
    RectTransform canvasRect;
    RectTransform promptRect;
    EnemyBase currentTarget;

    void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        if (canvas != null) canvasRect = canvas.GetComponent<RectTransform>();

        EnsurePromptObjects();
        Hide();
    }

    void LateUpdate()
    {
        if (playerExecute == null)
            playerExecute = FindAnyObjectByType<PlayerExecute>();

        currentTarget = FindExecutableTarget();
        if (currentTarget == null)
        {
            Hide();
            return;
        }

        ShowAt(currentTarget.transform.position + worldOffset);
    }

    EnemyBase FindExecutableTarget()
    {
        if (playerExecute == null) return null;

        LayerMask layerMask = playerExecute.enemyLayer;
        float range = playerExecute.executeRange;
        Collider2D[] nearby = layerMask.value == 0
            ? Physics2D.OverlapCircleAll(playerExecute.transform.position, range)
            : Physics2D.OverlapCircleAll(playerExecute.transform.position, range, layerMask);

        EnemyBase closest = null;
        float closestSqrDistance = float.MaxValue;

        foreach (var col in nearby)
        {
            var enemy = col.GetComponent<EnemyBase>();
            if (enemy == null || !enemy.IsExecutable) continue;

            float sqrDistance = (enemy.transform.position - playerExecute.transform.position).sqrMagnitude;
            if (sqrDistance >= closestSqrDistance) continue;

            closest = enemy;
            closestSqrDistance = sqrDistance;
        }

        return closest;
    }

    void ShowAt(Vector3 worldPosition)
    {
        if (promptText == null || promptRect == null || canvasRect == null) return;

        promptText.text = prompt;
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        else promptText.gameObject.SetActive(true);

        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector3 screenPosition = Camera.main != null
            ? Camera.main.WorldToScreenPoint(worldPosition)
            : worldPosition;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, uiCamera, out Vector2 localPoint))
            promptRect.anchoredPosition = localPoint;

        float scale = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseScale;
        promptRect.localScale = new Vector3(scale, scale, 1f);
    }

    void Hide()
    {
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        else if (promptText != null) promptText.gameObject.SetActive(false);
    }

    void EnsurePromptObjects()
    {
        if (promptText == null)
        {
            GameObject textObject = new GameObject("ExecutePromptText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(transform, false);

            promptText = textObject.GetComponent<Text>();
            promptText.alignment = TextAnchor.MiddleCenter;
            promptText.fontSize = 22;
            promptText.fontStyle = FontStyle.Bold;
            promptText.color = new Color(1f, 0.92f, 0.18f);
            promptText.raycastTarget = false;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            promptText.font = font;
        }

        promptRect = promptText.GetComponent<RectTransform>();
        promptRect.sizeDelta = new Vector2(140f, 36f);

        if (canvasGroup == null)
        {
            canvasGroup = promptText.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = promptText.gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }
}
