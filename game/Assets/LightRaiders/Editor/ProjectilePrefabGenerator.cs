using System;
using FishNet.Component.Transforming;
using FishNet.Editing;
using FishNet.Object;
using UnityEditor;
using UnityEngine;

namespace LightRaiders.Editor
{
    /// <summary>
    /// Builds the Projectile prefab: a server-simulated, server-authoritative
    /// network object with no collider (issue #6 has no hits) and no input.
    /// Idempotent and GUID-stable: SaveAsPrefabAsset overwrites the asset at a
    /// fixed path and never touches the .meta.
    /// </summary>
    public static class ProjectilePrefabGenerator
    {
        public static void Generate()
        {
            SessionAssetPaths.EnsureAllFolders();

            // Bright yellow: distinct from Raider teal, indicator orange, and the two arena grays.
            Material material = RaiderPrefabGenerator.CreateOrUpdateMaterial(
                SessionAssetPaths.ProjectileMaterial, new Color(1f, 0.9f, 0.2f));

            GameObject root = new GameObject("Projectile");
            // NetworkObject FIRST, on the root - never rely on FishNet's add-on-demand fallback.
            root.AddComponent<NetworkObject>();
            root.AddComponent<Projectile>();

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
            /* No collisions in issue #6 scope; primitives ship a collider - destroy
             * it (same rule as the Raider's Visual/AimIndicator children). */
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<SphereCollider>());
            visual.GetComponent<Renderer>().sharedMaterial = material;

            AddNetworkTransform(root);

            PrefabUtility.SaveAsPrefabAsset(root, SessionAssetPaths.ProjectilePrefab);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            /* Same contract as RaiderPrefabGenerator: after Generate() returns,
             * the prefab is registered in DefaultPrefabObjects. Running the
             * rebuild twice per GenerateAll is idempotent and cheap. */
            RefreshDefaultPrefabsMenu.RebuildDefaultPrefabs();
        }

        /* Deliberate duplication of RaiderPrefabGenerator.AddNetworkTransform
         * rather than parameterizing it: the Raider variant also sets the
         * CharacterController component configuration; keep both fail-fast. */
        private static void AddNetworkTransform(GameObject root)
        {
            NetworkTransform networkTransform = root.AddComponent<NetworkTransform>();
            SerializedObject so = new SerializedObject(networkTransform);
            SerializedProperty clientAuthoritative = so.FindProperty("_clientAuthoritative");
            if (clientAuthoritative == null)
                throw new InvalidOperationException(
                    "FishNet renamed NetworkTransform serialized field '_clientAuthoritative' — update ProjectilePrefabGenerator.");
            clientAuthoritative.boolValue = false;   // ADR-0005: server-authoritative
            /* _componentConfiguration deliberately untouched (stays Disabled):
             * the projectile has no CharacterController. */
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
