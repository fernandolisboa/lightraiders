using FishNet.Editing;
using FishNet.Object;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LightRaiders.Editor
{
    /// <summary>
    /// Builds the graybox LootBag prefab: a networked marker dropped where a Raider
    /// died, holding nothing (contents arrive with the Phase 4 economy) and
    /// despawning on its own lifetime. No collider (nothing interacts with it yet)
    /// and no NetworkTransform (it never moves - its drop pose replicates once in
    /// the spawn message). Idempotent and GUID-stable.
    /// </summary>
    public static class LootBagPrefabGenerator
    {
        public static void Generate()
        {
            SessionAssetPaths.EnsureAllFolders();

            // Amber/brown: distinct from Raider teal, projectile yellow, target crimson, emitter purple, and the arena grays.
            Material material = RaiderPrefabGenerator.CreateOrUpdateMaterial(
                SessionAssetPaths.LootBagMaterial, new Color(0.62f, 0.42f, 0.12f));

            GameObject root = new GameObject("LootBag");
            // NetworkObject FIRST on the root - never rely on FishNet's add-on-demand fallback.
            root.AddComponent<NetworkObject>();
            root.AddComponent<LootBag>();

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform);
            // A small low cube sitting on the floor at a Raider's feet (root spawned at the death position).
            visual.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            visual.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
            /* No collisions: the bag is a pure marker this slice. Primitives ship a
             * collider - destroy it (same rule as the Raider/Projectile visuals). */
            Object.DestroyImmediate(visual.GetComponent<BoxCollider>());
            visual.GetComponent<Renderer>().sharedMaterial = material;

            PrefabUtility.SaveAsPrefabAsset(root, SessionAssetPaths.LootBagPrefab);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            RefreshDefaultPrefabsMenu.RebuildDefaultPrefabs();
        }
    }
}
