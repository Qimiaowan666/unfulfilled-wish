using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CheckpointPromptUI : MonoBehaviour
{
    public Text messageText;
    public CanvasGroup canvasGroup;
    public float visibleDuration = 1.6f;
    public float fadeDuration = 0.35f;

    Coroutine activeRoutine;
    bool subscribed;

    void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        HideImmediate();
    }

    void OnEnable()
    {
        TrySubscribe();
    }

    void Start()
    {
        TrySubscribe();
    }

    void OnDisable()
    {
        if (CheckpointManager.Instance != null && subscribed)
            CheckpointManager.Instance.OnCheckpointActivated -= Show;
        subscribed = false;
    }

    void TrySubscribe()
    {
        if (subscribed || CheckpointManager.Instance == null) return;
        CheckpointManager.Instance.OnCheckpointActivated += Show;
        subscribed = true;
    }

    public void Show(string checkpointID)
    {
        if (messageText != null)
            messageText.text = $"Checkpoint activated: {checkpointID}";

        if (activeRoutine != null) StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(ShowRoutine());
    }

    IEnumerator ShowRoutine()
    {
        SetAlpha(1f);
        yield return new WaitForSecondsRealtime(visibleDuration);

        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            SetAlpha(1f - timer / fadeDuration);
            yield return null;
        }

        HideImmediate();
        activeRoutine = null;
    }

    void HideImmediate()
    {
        SetAlpha(0f);
    }

    void SetAlpha(float alpha)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = alpha;
            canvasGroup.interactable = alpha > 0f;
            canvasGroup.blocksRaycasts = false;
        }
    }
}
