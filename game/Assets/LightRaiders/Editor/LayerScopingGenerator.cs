using System;
using FishNet.Object;
using FishNet.Observing;
using UnityEditor;
using UnityEngine;

namespace LightRaiders.Editor
{
    /// <summary>
    /// Shared generator step for Layer-bound prefabs (ADR-0008): adds a
    /// <see cref="LayerMember"/> and a NetworkObserver carrying the project's
    /// <see cref="LayerCondition"/> asset, so the object is observed only by
    /// connections in its Layer. Creates the condition asset on first use.
    /// </summary>
    public static class LayerScopingGenerator
    {
        public static LayerCondition EnsureConditionAsset()
        {
            SessionAssetPaths.EnsureAllFolders();
            LayerCondition condition = AssetDatabase.LoadAssetAtPath<LayerCondition>(SessionAssetPaths.LayerConditionAsset);
            if (condition != null)
                return condition;

            condition = ScriptableObject.CreateInstance<LayerCondition>();
            AssetDatabase.CreateAsset(condition, SessionAssetPaths.LayerConditionAsset);
            AssetDatabase.SaveAssets();
            return condition;
        }

        /// <summary>Call on the prefab root (which already carries its NetworkObject) before saving it.</summary>
        public static void AddLayerScoping(GameObject root)
        {
            if (root.GetComponent<NetworkObject>() == null)
                throw new InvalidOperationException("AddLayerScoping needs the NetworkObject on '" + root.name + "' first.");

            root.AddComponent<LayerMember>();

            NetworkObserver observer = root.AddComponent<NetworkObserver>();
            SerializedObject so = new SerializedObject(observer);
            SerializedProperty conditions = so.FindProperty("_observerConditions");
            if (conditions == null)
                throw new InvalidOperationException("FishNet renamed NetworkObserver field '_observerConditions' — update LayerScopingGenerator.");
            /* IgnoreManager (the component default): this object's visibility is
             * exactly the Layer rule, independent of any ObserverManager defaults. */
            SerializedProperty overrideType = so.FindProperty("_overrideType");
            if (overrideType == null)
                throw new InvalidOperationException("FishNet renamed NetworkObserver field '_overrideType' — update LayerScopingGenerator.");
            overrideType.enumValueIndex = (int)NetworkObserver.ConditionOverrideType.IgnoreManager;

            conditions.arraySize = 1;
            conditions.GetArrayElementAtIndex(0).objectReferenceValue = EnsureConditionAsset();
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
