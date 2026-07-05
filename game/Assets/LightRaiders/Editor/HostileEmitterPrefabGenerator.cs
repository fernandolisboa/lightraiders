using System;
using FishNet.Editing;
using FishNet.Object;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LightRaiders.Editor
{
    /// <summary>
    /// Builds the graybox HostileEmitter prefab: a stationary, server-authoritative
    /// network object carrying Health (Side.Hostile) so it fits the one damage
    /// model (and, in #19, a world-space bar), and a HostileEmitter that fires the
    /// shared Projectile at Raiders. Like DummyTarget, the pillar visual KEEPS its
    /// collider - the projectile sweep raycasts colliders, so the emitter needs one
    /// to be shootable and to carry that future bar. No NetworkTransform: an emitter
    /// never moves, so its pose replicates once in the spawn message. Idempotent and
    /// GUID-stable: SaveAsPrefabAsset overwrites at a fixed path.
    /// </summary>
    public static class HostileEmitterPrefabGenerator
    {
        public static void Generate()
        {
            SessionAssetPaths.EnsureAllFolders();

            // Deep purple: distinct from Raider teal, projectile yellow, target crimson, indicator orange, the two arena grays, and the reserved magenta/pink.
            Material material = RaiderPrefabGenerator.CreateOrUpdateMaterial(
                SessionAssetPaths.HostileEmitterMaterial, new Color(0.35f, 0.12f, 0.55f));

            GameObject root = new GameObject("HostileEmitter");
            // NetworkObject FIRST on the root - never rely on FishNet's add-on-demand fallback.
            root.AddComponent<NetworkObject>();

            Health health = root.AddComponent<Health>();
            SetSide(health, Side.Hostile);

            AddEmitter(root);

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform);
            /* A 1x2x1 pillar centered at local y=1: with the root placed on the floor
             * (post y=0) it spans world y 0..2, so a flat shot at muzzle height (y=1)
             * strikes its middle. The BoxCollider is deliberately KEPT (shootable +
             * future world-space bar). Cube, not the target's capsule, to read as a
             * distinct kind of object. */
            visual.transform.localPosition = new Vector3(0f, 1f, 0f);
            visual.transform.localScale = new Vector3(1f, 2f, 1f);
            visual.GetComponent<Renderer>().sharedMaterial = material;

            PrefabUtility.SaveAsPrefabAsset(root, SessionAssetPaths.HostileEmitterPrefab);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            /* Same contract as the other generators: after Generate() returns the
             * prefab is registered in DefaultPrefabObjects. */
            RefreshDefaultPrefabsMenu.RebuildDefaultPrefabs();
        }

        /* Fail fast with InvalidOperationException: -executeMethod exits 0 on silent
         * partial success, so a missing asset or a renamed serialized field must
         * abort loudly (same rule as RaiderPrefabGenerator.AddWeapon). */
        private static void AddEmitter(GameObject root)
        {
            HostileEmitter emitter = root.AddComponent<HostileEmitter>();

            NetworkObject projectilePrefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(SessionAssetPaths.ProjectilePrefab);
            if (projectilePrefab == null)
                throw new InvalidOperationException(
                    "Projectile prefab missing at '" + SessionAssetPaths.ProjectilePrefab +
                    "' — ProjectilePrefabGenerator.Generate() must run first (-executeMethod exits 0 on silent partial success).");

            SerializedObject serializedEmitter = new SerializedObject(emitter);
            SerializedProperty prefabProperty = serializedEmitter.FindProperty("_projectilePrefab");
            if (prefabProperty == null)
                throw new InvalidOperationException(
                    "HostileEmitter renamed serialized field '_projectilePrefab' — update HostileEmitterPrefabGenerator.");
            prefabProperty.objectReferenceValue = projectilePrefab;
            serializedEmitter.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSide(Health health, Side side)
        {
            SerializedObject so = new SerializedObject(health);
            SerializedProperty sideProperty = so.FindProperty("_side");
            if (sideProperty == null)
                throw new InvalidOperationException(
                    "Health renamed serialized field '_side' — update HostileEmitterPrefabGenerator.");
            sideProperty.enumValueIndex = (int)side;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
