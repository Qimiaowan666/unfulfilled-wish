using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class EnemySaveData
{
    public string id;
    public float currentHP;
    public bool defeated;
}

[System.Serializable]
public class DoorSaveData
{
    public string id;
    public bool opened;
}

[System.Serializable]
public class ShopStockEntrySaveData
{
    public string assetID;
    public int quantity;
}

[System.Serializable]
public class ShopSaveData
{
    public string id;
    public ShopStockEntrySaveData[] itemEntries;
    public ShopStockEntrySaveData[] equipmentEntries;
    public ShopStockEntrySaveData[] skillEntries;
}

[System.Serializable]
public class SaveData
{
    public int saveVersion;
    public string sceneName;
    public string savedAtUtc;

    public float currentHP;
    public float currentGhostHP;
    public int gold;
    public float playerX;
    public float playerY;

    public float baseAttack;
    public float baseDefense;
    public float baseMaxHP;

    public string[] inventoryItemIDs;
    public string[] ownedEquipmentIDs;
    public string equippedWeaponID;
    public string equippedArmorID;
    public string equippedAccessory1ID;
    public string equippedAccessory2ID;
    public string[] learnedSkillIDs;

    public int collectedKeys;
    public string[] collectedKeyIDs;
    public EnemySaveData[] enemyStates;
    public DoorSaveData[] doorStates;
    public ShopSaveData[] shopStates;
    public string[] unlockedCheckpoints;
    public string lastCheckpointID;
}

public class SaveSystem : MonoBehaviour
{
    public static SaveSystem Instance { get; private set; }

