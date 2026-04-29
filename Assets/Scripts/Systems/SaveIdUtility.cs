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

    public static string GetSceneObjectID(Component component, string explicitID)
    {
        if (!string.IsNullOrWhiteSpace(explicitID))
            return explicitID;

        return component != null ? GetHierarchyPath(component.transform) : string.Empty;
    }

    public static string GetSceneObjectID(GameObject gameObject, string explicitID)
    {
        if (!string.IsNullOrWhiteSpace(explicitID))
            return explicitID;

        return gameObject != null ? GetHierarchyPath(gameObject.transform) : string.Empty;
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
