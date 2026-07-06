using System;
using FishNet.Component.Transforming;
using FishNet.Editing;
using FishNet.Object;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace LightRaiders.Editor
{
    /// <summary>
    /// Generates (or regenerates) the Raider stand-in prefab: a NetworkObject root
    /// with a capsule visual, registered in FishNet's DefaultPrefabObjects collection.
    /// </summary>
    public static class RaiderPrefabGenerator
    {
        public static void Generate()
        {
            SessionAssetPaths.EnsureAllFolders();

            Material material = CreateOrUpdateMaterial(SessionAssetPaths.RaiderMaterial, new Color(0.10f, 0.75f, 0.70f));
            Material indicatorMaterial = CreateOrUpdateMaterial(SessionAssetPaths.AimIndicatorMaterial, new Color(0.95f, 0.45f, 0.10f));

            GameObject root = new GameObject("Raider");
            root.AddComponent<NetworkObject>();
            root.AddComponent<Raider>();

            /* Raiders are damageable as of #17: a Health of Side.Raider so hostile
             * emitter shots (Side.Hostile) strip its Shield then Health, while Raider
             * fire (Side.Raider) still passes through friendly Raiders. The root
             * CharacterController is the collider the projectile sweep hits. */
            Health health = root.AddComponent<Health>();
            SetSide(health, Side.Raider);

            /* Death consequences (#18): on zero Health the RaiderDeathHandler hands
             * off to the session RaiderRespawner - drop a loot bag, respawn the owner
             * after a delay. Server-only; harmless on a Raider that never dies. */
            root.AddComponent<RaiderDeathHandler>();

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform);
            visual.transform.localPosition = new Vector3(0f, 1f, 0f);
            /* The root CharacterController is now the sole collider; an overlapping
             * child collider would make the CC sweep ghost-collide against itself. */
            Object.DestroyImmediate(visual.GetComponent<CapsuleCollider>());
            visual.GetComponent<Renderer>().sharedMaterial = material;

            /* Graybox aim indicator: a bar along local +Z (= transform.forward) so the
             * server-set facing is readable on every client. NetworkTransform replicates
             * the ROOT rotation; children ride along rigidly. Spans local z 0.5..1.3:
             * flush with the capsule surface, protruding 0.8 at mid-height. */
            GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            indicator.name = "AimIndicator";
            indicator.transform.SetParent(root.transform);
            indicator.transform.localPosition = new Vector3(0f, 1f, 0.9f);
            indicator.transform.localScale = new Vector3(0.15f, 0.15f, 0.8f);
            /* Same rule as Visual: the root CharacterController is the sole collider -
             * a Cube primitive ships a BoxCollider, not a CapsuleCollider. */
            Object.DestroyImmediate(indicator.GetComponent<BoxCollider>());
            indicator.GetComponent<Renderer>().sharedMaterial = indicatorMaterial;

            // Matches the visual capsule (2 units tall, 0.5 radius, centered at y=1).
            CharacterController controller = root.AddComponent<CharacterController>();
            controller.center = new Vector3(0f, 1f, 0f);
            controller.height = 2f;
            controller.radius = 0.5f;

            AddInputIntentProvider(root);
            root.AddComponent<RaiderMovement>();
            AddWeapon(root);
            AddNetworkTransform(root);

            PrefabUtility.SaveAsPrefabAsset(root, SessionAssetPaths.RaiderPrefab);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            /* Synchronously rebuild FishNet's DefaultPrefabObjects so the new/changed
             * prefab is registered without waiting for an asset postprocessor pass.
             * RefreshDefaultPrefabsMenu.RebuildDefaultPrefabs is the public entry point;
             * the internal Generator class is not callable from here. */
            RefreshDefaultPrefabsMenu.RebuildDefaultPrefabs();
        }

        /* These helpers fail fast with InvalidOperationException: -executeMethod
         * exits 0 on silent partial success, so a missing asset or a renamed
         * serialized field must abort loudly. */

        private static void SetSide(Health health, Side side)
        {
            SerializedObject so = new SerializedObject(health);
            SerializedProperty sideProperty = so.FindProperty("_side");
            if (sideProperty == null)
                throw new InvalidOperationException(
                    "Health renamed serialized field '_side' — update RaiderPrefabGenerator.");
            sideProperty.enumValueIndex = (int)side;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddInputIntentProvider(GameObject root)
        {
            RaiderInputIntentProvider provider = root.AddComponent<RaiderInputIntentProvider>();

            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(SessionAssetPaths.InputActions);
            if (actions == null)
                throw new InvalidOperationException(
                    "Input action asset missing at '" + SessionAssetPaths.InputActions + "' — restore it or update SessionAssetPaths.");

            SerializedObject serializedProvider = new SerializedObject(provider);
            SerializedProperty actionsProperty = serializedProvider.FindProperty("_actions");
            if (actionsProperty == null)
                throw new InvalidOperationException(
                    "RaiderInputIntentProvider renamed serialized field '_actions' — update RaiderPrefabGenerator.");
            actionsProperty.objectReferenceValue = actions;
            serializedProvider.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddWeapon(GameObject root)
        {
            RaiderWeapon weapon = root.AddComponent<RaiderWeapon>();

            NetworkObject projectilePrefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(SessionAssetPaths.ProjectilePrefab);
            if (projectilePrefab == null)
                throw new InvalidOperationException(
                    "Projectile prefab missing at '" + SessionAssetPaths.ProjectilePrefab +
                    "' — ProjectilePrefabGenerator.Generate() must run first (-executeMethod exits 0 on silent partial success).");

            SerializedObject serializedWeapon = new SerializedObject(weapon);
            SerializedProperty prefabProperty = serializedWeapon.FindProperty("_projectilePrefab");
            if (prefabProperty == null)
                throw new InvalidOperationException(
                    "RaiderWeapon renamed serialized field '_projectilePrefab' — update RaiderPrefabGenerator.");
            prefabProperty.objectReferenceValue = projectilePrefab;
            serializedWeapon.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddNetworkTransform(GameObject root)
        {
            /* Server-authoritative per ADR-0005. The CharacterController component
             * configuration auto-disables the CC on non-server instances, and
             * _sendToOwner's default (true) streams server positions back to the
             * owner — no prediction in Phase 0. */
            NetworkTransform networkTransform = root.AddComponent<NetworkTransform>();

            SerializedObject serializedTransform = new SerializedObject(networkTransform);
            SerializedProperty clientAuthoritative = serializedTransform.FindProperty("_clientAuthoritative");
            if (clientAuthoritative == null)
                throw new InvalidOperationException(
                    "FishNet renamed NetworkTransform serialized field '_clientAuthoritative' — update RaiderPrefabGenerator.");
            clientAuthoritative.boolValue = false;
            SerializedProperty componentConfiguration = serializedTransform.FindProperty("_componentConfiguration");
            if (componentConfiguration == null)
                throw new InvalidOperationException(
                    "FishNet renamed NetworkTransform serialized field '_componentConfiguration' — update RaiderPrefabGenerator.");
            componentConfiguration.intValue = (int)NetworkTransform.ComponentConfigurationType.CharacterController;
            serializedTransform.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static Material CreateOrUpdateMaterial(string assetPath, Color baseColor)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, assetPath);
            }

            // _BaseColor is the URP Lit base color property.
            material.SetColor("_BaseColor", baseColor);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
