using FishNet.Editing;
using FishNet.Object;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LightRaiders.Editor
{
    /// <summary>
    /// Builds the graybox Hatch prefab (#39): a networked, Layer-scoped low
    /// square lid, collider-free so it never blocks movement. Idempotent and
    /// GUID-stable like the other prefab generators.
    /// </summary>
    public static class HatchPrefabGenerator
    {
        public static void Generate()
        {
            SessionAssetPaths.EnsureAllFolders();

            // Orange-brown: reads as a rusted lid, distinct from Exfil green.
            Material material = RaiderPrefabGenerator.CreateOrUpdateMaterial(
                SessionAssetPaths.HatchMaterial, new Color(0.75f, 0.40f, 0.15f));

            GameObject root = new GameObject("Hatch");
            root.AddComponent<NetworkObject>();
            LayerScopingGenerator.AddLayerScoping(root);
            root.AddComponent<Hatch>();

            GameObject lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lid.name = "Lid";
            lid.transform.SetParent(root.transform);
            lid.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            lid.transform.localScale = new Vector3(1.6f, 0.2f, 1.6f);
            Object.DestroyImmediate(lid.GetComponent<Collider>());
            lid.GetComponent<Renderer>().sharedMaterial = material;

            PrefabUtility.SaveAsPrefabAsset(root, SessionAssetPaths.HatchPrefab);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            RefreshDefaultPrefabsMenu.RebuildDefaultPrefabs();
        }
    }
}
