using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(SpriteRenderer))]
public class WuxiaSpritePreview : MonoBehaviour
{
    [System.Serializable]
    public class PreviewAction
    {
        public string actionName;
        public Sprite[] frames;
        public float framesPerSecond = 8f;
        public float previewDuration = 2f;
    }

    public PreviewAction[] actions;
    public bool autoSwitchAction = true;
    public bool showDebugLabel = true;

    SpriteRenderer spriteRenderer;
    int actionIndex;
    int frameIndex;
    float frameTimer;
    float actionTimer;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        SelectAction(0);
    }

    void Update()
    {
        if (actions == null || actions.Length == 0) return;

        for (int i = 0; i < actions.Length && i < 9; i++)
        {
            if (WasNumberPressed(i))
            {
                SelectAction(i);
                return;
            }
        }

        float deltaTime = Time.unscaledDeltaTime;
        UpdateFrame(deltaTime);
        UpdateAction(deltaTime);
    }

    void UpdateFrame(float deltaTime)
    {
        PreviewAction action = actions[actionIndex];
        if (action.frames == null || action.frames.Length == 0) return;

        float fps = Mathf.Max(1f, action.framesPerSecond);
        frameTimer += deltaTime;
        if (frameTimer < 1f / fps) return;

        frameTimer = 0f;
        frameIndex = (frameIndex + 1) % action.frames.Length;
        spriteRenderer.sprite = action.frames[frameIndex];
    }

    void UpdateAction(float deltaTime)
    {
        if (!autoSwitchAction || actions.Length <= 1) return;

        PreviewAction action = actions[actionIndex];
        actionTimer += deltaTime;
        if (actionTimer < Mathf.Max(0.5f, action.previewDuration)) return;

        SelectAction((actionIndex + 1) % actions.Length);
    }

    void SelectAction(int index)
    {
        if (actions == null || actions.Length == 0) return;

        actionIndex = Mathf.Clamp(index, 0, actions.Length - 1);
        frameIndex = 0;
        frameTimer = 0f;
        actionTimer = 0f;

        PreviewAction action = actions[actionIndex];
        if (action.frames != null && action.frames.Length > 0)
        {
            spriteRenderer.sprite = action.frames[0];
        }
    }

    bool WasNumberPressed(int zeroBasedIndex)
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return false;

        switch (zeroBasedIndex)
        {
            case 0: return keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame;
            case 1: return keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame;
            case 2: return keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame;
            case 3: return keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame;
            case 4: return keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame;
            case 5: return keyboard.digit6Key.wasPressedThisFrame || keyboard.numpad6Key.wasPressedThisFrame;
            case 6: return keyboard.digit7Key.wasPressedThisFrame || keyboard.numpad7Key.wasPressedThisFrame;
            case 7: return keyboard.digit8Key.wasPressedThisFrame || keyboard.numpad8Key.wasPressedThisFrame;
            case 8: return keyboard.digit9Key.wasPressedThisFrame || keyboard.numpad9Key.wasPressedThisFrame;
            default: return false;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + zeroBasedIndex));
#else
        return false;
#endif
    }

    void OnGUI()
    {
        if (!showDebugLabel || actions == null || actions.Length == 0) return;

        string actionName = actions[actionIndex] != null ? actions[actionIndex].actionName : "None";
        GUI.Label(new Rect(16f, 16f, 520f, 24f), $"Wuxia Sprite Preview: {actionName} | 1-{Mathf.Min(actions.Length, 9)} switch action");
    }
}
