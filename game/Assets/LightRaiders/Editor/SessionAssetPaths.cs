using UnityEditor;

namespace LightRaiders.Editor
{
    /// <summary>
    /// Central asset-path constants for generated session assets, plus folder helpers.
    /// </summary>
    public static class SessionAssetPaths
    {
        public const string RootFolder = "Assets/LightRaiders";
        public const string PrefabsFolder = RootFolder + "/Prefabs";
        public const string ScenesFolder = RootFolder + "/Scenes";
        public const string MaterialsFolder = RootFolder + "/Materials";

        public const string RaiderPrefab = PrefabsFolder + "/Raider.prefab";
        public const string ProjectilePrefab = PrefabsFolder + "/Projectile.prefab";
        public const string DummyTargetPrefab = PrefabsFolder + "/DummyTarget.prefab";
        public const string HostileEmitterPrefab = PrefabsFolder + "/HostileEmitter.prefab";
        public const string BootstrapArenaScene = ScenesFolder + "/BootstrapArena.unity";
        public const string RaiderMaterial = MaterialsFolder + "/Raider.mat";
        public const string AimIndicatorMaterial = MaterialsFolder + "/AimIndicator.mat";
        public const string ProjectileMaterial = MaterialsFolder + "/Projectile.mat";
        public const string DummyTargetMaterial = MaterialsFolder + "/DummyTarget.mat";
        public const string HostileEmitterMaterial = MaterialsFolder + "/HostileEmitter.mat";
        public const string ArenaFloorMaterial = MaterialsFolder + "/ArenaFloor.mat";
        public const string ArenaObstacleMaterial = MaterialsFolder + "/ArenaObstacle.mat";
        public const string HealthBarBackgroundMaterial = MaterialsFolder + "/HealthBarBackground.mat";
        public const string HealthBarFillMaterial = MaterialsFolder + "/HealthBarFill.mat";

        /// <summary>
        /// FishNet's generated prefab collection; its default location is project-wide.
        /// </summary>
        public const string DefaultPrefabObjects = "Assets/DefaultPrefabObjects.asset";

        /// <summary>
        /// Project-wide Input System action asset created by the Unity template.
        /// </summary>
        public const string InputActions = "Assets/InputSystem_Actions.inputactions";

        /// <summary>
        /// Creates the folder under parent if it does not already exist.
        /// </summary>
        public static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }

        /// <summary>
        /// Ensures every folder used by the session asset generators exists.
        /// </summary>
        public static void EnsureAllFolders()
        {
            EnsureFolder("Assets", "LightRaiders");
            EnsureFolder(RootFolder, "Prefabs");
            EnsureFolder(RootFolder, "Scenes");
            EnsureFolder(RootFolder, "Materials");
        }
    }
}
