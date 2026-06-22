using System;
using UnityEngine;

public class LevelKeyManager : MonoBehaviour
{
    public static LevelKeyManager Instance { get; private set; }

    public int requiredKeys = 2;

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

    public void LoadCollectedKeys(int count)
    {
        CollectedKeys = Mathf.Clamp(count, 0, requiredKeys);
        OnKeysChanged?.Invoke(CollectedKeys, requiredKeys);
    }

}
