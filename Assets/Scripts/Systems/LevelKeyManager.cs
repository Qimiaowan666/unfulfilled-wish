using System;
using UnityEngine;

public class LevelKeyManager : MonoBehaviour
{
    public static LevelKeyManager Instance { get; private set; }

    public int requiredKeys = 2;
    public bool showHud = true;

    public int CollectedKeys { get; private set; }
    public event Action<int, int> OnKeysChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void CollectKey(int amount = 1)
    {
        CollectedKeys = Mathf.Clamp(CollectedKeys + Mathf.Max(1, amount), 0, requiredKeys);
        OnKeysChanged?.Invoke(CollectedKeys, requiredKeys);
    }

    public bool HasRequiredKeys()
    {
        return CollectedKeys >= requiredKeys;
    }

    void OnGUI()
    {
        if (!showHud) return;

        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft
        };
        style.normal.textColor = new Color(1f, 0.86f, 0.18f, 1f);
        GUI.Label(new Rect(24f, 88f, 240f, 40f), $"Keys {CollectedKeys}/{requiredKeys}", style);
    }
}