    const string SaveFileName = "save.json";
    string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);
    readonly HashSet<string> runtimeDefeatedEnemyIDs = new HashSet<string>();
    readonly HashSet<string> runtimeOpenedDoorIDs = new HashSet<string>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Save(PlayerStats stats, Transform playerTransform, CheckpointManager checkpoints)
    {
        var inventory = InventorySystem.Instance;
        var equipment = EquipmentSystem.Instance;
        var skills = SkillSystem.Instance;
        var keyManager = LevelKeyManager.Instance;

        float baseMaxHP = stats.maxHP - (equipment != null ? equipment.GetEquippedMaxHPBonus() : 0f);

        var data = new SaveData
        {
            saveVersion = 2,
            sceneName = SceneManager.GetActiveScene().name,
            savedAtUtc = System.DateTime.UtcNow.ToString("o"),

            currentHP = stats.CurrentHP,
            currentGhostHP = stats.CurrentGhostHP,
            gold = stats.gold,
            playerX = playerTransform.position.x,
            playerY = playerTransform.position.y,

            baseAttack = stats.baseAttack,
            baseDefense = stats.baseDefense,
            baseMaxHP = Mathf.Max(1f, baseMaxHP),

            inventoryItemIDs = inventory != null ? GetItemIDs(inventory.items) : new string[0],
            ownedEquipmentIDs = equipment != null ? GetEquipmentIDs(equipment.ownedEquipment) : new string[0],
            equippedWeaponID = GetAssetID(equipment != null ? equipment.weapon : null),
            equippedArmorID = GetAssetID(equipment != null ? equipment.armor : null),
            equippedAccessory1ID = GetAssetID(equipment != null ? equipment.accessory1 : null),
            equippedAccessory2ID = GetAssetID(equipment != null ? equipment.accessory2 : null),
            learnedSkillIDs = skills != null ? GetSkillIDs(skills.learnedSkills) : new string[0],

            collectedKeys = keyManager != null ? keyManager.CollectedKeys : 0,
            collectedKeyIDs = GetCollectedKeyIDs(),
            enemyStates = CaptureEnemyStates(),
            doorStates = CaptureDoorStates(),
            shopStates = CaptureShopStates(),
            unlockedCheckpoints = checkpoints != null ? checkpoints.GetUnlockedIDs() : new string[0],
            lastCheckpointID = checkpoints != null ? checkpoints.LastCheckpointID : null
        };

        File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
        Debug.Log($"Saved to {SavePath}");
    }

    public SaveData Load()
    {
        if (!File.Exists(SavePath)) return null;
        return JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
    }

    public bool LoadAndApply()
    {
        return ApplySave(Load());
    }

    public bool ApplySave(SaveData data)
    {
        if (data == null) return false;

        var player = FindAnyObjectByType<PlayerController>();
        var stats = player != null ? player.Stats : FindAnyObjectByType<PlayerStats>();
        var inventory = InventorySystem.Instance;
        var equipment = EquipmentSystem.Instance;
        var skills = SkillSystem.Instance;
        var keyManager = LevelKeyManager.Instance;
        var checkpoints = CheckpointManager.Instance;

        if (stats != null)
        {
            float savedBaseAttack = data.baseAttack > 0f ? data.baseAttack : stats.baseAttack;
            float savedBaseDefense = data.baseDefense > 0f ? data.baseDefense : stats.baseDefense;
            float savedBaseMaxHP = data.baseMaxHP > 0f ? data.baseMaxHP : stats.maxHP;
            stats.LoadBaseStats(savedBaseAttack, savedBaseDefense, savedBaseMaxHP);
        }

        if (inventory != null)
            inventory.LoadItems(ResolveItems(data.inventoryItemIDs));

        if (equipment != null)
        {
            equipment.LoadEquipment(
                ResolveEquipment(data.ownedEquipmentIDs),
                ResolveEquipment(data.equippedWeaponID),
                ResolveEquipment(data.equippedArmorID),
                ResolveEquipment(data.equippedAccessory1ID),
                ResolveEquipment(data.equippedAccessory2ID));
        }

        if (skills != null)
            skills.LoadSkills(ResolveSkills(data.learnedSkillIDs));

        if (keyManager != null)
            keyManager.LoadCollectedKeys(data.collectedKeys);

        if (data.collectedKeyIDs != null)
            ApplyCollectedKeys(data.collectedKeyIDs);

        RefreshRespawnableEnemies();
        ApplyEnemyStates(data.enemyStates);
        ApplyDoorStates(data.doorStates);
        ApplyShopStates(data.shopStates);

        if (checkpoints != null)
            checkpoints.LoadState(data.unlockedCheckpoints, data.lastCheckpointID);

        if (player != null)
        {
            player.transform.position = new Vector3(data.playerX, data.playerY, player.transform.position.z);
            if (player.Rb != null)
                player.Rb.linearVelocity = Vector2.zero;
            player.stateMachine.ChangeState(player.idleState);
        }

        if (stats != null)
            stats.LoadSavedVitals(data.currentHP, data.currentGhostHP, data.gold);

        PlayerInputBuffer.ClearAll();
        Debug.Log($"Loaded save from {SavePath}");
        return true;
    }

    public void RefreshRespawnableEnemies()
    {
        var enemies = FindObjectsByType<EnemyBase>(FindObjectsInactive.Include);
        foreach (var enemy in enemies)
        {
            if (enemy != null && enemy.RespawnsAtCheckpoint)
                enemy.Respawn();
        }
    }

    public bool HasSave() => File.Exists(SavePath);

    public void DeleteSave()
    {
        if (File.Exists(SavePath)) File.Delete(SavePath);
        runtimeDefeatedEnemyIDs.Clear();
        runtimeOpenedDoorIDs.Clear();
    }

    public void MarkEnemyDefeated(string id)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            runtimeDefeatedEnemyIDs.Add(id);
            PersistEnemyDefeat(id);
        }
    }

    public void MarkDoorOpened(string id)
    {
        if (!string.IsNullOrWhiteSpace(id))
            runtimeOpenedDoorIDs.Add(id);
    }

    void PersistEnemyDefeat(string id)
    {
        SaveData data = Load();
        if (data == null) return;

        var states = new List<EnemySaveData>();
        if (data.enemyStates != null)
        {
            foreach (var state in data.enemyStates)
            {
                if (state != null && !string.IsNullOrWhiteSpace(state.id))
                    states.Add(state);
            }
        }

        var existing = states.Find(state => state.id == id);
        if (existing == null)
        {
            existing = new EnemySaveData { id = id };
            states.Add(existing);
        }

        existing.currentHP = 0f;
        existing.defeated = true;
        data.enemyStates = states.ToArray();
        data.savedAtUtc = System.DateTime.UtcNow.ToString("o");
        File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
    }

    static string GetAssetID(ScriptableObject asset)
    {
        return SaveIdUtility.GetAssetID(asset);
    }

    static string[] GetItemIDs(List<ItemData> items)
    {
        var ids = new List<string>();
        foreach (var item in items)
            if (item != null)
                ids.Add(GetAssetID(item));
        return ids.ToArray();
    }

    static string[] GetEquipmentIDs(List<EquipmentData> equipmentList)
    {
        var ids = new List<string>();
        foreach (var equipment in equipmentList)
            if (equipment != null)
                ids.Add(GetAssetID(equipment));
        return ids.ToArray();
    }

    static string[] GetSkillIDs(List<SkillData> skillList)
    {
        var ids = new List<string>();
        foreach (var skill in skillList)
            if (skill != null)
                ids.Add(GetAssetID(skill));
        return ids.ToArray();
    }

    static string[] GetCollectedKeyIDs()
    {
        var ids = new List<string>();
        var keys = FindObjectsByType<KeyPickup>(FindObjectsInactive.Include);
        foreach (var key in keys)
            if (key != null && key.IsCollected)
                ids.Add(key.SaveID);
        return ids.ToArray();
    }

    EnemySaveData[] CaptureEnemyStates()
    {
        var result = new List<EnemySaveData>();
        var seen = new HashSet<string>();
        var enemies = FindObjectsByType<EnemyBase>(FindObjectsInactive.Include);

        foreach (var enemy in enemies)
        {
            if (enemy == null) continue;

            string id = enemy.SaveID;
            if (string.IsNullOrWhiteSpace(id) || seen.Contains(id)) continue;
            if (!enemy.SavesPermanentDeath) continue;

            bool defeated = runtimeDefeatedEnemyIDs.Contains(id) || enemy.IsDefeated || !enemy.gameObject.activeSelf;
            result.Add(new EnemySaveData
            {
                id = id,
                currentHP = defeated ? 0f : enemy.maxHP,
                defeated = defeated
            });
            seen.Add(id);
        }

        foreach (string id in runtimeDefeatedEnemyIDs)
        {
            if (seen.Contains(id)) continue;
            result.Add(new EnemySaveData
            {
                id = id,
                currentHP = 0f,
                defeated = true
            });
        }

        return result.ToArray();
    }

    DoorSaveData[] CaptureDoorStates()
    {
        var result = new List<DoorSaveData>();
        var doors = FindObjectsByType<LockedDoor>(FindObjectsInactive.Include);

        foreach (var door in doors)
        {
            if (door == null) continue;

            string id = door.SaveID;
            if (string.IsNullOrWhiteSpace(id)) continue;

            result.Add(new DoorSaveData
            {
                id = id,
                opened = runtimeOpenedDoorIDs.Contains(id) || door.IsOpen
            });
        }

        foreach (string id in runtimeOpenedDoorIDs)
        {
            if (result.Exists(state => state.id == id)) continue;
            result.Add(new DoorSaveData
            {
                id = id,
                opened = true
            });
        }

        return result.ToArray();
    }

    ShopSaveData[] CaptureShopStates()
    {
        var result = new List<ShopSaveData>();
        var shops = FindObjectsByType<ShopSystem>(FindObjectsInactive.Include);

        foreach (var shop in shops)
        {
            if (shop != null)
                result.Add(shop.CaptureSaveData());
        }

        return result.ToArray();
    }

    static List<ItemData> ResolveItems(string[] ids)
    {
        var result = new List<ItemData>();
        if (ids == null) return result;

        foreach (string id in ids)
        {
            var item = ResolveAsset<ItemData>(id);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    static List<EquipmentData> ResolveEquipment(string[] ids)
    {
        var result = new List<EquipmentData>();
        if (ids == null) return result;

        foreach (string id in ids)
        {
            var equipment = ResolveAsset<EquipmentData>(id);
            if (equipment != null && !result.Contains(equipment))
                result.Add(equipment);
        }

        return result;
    }

    static EquipmentData ResolveEquipment(string id)
    {
        return ResolveAsset<EquipmentData>(id);
    }

    static List<SkillData> ResolveSkills(string[] ids)
    {
        var result = new List<SkillData>();
        if (ids == null) return result;

        foreach (string id in ids)
        {
            var skill = ResolveAsset<SkillData>(id);
            if (skill != null && !result.Contains(skill))
                result.Add(skill);
        }

        return result;
    }

    static T ResolveAsset<T>(string id) where T : ScriptableObject
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        foreach (var asset in Resources.FindObjectsOfTypeAll<T>())
        {
            if (SaveIdUtility.MatchesAssetID(asset, id))
                return asset;
        }

#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (SaveIdUtility.MatchesAssetID(asset, id))
                return asset;
        }
#endif

        Debug.LogWarning($"SaveSystem could not resolve {typeof(T).Name}: {id}");
        return null;
    }

    static void ApplyCollectedKeys(string[] collectedIDs)
    {
        var collected = new HashSet<string>(collectedIDs);
        var keys = FindObjectsByType<KeyPickup>(FindObjectsInactive.Include);
        foreach (var key in keys)
        {
            if (key != null)
                key.LoadCollected(collected.Contains(key.SaveID));
        }
    }

    void ApplyEnemyStates(EnemySaveData[] enemyStates)
    {
        runtimeDefeatedEnemyIDs.Clear();
        if (enemyStates == null) return;

        var states = new Dictionary<string, EnemySaveData>();
        foreach (var state in enemyStates)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.id)) continue;
            states[state.id] = state;
            if (state.defeated)
                runtimeDefeatedEnemyIDs.Add(state.id);
        }

        var enemies = FindObjectsByType<EnemyBase>(FindObjectsInactive.Include);
        foreach (var enemy in enemies)
        {
            if (enemy == null) continue;
            if (states.TryGetValue(enemy.SaveID, out var state))
                enemy.LoadSaveState(state.currentHP, state.defeated);
        }
    }

    void ApplyDoorStates(DoorSaveData[] doorStates)
    {
        runtimeOpenedDoorIDs.Clear();
        if (doorStates == null) return;

        var states = new Dictionary<string, DoorSaveData>();
        foreach (var state in doorStates)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.id)) continue;
            states[state.id] = state;
            if (state.opened)
                runtimeOpenedDoorIDs.Add(state.id);
        }

        var doors = FindObjectsByType<LockedDoor>(FindObjectsInactive.Include);
        foreach (var door in doors)
        {
            if (door == null) continue;
            if (states.TryGetValue(door.SaveID, out var state))
                door.LoadOpened(state.opened);
        }
    }

    void ApplyShopStates(ShopSaveData[] shopStates)
    {
        if (shopStates == null) return;

        var states = new Dictionary<string, ShopSaveData>();
        foreach (var state in shopStates)
        {
            if (state == null || string.IsNullOrWhiteSpace(state.id)) continue;
            states[state.id] = state;
        }

        var shops = FindObjectsByType<ShopSystem>(FindObjectsInactive.Include);
        foreach (var shop in shops)
        {
            if (shop == null) continue;
            if (states.TryGetValue(shop.SaveID, out var state))
                shop.LoadSaveData(state);
        }
    }
}
