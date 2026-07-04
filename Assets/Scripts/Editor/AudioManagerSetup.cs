#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AudioManagerSetup
{
    const string PrefabPath = "Assets/Prefabs/Systems/AudioManager.prefab";

    [MenuItem("Tools/Audio/Setup AudioManager Clips")]
    static void SetupClips()
    {
        var manager = LoadPrefabManager();
        if (manager == null) return;

        var so = new SerializedObject(manager);
        var map = new Dictionary<string, string>
        {
            { "attack1WhooshClip", "Assets/Audio/SFX/Player/attack/sfx_attack1_whoosh.wav" },
            { "attack2WhooshClip", "Assets/Audio/SFX/Player/attack/sfx_attack2_whoosh.wav" },
            { "attack3WhooshClip", "Assets/Audio/SFX/Player/attack/sfx_attack3_whoosh.wav" },

            { "blockClip", "Assets/Audio/SFX/Player/guard/sfx_block.wav" },
            { "perfectBlockClip", "Assets/Audio/SFX/Player/guard/sfx_perfect_block.wav" },
            { "perfectBlockTailClip", "Assets/Audio/SFX/Player/guard/sfx_perfect_block_tail.wav" },
            { "counterClip", "Assets/Audio/SFX/Player/guard/sfx_counter.wav" },

            { "hitLightClip", "Assets/Audio/SFX/Player/hit/sfx_hit_light.wav" },
            { "hitHeavyClip", "Assets/Audio/SFX/Player/hit/sfx_hit_heavy.wav" },

            { "executeDrawClip", "Assets/Audio/SFX/Player/execute/sfx_execute_draw.wav" },
            { "executeStrikeClip", "Assets/Audio/SFX/Player/execute/sfx_execute_strike.wav" },
            { "executeTailClip", "Assets/Audio/SFX/Player/execute/sfx_execute_tail.wav" },

            { "footstepClip", "Assets/Audio/SFX/Player/movement/sfx_footstep.wav" },
            { "jumpClip", "Assets/Audio/SFX/Player/movement/sfx_jump.wav" },
            { "dashClip", "Assets/Audio/SFX/Player/movement/sfx_dash.wav" },
            { "landClip", "Assets/Audio/SFX/Player/movement/sfx_land.wav" },

            { "deathClip", "Assets/Audio/SFX/Player/misc/sfx_death.wav" },

            { "checkpointClip", "Assets/Audio/SFX/kenney_rpg-audio/Audio/creak1.ogg" },
            { "keyPickupClip", "Assets/Audio/SFX/kenney_rpg-audio/Audio/handleCoins.ogg" },
            { "doorOpenClip", "Assets/Audio/SFX/kenney_rpg-audio/Audio/doorOpen_1.ogg" },
            { "shopBuyClip", "Assets/Audio/SFX/kenney_rpg-audio/Audio/handleCoins2.ogg" },
            { "shopFailClip", "Assets/Audio/SFX/kenney_rpg-audio/Audio/metalClick.ogg" },
            { "uiClickClip", "Assets/Audio/SFX/kenney_rpg-audio/Audio/bookFlip1.ogg" },

            { "enemyAttackClip", "Assets/Audio/SFX/kenney_rpg-audio/Audio/knifeSlice.ogg" },
            { "enemyHitClip", "Assets/Audio/SFX/kenney_rpg-audio/Audio/metalPot2.ogg" },
            { "enemyDeathClip", "Assets/Audio/SFX/kenney_rpg-audio/Audio/cloth1.ogg" },
            { "bossAttackClip", "Assets/Audio/SFX/kenney_rpg-audio/Audio/chop.ogg" },
            { "bossRushClip", "Assets/Audio/SFX/Player/movement/sfx_dash.wav" },
            { "bossPhaseChangeClip", "Assets/Audio/SFX/kenney_rpg-audio/Audio/metalLatch.ogg" },

            { "menuBGMClip", "Assets/Audio/BGM/menu.mp3" },
            { "bgmClip", "Assets/Audio/BGM/area1.mp3" },
            { "bossBGMClip", "Assets/Audio/BGM/boss1.mp3" },
            { "futureBossBGMClip", "Assets/Audio/BGM/boss2.mp3" },
        };

        int assigned = 0;
        foreach (var kv in map)
        {
            if (AssignClip(so, kv.Key, kv.Value))
                assigned++;
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(manager);
        AssetDatabase.SaveAssets();
        Debug.Log($"AudioManager clips setup complete. Assigned {assigned} clips.");
    }

    [MenuItem("Tools/Audio/Validate Audio Setup")]
    static void Validate()
    {
        var manager = LoadPrefabManager();
        if (manager == null) return;

        var so = new SerializedObject(manager);
        string[] requiredFields =
        {
            "attack1WhooshClip", "attack2WhooshClip", "attack3WhooshClip",
            "blockClip", "perfectBlockClip", "perfectBlockTailClip", "counterClip",
            "hitLightClip", "hitHeavyClip",
            "executeDrawClip", "executeStrikeClip", "executeTailClip",
            "footstepClip", "jumpClip", "dashClip", "landClip", "deathClip",
            "checkpointClip", "keyPickupClip", "doorOpenClip", "shopBuyClip", "shopFailClip", "uiClickClip",
            "enemyAttackClip", "enemyHitClip", "enemyDeathClip",
            "bossAttackClip", "bossRushClip", "bossPhaseChangeClip",
            "menuBGMClip", "bgmClip", "bossBGMClip", "futureBossBGMClip"
        };

        int missing = 0;
        foreach (string field in requiredFields)
        {
            var property = so.FindProperty(field);
            if (property == null || property.objectReferenceValue == null)
            {
                Debug.LogWarning($"AudioManager missing clip: {field}");
                missing++;
            }
        }

        var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude);
        if (listeners.Length != 1)
            Debug.LogWarning($"Scene '{SceneManager.GetActiveScene().name}' has {listeners.Length} active AudioListener(s). Expected exactly 1.");

        var sceneManagers = Object.FindObjectsByType<AudioManager>(FindObjectsInactive.Include);
        if (sceneManagers.Length < 1)
            Debug.LogWarning($"Scene '{SceneManager.GetActiveScene().name}' has no AudioManager instance.");

        if (missing == 0 && listeners.Length == 1 && sceneManagers.Length >= 1)
            Debug.Log("Audio setup validation passed for prefab and active scene.");
    }

    static AudioManager LoadPrefabManager()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"AudioManager prefab not found: {PrefabPath}");
            return null;
        }

        var manager = prefab.GetComponent<AudioManager>();
        if (manager == null)
        {
            Debug.LogError($"AudioManager component not found on prefab: {PrefabPath}");
            return null;
        }

        return manager;
    }

    static bool AssignClip(SerializedObject so, string fieldName, string assetPath)
    {
        var property = so.FindProperty(fieldName);
        if (property == null)
        {
            Debug.LogWarning($"AudioManager field not found: {fieldName}");
            return false;
        }

        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
        if (clip == null)
        {
            Debug.LogWarning($"Audio clip not found: {assetPath}");
            return false;
        }

        property.objectReferenceValue = clip;
        return true;
    }
}
#endif
