using FishNet.Editing;
using FishNet.Object;
using UnityEditor;
using UnityEngine;

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

            GameObject root = new GameObject("Raider");
            root.AddComponent<NetworkObject>();
            root.AddComponent<Raider>();

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform);
            visual.transform.localPosition = new Vector3(0f, 1f, 0f);
            // The CapsuleCollider from the primitive is kept intentionally.
            visual.GetComponent<Renderer>().sharedMaterial = material;

            PrefabUtility.SaveAsPrefabAsset(root, SessionAssetPaths.RaiderPrefab);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            /* Synchronously rebuild FishNet's DefaultPrefabObjects so the new/changed
             * prefab is registered without waiting for an asset postprocessor pass.
             * RefreshDefaultPrefabsMenu.RebuildDefaultPrefabs is the public entry point;
             * the internal Generator class is not callable from here. */
            RefreshDefaultPrefabsMenu.RebuildDefaultPrefabs();
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
