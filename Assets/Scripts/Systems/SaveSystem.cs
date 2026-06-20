using System.Collections;
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
    public float currentStamina;
    public int gold;
    public float playerX;
    public float playerY;
    public float respawnX;   // 火堆复活点（死亡重生落这里）；playerX/Y 是读档落点
    public float respawnY;

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

    public const int AutoSlot = -1;        // 检查点重生用的内部自动档
    public const int ManualSlotCount = 3;  // 玩家手动存档槽：0 / 1 / 2

    static string SlotPath(int slot) => Path.Combine(Application.persistentDataPath,
        slot == AutoSlot ? "save_auto.json" : $"save_{slot}.json");
    static string ThumbPath(int slot) => Path.Combine(Application.persistentDataPath, $"save_{slot}.png");
    string SavePath => SlotPath(AutoSlot);   // 原有“自动档”读写沿用此路径（检查点存 / 重生读 / 场景加载读）
    readonly HashSet<string> runtimeDefeatedEnemyIDs = new HashSet<string>();
    readonly HashSet<string> runtimeOpenedDoorIDs = new HashSet<string>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    // 读档的统一触发点：每个游戏场景加载后等一帧自动 apply（不再依赖场景里是否挂了 LevelManager）
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (SceneNames.IsNonGameplay(scene.name)) return;
        StartCoroutine(ApplyAfterFrame());
    }

    IEnumerator ApplyAfterFrame()
    {
        yield return null;   // 等玩家/敌人 Awake 完再 apply，确保 FindAnyObjectByType 能拿到玩家
        if (HasSave())
            ApplyOnSceneLoaded();
        nextLoadIsRespawn = false;   // 兜底清标志，避免“没档可读”时残留导致下次读档落点出错
        globalStateLoaded = true;    // 本场景玩家状态已确立(全新/读档)；之后过门不再用存档覆盖常驻玩家，只有继续/重生(RequestFullReload/PrepareRespawn)才会
    }

    public void Save(PlayerStats stats, Vector3 respawnPosition, CheckpointManager checkpoints)
    {
        WriteSlot(AutoSlot, BuildSaveData(stats, respawnPosition, respawnPosition, checkpoints));   // 检查点：落点=复活点=火堆
    }

    void WriteSlot(int slot, SaveData data)
    {
        File.WriteAllText(SlotPath(slot), JsonUtility.ToJson(data, true));
        Debug.Log($"Saved slot {slot}: {SlotPath(slot)}");
    }

    SaveData BuildSaveData(PlayerStats stats, Vector3 landPosition, Vector3 respawnPosition, CheckpointManager checkpoints)
    {
        var inventory = InventorySystem.Instance;
        var equipment = EquipmentSystem.Instance;
        var skills = SkillSystem.Instance;
        var keyManager = LevelKeyManager.Instance;

        float baseMaxHP = stats.maxHP - (equipment != null ? equipment.GetEquippedMaxHPBonus() : 0f);

        return new SaveData
        {
            saveVersion = 2,
            sceneName = SceneManager.GetActiveScene().name,
            savedAtUtc = System.DateTime.UtcNow.ToString("o"),

            currentHP = stats.CurrentHP,
            currentGhostHP = stats.CurrentGhostHP,
            currentStamina = stats.CurrentStamina,
            gold = stats.gold,
            playerX = landPosition.x,
            playerY = landPosition.y,
            respawnX = respawnPosition.x,
            respawnY = respawnPosition.y,

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
    }

    public SaveData Load()
    {
        if (!File.Exists(SavePath)) return null;
        return JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
    }

    // 全局持久态（背包/装备/技能/玩家属性）是否已在本会话载入过。
    // 常驻系统下，全局态只在「首次进入 / 显式读档 / 重生」时 apply，切场景不再 apply（内存连续）。
    bool globalStateLoaded;

    // 每次「场景态 apply 完成」后触发（读档 / 重生 / 进场景都会走到）。
    // 例如 BossIntroTrigger 订阅它，在读档后把存活的 boss 重新置为沉睡，让登场剧情可再次触发。
    public static event System.Action AfterApply;

    // 显式读档 / 重生：apply 全套（全局 + 场景），并标记全局已载入
    public bool LoadAndApply()
    {
        return ApplySave(Load());
    }

    public bool ApplySave(SaveData data)
    {
        if (data == null) return false;
        ApplyGlobalState(data);
        ApplySceneState(data);
        globalStateLoaded = true;
        Debug.Log($"Loaded save from {SavePath}");
        return true;
    }

    // 每个场景加载时由 LevelManager 调：
    //   全局态只首次 apply（之后内存连续，不被存档覆盖）；场景态每次都 apply（恢复本场景敌人/门/钥匙）
    public bool ApplyOnSceneLoaded()
    {
        var data = Load();
        if (data == null) return false;

        if (!globalStateLoaded)
        {
            ApplyGlobalState(data);
            globalStateLoaded = true;
        }
        ApplySceneState(data);
        return true;
    }

    // 重生 / 显式读档前调用，强制下次重新 apply 全局态（让数据回到存档状态）
    public void RequestFullReload() => globalStateLoaded = false;

    bool nextLoadIsRespawn;
    // 死亡重生：下次 apply 落“火堆复活点”而非读档落点
    public void PrepareRespawn() { nextLoadIsRespawn = true; globalStateLoaded = false; }

    // 全局持久态：玩家属性、背包、装备、技能、玩家位置/血量
    void ApplyGlobalState(SaveData data)
    {
        if (data == null) return;

        var player = FindAnyObjectByType<PlayerController>();
        var stats = player != null ? player.Stats : FindAnyObjectByType<PlayerStats>();
        var inventory = InventorySystem.Instance;
        var equipment = EquipmentSystem.Instance;
        var skills = SkillSystem.Instance;

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

        if (player != null)
        {
            // 死亡重生 → 火堆复活点(respawnX/Y)；普通读档/进入 → 读档落点(playerX/Y)。旧档无 respawn 坐标时回退 playerX/Y
            bool useRespawn = nextLoadIsRespawn && (data.respawnX != 0f || data.respawnY != 0f);
            float px = useRespawn ? data.respawnX : data.playerX;
            float py = useRespawn ? data.respawnY : data.playerY;
            player.transform.position = new Vector3(px, py, player.transform.position.z);
            if (player.Rb != null)
                player.Rb.linearVelocity = Vector2.zero;
            player.stateMachine.ChangeState(player.idleState);
        }
        nextLoadIsRespawn = false;

        if (stats != null)
            stats.LoadSavedVitals(data.currentHP, data.currentGhostHP, data.currentStamina, data.gold);

        PlayerInputBuffer.ClearAll();
    }

    // 场景态：钥匙、敌人、门、商店、检查点（每个场景的对象，每次进场景都恢复）
    void ApplySceneState(SaveData data)
    {
        if (data == null) return;

        var keyManager = LevelKeyManager.Instance;
        var checkpoints = CheckpointManager.Instance;

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

        AfterApply?.Invoke();
    }

    public void RefreshRespawnableEnemies()
    {
        var enemies = FindObjectsByType<EnemyBase>(FindObjectsInactive.Include);
        foreach (var enemy in enemies)
        {
            // 跳过从未初始化的预留 inactive 敌人（它们的 initialPosition 还是 0,0,0，Respawn 会把它们扔到原点飞出去）
            if (enemy != null && enemy.Initialized && enemy.RespawnsAtCheckpoint)
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

    // 新游戏：清自动档 + 清空常驻单例（装备/背包/技能）。
    // 这些是 DontDestroyOnLoad 单例，光删档不会清它们；不清的话上一局的装备/物品/技能会残留到新档。
    public void ResetForNewGame()
    {
        DeleteSave();
        globalStateLoaded = false;
        nextLoadIsRespawn = false;
        EquipmentSystem.Instance?.LoadEquipment(null, null, null, null, null);
        InventorySystem.Instance?.LoadItems(null);
        SkillSystem.Instance?.LoadSkills(null);
    }

    // ===== 手动多槽存读（ESC 菜单：随时存 / 读，带截图缩略图）=====

    public SaveData Load(int slot)
    {
        string p = SlotPath(slot);
        if (!File.Exists(p)) return null;
        return JsonUtility.FromJson<SaveData>(File.ReadAllText(p));
    }

    public bool HasSlot(int slot) => File.Exists(SlotPath(slot));
    public SaveData GetSlotMeta(int slot) => Load(slot);   // 档不大，直接读整档取 sceneName/savedAtUtc/gold 预览

    public void DeleteSlot(int slot)
    {
        if (File.Exists(SlotPath(slot))) File.Delete(SlotPath(slot));
        if (File.Exists(ThumbPath(slot))) File.Delete(ThumbPath(slot));
    }

    // 随时手动存档：抓玩家“当前位置”+ 全套状态，写入手动槽，并存缩略图
    public bool SaveToSlot(int slot)
    {
        var player = FindAnyObjectByType<PlayerController>();
        var stats = player != null ? player.Stats : FindAnyObjectByType<PlayerStats>();
        if (stats == null) { Debug.LogWarning("SaveToSlot: 找不到 PlayerStats"); return false; }
        Vector3 landPos = player != null ? player.transform.position : stats.transform.position;

        // 复活点 = 当前自动档记录的火堆点（最后坐的火）；没坐过火则落当前位置
        Vector3 respawnPos = landPos;
        var auto = Load(AutoSlot);
        if (auto != null)
            respawnPos = (auto.respawnX != 0f || auto.respawnY != 0f)
                ? new Vector3(auto.respawnX, auto.respawnY, 0f)
                : new Vector3(auto.playerX, auto.playerY, 0f);

        WriteSlot(slot, BuildSaveData(stats, landPos, respawnPos, CheckpointManager.Instance));
        WriteThumbnail(slot);
        return true;
    }

    // 读取手动槽（同步到 auto 重生点；跨场景则切场景后由 ApplyOnSceneLoaded 恢复）
    public bool LoadSlot(int slot)
    {
        var data = Load(slot);
        if (data == null) return false;
        RequestFullReload();
        WriteSlot(AutoSlot, data);   // 让重生点 = 这次读的档
        string active = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrEmpty(data.sceneName) && data.sceneName != active)
            GameManager.Instance?.LoadScene(data.sceneName);   // 切场景，加载后自动 apply auto 档（含 BGM）
        else
        {
            AudioManager.Instance?.RefreshSceneBGM();   // 先回到场景默认曲(区域曲)——同场景读档没有 sceneLoaded
            ApplySave(data);                            // 再 apply → AfterApply → LevelManager 视战斗状态切 boss 曲（最终定）
        }
        return true;
    }

    // ---- 缩略图（存档时现截一次；存档前由 UI 临时隐藏菜单 overlay，保证画面干净）----
    void WriteThumbnail(int slot)
    {
        var thumb = MakeThumbnail();
        if (thumb == null) return;
        try { File.WriteAllBytes(ThumbPath(slot), thumb.EncodeToPNG()); } catch { }
        Destroy(thumb);
    }

    Texture2D MakeThumbnail()
    {
        var full = ScreenCapture.CaptureScreenshotAsTexture();
        if (full == null) return null;
        int tw = 320, th = Mathf.Max(1, Mathf.RoundToInt(320f * full.height / Mathf.Max(1, full.width)));
        var rt = RenderTexture.GetTemporary(tw, th);
        Graphics.Blit(full, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var thumb = new Texture2D(tw, th, TextureFormat.RGB24, false);
        thumb.ReadPixels(new Rect(0, 0, tw, th), 0, 0);
        thumb.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        Destroy(full);
        return thumb;
    }

    public Texture2D LoadThumbnail(int slot)
    {
        string p = ThumbPath(slot);
        if (!File.Exists(p)) return null;
        var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
        tex.LoadImage(File.ReadAllBytes(p));
        return tex;
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

        // 宝箱复用同一套"已开"持久化(开过的 id 在 runtimeOpenedDoorIDs 里)
        var chests = FindObjectsByType<ChestInteract>(FindObjectsInactive.Include);
        foreach (var chest in chests)
            if (chest != null) chest.LoadOpened(runtimeOpenedDoorIDs.Contains(chest.SaveID));
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
