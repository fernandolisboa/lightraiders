using FishNet.Editing;
using FishNet.Object;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LightRaiders.Editor
{
    /// <summary>
    /// Builds the graybox Exfil site prefab (#38): a networked, Layer-scoped
    /// marker with a flat disc showing the extraction zone and a beacon pole.
    /// Collider-free; it never moves. Idempotent and GUID-stable.
    /// </summary>
    public static class ExfilSitePrefabGenerator
    {
        public static void Generate()
        {
            SessionAssetPaths.EnsureAllFolders();

            // Green: the "way out" colour, distinct from door yellow and gate violet.
            Material material = RaiderPrefabGenerator.CreateOrUpdateMaterial(
                SessionAssetPaths.ExfilZoneMaterial, new Color(0.20f, 0.80f, 0.35f));

            GameObject root = new GameObject("ExfilSite");
            root.AddComponent<NetworkObject>();
            LayerScopingGenerator.AddLayerScoping(root);
            root.AddComponent<ExfilSite>();

            GameObject zone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            zone.name = "Zone";
            zone.transform.SetParent(root.transform);
            zone.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            float diameter = ExfilSite.ZoneRadius * 2f;
            zone.transform.localScale = new Vector3(diameter, 0.01f, diameter);
            Object.DestroyImmediate(zone.GetComponent<Collider>());
            zone.GetComponent<Renderer>().sharedMaterial = material;

            GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beacon.name = "Beacon";
            beacon.transform.SetParent(root.transform);
            beacon.transform.localPosition = new Vector3(0f, 2f, 0f);
            beacon.transform.localScale = new Vector3(0.4f, 2f, 0.4f);
            Object.DestroyImmediate(beacon.GetComponent<Collider>());
            beacon.GetComponent<Renderer>().sharedMaterial = material;

            PrefabUtility.SaveAsPrefabAsset(root, SessionAssetPaths.ExfilSitePrefab);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            RefreshDefaultPrefabsMenu.RebuildDefaultPrefabs();
        }
    }
}
