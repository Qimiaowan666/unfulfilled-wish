using UnityEngine;
using System.Collections.Generic;
using System;

public class CheckpointManager : MonoBehaviour
{
    public static CheckpointManager Instance { get; private set; }

    public bool restorePlayerOnActivate = true;
    public Vector2 respawnOffset = new Vector2(1f, 0f);   // 复活点相对存档点的偏移（右偏，避免跟火堆重叠）

    public string LastCheckpointID { get; private set; }
    public event Action<string> OnCheckpointActivated;

    HashSet<string> unlockedIDs = new HashSet<string>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);   // Bootstrap 常驻，跨场景唯一存活
    }

    public void ActivateCheckpoint(string id, Vector3? respawnAnchor = null)
    {
        if (EnemyBase.AnyBossInCombat()) return;   // boss 战期间禁用火堆（不存档、不回血、不刷新复活点）

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
            player.stateMachine.ChangeState(player.idleState);
        }


        SaveSystem.Instance?.RefreshRespawnableEnemies();

        // 复活点：优先用存档点摆好的锚点（设计师精确摆位，不卡墙）；没有锚点则用玩家站立位置 + 右偏移
        if (stats != null && player != null)
        {
            Vector3 respawnPos = respawnAnchor ?? (player.transform.position + (Vector3)respawnOffset);
            SaveSystem.Instance?.Save(stats, respawnPos, this);
        }

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
