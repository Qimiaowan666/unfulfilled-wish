using UnityEngine;
using System.IO;

[System.Serializable]
public class SaveData
{
    public float currentHP;
    public float currentGhostHP;
    public int gold;
    public float playerX;
    public float playerY;
    public string[] unlockedCheckpoints;
    public string lastCheckpointID;
}

public class SaveSystem : MonoBehaviour
{
    public static SaveSystem Instance { get; private set; }

    const string SaveFileName = "save.json";
    string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Save(PlayerStats stats, Transform playerTransform, CheckpointManager checkpoints)
    {
        var data = new SaveData
        {
            currentHP      = stats.CurrentHP,
            currentGhostHP = stats.CurrentGhostHP,
            gold           = stats.gold,
            playerX        = playerTransform.position.x,
            playerY        = playerTransform.position.y,
            unlockedCheckpoints = checkpoints.GetUnlockedIDs(),
            lastCheckpointID    = checkpoints.LastCheckpointID
        };

        File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
        Debug.Log($"Saved to {SavePath}");
    }

    public SaveData Load()
    {
        if (!File.Exists(SavePath)) return null;
        return JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
    }

    public bool HasSave() => File.Exists(SavePath);

    public void DeleteSave()
    {
        if (File.Exists(SavePath)) File.Delete(SavePath);
    }
}
