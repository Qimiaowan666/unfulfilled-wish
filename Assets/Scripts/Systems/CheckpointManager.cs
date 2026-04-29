using UnityEngine;
using System.Collections.Generic;
using System;

public class CheckpointManager : MonoBehaviour
{
    public static CheckpointManager Instance { get; private set; }

    public bool restorePlayerOnActivate = true;

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
        if (string.IsNullOrWhiteSpace(id))
            id = "Checkpoint";

        unlockedIDs.Add(id);
        LastCheckpointID = id;

        var player = FindAnyObjectByType<PlayerController>();
        var stats = player != null ? player.Stats : FindAnyObjectByType<PlayerStats>();
        if (stats != null && restorePlayerOnActivate)
        {
            stats.RestoreAll();
        }

        if (player != null)
        {
            player.Rb.linearVelocity = Vector2.zero;
            player.SetLocomotionState();
        }

        PlayerInputBuffer.ClearAll();

        SaveSystem.Instance?.RefreshRespawnableEnemies();

        if (stats != null && player != null)
            SaveSystem.Instance?.Save(stats, player.transform, this);

        OnCheckpointActivated?.Invoke(id);
        AudioManager.Instance?.PlayCheckpoint();
    }

    public bool IsTeleportable(string id) => unlockedIDs.Contains(id);

    public string[] GetUnlockedIDs()
    {
        var arr = new string[unlockedIDs.Count];
        unlockedIDs.CopyTo(arr);
        return arr;
    }

    public void LoadState(string[] ids, string lastCheckpointID)
    {
        unlockedIDs.Clear();
        if (ids != null)
        {
            foreach (string id in ids)
            {
                if (!string.IsNullOrWhiteSpace(id))
                    unlockedIDs.Add(id);
            }
        }

        LastCheckpointID = string.IsNullOrWhiteSpace(lastCheckpointID) ? null : lastCheckpointID;
    }

    public void TeleportPlayer(Vector2 destination)
    {
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null) player.transform.position = destination;
    }
}
