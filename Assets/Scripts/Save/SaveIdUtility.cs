using System.Collections.Generic;
using UnityEngine;

public static class SaveIdUtility
{
    public static string GetAssetID(ScriptableObject asset)
    {
        if (asset == null) return string.Empty;

        if (asset is ItemData item && !string.IsNullOrWhiteSpace(item.saveID)) return item.saveID;
        if (asset is EquipmentData equipment && !string.IsNullOrWhiteSpace(equipment.saveID)) return equipment.saveID;
        if (asset is SkillData skill && !string.IsNullOrWhiteSpace(skill.saveID)) return skill.saveID;

        return asset.name;
    }

    public static bool MatchesAssetID(ScriptableObject asset, string id)
    {
        if (asset == null || string.IsNullOrWhiteSpace(id)) return false;
        return GetAssetID(asset) == id || asset.name == id;
    }

    // 给场景物体 ID 加「场景名:」前缀,避免不同场景里同名/同路径物体撞 ID(跨场景污染:
    // A 场景开过的门/杀过的怪,进 B 场景时同 ID 物体被误判恢复)。资产 ID(GetAssetID)是全局的,不加前缀。
    public static string WithScene(GameObject go, string baseId)
    {
        string scene = go != null ? go.scene.name : null;
        return string.IsNullOrEmpty(scene) ? baseId : scene + ":" + baseId;
    }

    public static string WithScene(Component component, string baseId)
        => WithScene(component != null ? component.gameObject : null, baseId);

    public static string GetSceneObjectID(Component component, string explicitID)
    {
        string baseId = !string.IsNullOrWhiteSpace(explicitID)
            ? explicitID
            : (component != null ? GetHierarchyPath(component.transform) : string.Empty);
        return WithScene(component, baseId);
    }

    public static string GetSceneObjectID(GameObject gameObject, string explicitID)
    {
        string baseId = !string.IsNullOrWhiteSpace(explicitID)
            ? explicitID
            : (gameObject != null ? GetHierarchyPath(gameObject.transform) : string.Empty);
        return WithScene(gameObject, baseId);
    }

    static string GetHierarchyPath(Transform transform)
    {
        var names = new List<string>();
        while (transform != null)
        {
            names.Add(transform.name);
            transform = transform.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }
}
