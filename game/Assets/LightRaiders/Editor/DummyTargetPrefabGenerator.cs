using System;
using FishNet.Editing;
using FishNet.Object;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LightRaiders.Editor
{
    /// <summary>
    /// Builds the graybox DummyTarget prefab: a networked, server-authoritative
    /// damageable carrying Health (Side.Hostile) with a capsule visual. Unlike the
    /// Raider and Projectile visuals, the capsule KEEPS its collider - the
    /// projectile sweep raycasts colliders, so the target needs one to be hit.
    /// No NetworkTransform: a target never moves (respawn is a fresh spawn at the
    /// post), so its pose replicates once in the spawn message. Idempotent and
    /// GUID-stable: SaveAsPrefabAsset overwrites at a fixed path.
    /// </summary>
    public static class DummyTargetPrefabGenerator
    {
        public static void Generate()
        {
            SessionAssetPaths.EnsureAllFolders();

            // Crimson: distinct from Raider teal, projectile yellow, indicator orange, and the two arena grays.
            Material material = RaiderPrefabGenerator.CreateOrUpdateMaterial(
                SessionAssetPaths.DummyTargetMaterial, new Color(0.85f, 0.15f, 0.20f));

            GameObject root = new GameObject("DummyTarget");
            // NetworkObject FIRST on the root - never rely on FishNet's add-on-demand fallback.
            root.AddComponent<NetworkObject>();

            Health health = root.AddComponent<Health>();
            SetSide(health, Side.Hostile);

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform);
            /* Local y=1: with the root placed on the floor (post y=0) the 2-tall
             * capsule spans world y 0..2, so a flat shot at muzzle height (y=1)
             * strikes its middle. The CapsuleCollider is deliberately KEPT. */
            visual.transform.localPosition = new Vector3(0f, 1f, 0f);
            visual.GetComponent<Renderer>().sharedMaterial = material;

            PrefabUtility.SaveAsPrefabAsset(root, SessionAssetPaths.DummyTargetPrefab);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            /* Same contract as the other generators: after Generate() returns the
             * prefab is registered in DefaultPrefabObjects. */
            RefreshDefaultPrefabsMenu.RebuildDefaultPrefabs();
        }

        private static void SetSide(Health health, Side side)
        {
            SerializedObject so = new SerializedObject(health);
            SerializedProperty sideProperty = so.FindProperty("_side");
            if (sideProperty == null)
                throw new InvalidOperationException(
                    "Health renamed serialized field '_side' — update DummyTargetPrefabGenerator.");
            sideProperty.enumValueIndex = (int)side;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
