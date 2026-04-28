using UnityEngine;

public enum BufferedPlayerAction
{
    None,
    Attack,
    Dodge,
    Counter,
    Execute,
    Dash
}

public class PlayerInputBuffer : MonoBehaviour
{
    public float defaultBufferTime = 0.22f;

    BufferedPlayerAction bufferedAction = BufferedPlayerAction.None;
    float expiresAt;

    public void Queue(BufferedPlayerAction action, float duration = -1f)
    {
        if (action == BufferedPlayerAction.None) return;

        bufferedAction = action;
        expiresAt = Time.time + (duration > 0f ? duration : defaultBufferTime);
    }

    public bool IsBuffered(BufferedPlayerAction action)
    {
        if (!HasBufferedAction) return false;
        return bufferedAction == action;
    }

    public bool TryConsume(BufferedPlayerAction action)
    {
        if (!IsBuffered(action)) return false;
        Clear();
        return true;
    }

    public void Clear()
    {
        bufferedAction = BufferedPlayerAction.None;
        expiresAt = 0f;
    }

    bool HasBufferedAction
    {
        get
        {
            if (bufferedAction == BufferedPlayerAction.None) return false;
            if (Time.time <= expiresAt) return true;

            Clear();
            return false;
        }
    }
}
