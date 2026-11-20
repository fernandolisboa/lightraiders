using FishNet.Editing;
using FishNet.Object;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LightRaiders.Editor
{
    /// <summary>
    /// Builds the graybox Waygate prefab (#37): a networked, Layer-scoped pad that
    /// never moves (its pose replicates once in the spawn message), with a
    /// collider-free visual so it is a marker, not an obstacle. Idempotent and
    /// GUID-stable like the other prefab generators.
    /// </summary>
    public static class WaygatePrefabGenerator
    {
        public static void Generate()
        {
            SessionAssetPaths.EnsureAllFolders();

            // Violet: distinct from the door yellow and every other graybox colour.
            Material material = RaiderPrefabGenerator.CreateOrUpdateMaterial(
                SessionAssetPaths.WaygateMaterial, new Color(0.55f, 0.30f, 0.95f));

            GameObject root = new GameObject("Waygate");
            root.AddComponent<NetworkObject>();
            LayerScopingGenerator.AddLayerScoping(root);
            root.AddComponent<Waygate>();

            GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.name = "Pad";
            pad.transform.SetParent(root.transform);
            pad.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            pad.transform.localScale = new Vector3(2.4f, 0.05f, 2.4f);
            Object.DestroyImmediate(pad.GetComponent<Collider>());
            pad.GetComponent<Renderer>().sharedMaterial = material;

            // A thin arch so the gate reads from a distance.
            GameObject arch = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arch.name = "Arch";
            arch.transform.SetParent(root.transform);
            arch.transform.localPosition = new Vector3(0f, 1.5f, -1.3f);
            arch.transform.localScale = new Vector3(2.4f, 3f, 0.2f);
            Object.DestroyImmediate(arch.GetComponent<Collider>());
            arch.GetComponent<Renderer>().sharedMaterial = material;

            PrefabUtility.SaveAsPrefabAsset(root, SessionAssetPaths.WaygatePrefab);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            RefreshDefaultPrefabsMenu.RebuildDefaultPrefabs();
        }
    }
}
