using UnityEngine;
using System.Collections.Generic;
using System;

public class CheckpointManager : MonoBehaviour
{
    public static CheckpointManager Instance { get; private set; }

    public string LastCheckpointID { get; private set; }
    public event Action<string> OnCheckpointActivated;

    HashSet<string> unlockedIDs = new HashSet<string>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void ActivateCheckpoint(string id, Vector2 position)
    {
        if (unlockedIDs.Contains(id)) return;

        unlockedIDs.Add(id);
        LastCheckpointID = id;

        var stats = FindAnyObjectByType<PlayerStats>();
        if (stats != null)
        {
            stats.TakeDamage(-stats.maxHP);
        }

        SaveSystem.Instance?.Save(stats, FindAnyObjectByType<PlayerController>().transform, this);
        OnCheckpointActivated?.Invoke(id);
    }

    public bool IsTeleportable(string id) => unlockedIDs.Contains(id);

    public string[] GetUnlockedIDs()
    {
        var arr = new string[unlockedIDs.Count];
        unlockedIDs.CopyTo(arr);
        return arr;
    }

    public void TeleportPlayer(Vector2 destination)
    {
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null) player.transform.position = destination;
    }
}
