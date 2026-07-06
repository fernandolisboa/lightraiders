using System;
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
            NetworkObject networkObject = root.AddComponent<NetworkObject>();
            /* CSP (ADR-0007): enable prediction on the NetworkObject itself. Without
             * this the owner and server run the Replicate/Reconcile, but FishNet
             * never distributes reconcile/state to non-owner OBSERVERS — spectators
             * (and cross-view damage) would freeze. Mirrors the in-repo CC-prediction
             * demo prefab; _predictionType 0 (CharacterController/"Other") and the
             * default smoothing already match, so only this flag differs. */
            EnablePrediction(networkObject);
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
             * predicted facing is readable on every client. RaiderMovement's replicate
             * sets the ROOT rotation (owner + server + spectators); children ride along
             * rigidly. Spans local z 0.5..1.3: flush with the capsule surface,
             * protruding 0.8 at mid-height. */
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
            /* CSP movement (ADR-0007): RaiderMovement drives the transform via
             * Replicate/Reconcile. No NetworkTransform — reconcile owns position
             * AND rotation, and the CharacterController stays enabled on every
             * instance (owner, server, spectators) so each runs the shared sim. */
            root.AddComponent<RaiderMovement>();
            AddWeapon(root);

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

        private static void EnablePrediction(NetworkObject networkObject)
        {
            SerializedObject so = new SerializedObject(networkObject);
            SerializedProperty enablePrediction = so.FindProperty("_enablePrediction");
            if (enablePrediction == null)
                throw new InvalidOperationException(
                    "FishNet renamed NetworkObject serialized field '_enablePrediction' — update RaiderPrefabGenerator.");
            enablePrediction.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

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
