using System.Collections.Generic;
using FishNet.Managing;
using FishNet.Object;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LightRaiders.Editor
{
    /// <summary>
    /// Generates the graybox Matins map (PRD #31, issue #36): three Surface
    /// districts, eight interior Layers tagged Small/Large, and one Undercroft
    /// Layer with an entrance in each district, all joined by linked Doors.
    /// Per ADR-0008 every Layer lives in the one physics scene, its geometry placed
    /// far from every other Layer's so nothing can be seen or shot across.
    /// Idempotent like the arena: reruns rebuild the generated roots in place.
    /// Also places the Waygate pair (#37); Exfil sites and Hatches come with #38-#39.
    /// </summary>
    public static class MatinsGenerator
    {
        public const string RootName = "Matins";

        public const int UndercroftLayer = 1;
        public const int FirstInteriorLayer = 2;
        public const int DistrictCount = 3;

        /* Surface: three 40 m square districts side by side along X, centred on
         * the origin, split by divider walls with one gap each (Surface travel
         * stays possible; the Undercroft is the faster route). */
        private const float DistrictSize = 40f;
        private const float WallHeight = 3f;
        private const float WallThickness = 1f;
        private const float DividerGap = 6f;

        /* Separation (ADR-0008): every non-Surface Layer sits 100+ metres
         * from the Surface footprint and from each other, well past any shot's
         * travel, so a Layer boundary never needs a physics rule. */
        private static readonly Vector3 UndercroftOrigin = new Vector3(0f, 0f, -600f);
        private static readonly Vector3 FirstInteriorOrigin = new Vector3(600f, 0f, 0f);
        private const float InteriorSpacing = 150f;
        private const float UndercroftWidth = 6f;

        private const float SmallInteriorSize = 12f;
        private const float LargeInteriorSize = 24f;

        /* Graybox names for the HUD; placeholders until the map gets its art pass. */
        private static readonly string[] DistrictNames = { "West Ward", "Central Ward", "East Ward" };
        private static readonly string[] InteriorNames =
        {
            "Depot", "Clinic", "Archive", "Foundry", "Warehouse", "Pumphouse", "Signal Hall", "Granary"
        };

        /// <summary>Interior i is Large when this holds; the rest are Small (3 Large, 5 Small).</summary>
        private static bool IsLarge(int interiorIndex) => interiorIndex % 3 == 0;

        /* Per-district building slots (district-local XZ). Interiors fill slots in
         * order, district by district: 3 + 3 + 2 = 8. */
        private static readonly Vector2[] BuildingSlots = { new Vector2(-10f, 10f), new Vector2(10f, 10f), new Vector2(-10f, -10f) };
        private static readonly int[] InteriorsPerDistrict = { 3, 3, 2 };
        private static readonly Vector2 UndercroftEntranceSlot = new Vector2(10f, -10f);
        private static readonly Vector2[] SpawnSlots = { new Vector2(-4f, 0f), new Vector2(4f, 0f) };

        /* Waygate pair (#37): one gate at the outer edge of each end district,
         * facing inward, linking the two ends of the map. District-local XZ. */
        private const int WaygateDistrictA = 0;
        private const int WaygateDistrictB = DistrictCount - 1;
        private static readonly Vector2 WaygateSlotA = new Vector2(-15f, 0f);
        private static readonly Vector2 WaygateSlotB = new Vector2(15f, 0f);

        private const float BuildingSize = 6f;
        private const float BuildingHeight = 4f;
        private const float EntranceSize = 3f;
        /// <summary>Doors sit this far out from the face they belong to.</summary>
        private const float DoorStandOff = 0.5f;
        private const float SpawnHeight = 1f;

        /// <summary>Materials the map is painted with; any may be null (tests skip asset writes).</summary>
        public sealed class Materials
        {
            public Material Floor;
            public Material Wall;
            public Material Building;
            public Material Interior;
            public Material Undercroft;
            public Material Door;
        }

        /// <summary>What <see cref="BuildMap"/> produced.</summary>
        public sealed class Layout
        {
            public GameObject Root;
            public Transform[] SpawnPoints;
            /// <summary>The Waygate pair's two posts (#37), West and East Ward.</summary>
            public Transform[] WaygatePosts;
        }

        /// <summary>
        /// Regenerates Matins.unity. Assumes the session prefabs exist, so it runs
        /// at the end of <see cref="ArenaSceneGenerator.GenerateAll"/>; the menu
        /// item reruns only the map. CLI:
        /// -executeMethod LightRaiders.Editor.MatinsGenerator.Generate
        /// </summary>
        [MenuItem("Light Raiders/Generate Matins")]
        public static void Generate()
        {
            SessionAssetPaths.EnsureAllFolders();
            Materials materials = CreateMaterials();

            Scene scene;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionAssetPaths.MatinsScene) != null)
            {
                scene = EditorSceneManager.OpenScene(SessionAssetPaths.MatinsScene, OpenSceneMode.Single);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, SessionAssetPaths.MatinsScene);
            }

            foreach (GameObject rootGo in scene.GetRootGameObjects())
            {
                if (rootGo.name == RootName || ArenaSceneGenerator.IsGeneratedSessionRoot(rootGo.name))
                    Object.DestroyImmediate(rootGo);
            }

            Layout layout = BuildMap(materials);
            NetworkManager networkManager = ArenaSceneGenerator.BuildNetworkSession(layout.SpawnPoints);
            ArenaSceneGenerator.BuildCameraRig(networkManager);
            BuildWaygateSpawner(networkManager, layout.WaygatePosts);
            HudGenerator.BuildHud(networkManager);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static Materials CreateMaterials()
        {
            return new Materials
            {
                Floor = RaiderPrefabGenerator.CreateOrUpdateMaterial(SessionAssetPaths.ArenaFloorMaterial, new Color(0.55f, 0.55f, 0.55f)),
                Wall = RaiderPrefabGenerator.CreateOrUpdateMaterial(SessionAssetPaths.ArenaObstacleMaterial, new Color(0.30f, 0.30f, 0.32f)),
                Building = RaiderPrefabGenerator.CreateOrUpdateMaterial(SessionAssetPaths.MatinsBuildingMaterial, new Color(0.62f, 0.58f, 0.50f)),
                Interior = RaiderPrefabGenerator.CreateOrUpdateMaterial(SessionAssetPaths.MatinsInteriorMaterial, new Color(0.45f, 0.40f, 0.35f)),
                Undercroft = RaiderPrefabGenerator.CreateOrUpdateMaterial(SessionAssetPaths.MatinsUndercroftMaterial, new Color(0.16f, 0.16f, 0.20f)),
                Door = RaiderPrefabGenerator.CreateOrUpdateMaterial(SessionAssetPaths.MatinsDoorMaterial, new Color(0.95f, 0.80f, 0.25f)),
            };
        }

        /// <summary>
        /// Builds the whole map under one "Matins" root without touching any scene
        /// or asset, so the smoke test can build and inspect it in EditMode.
        /// </summary>
        public static Layout BuildMap(Materials materials)
        {
            materials ??= new Materials();
            GameObject root = new GameObject(RootName);

            GameObject surface = CreateLayerRoot(root.transform, "Surface", LayerMember.Surface, "Surface", LayerSize.Open, Vector3.zero);
            BuildSurfaceShell(surface.transform, materials);

            GameObject undercroft = CreateLayerRoot(root.transform, "Undercroft", UndercroftLayer, "Undercroft", LayerSize.Open, UndercroftOrigin);
            Door[] undercroftDoors = BuildUndercroft(undercroft.transform, materials);

            List<Transform> spawnPoints = new List<Transform>();
            Transform[] waygatePosts = new Transform[2];
            int interiorIndex = 0;
            for (int d = 0; d < DistrictCount; d++)
            {
                Vector3 centre = DistrictCentre(d);
                GameObject districtGo = new GameObject("District" + (d + 1));
                districtGo.transform.SetParent(surface.transform, false);
                districtGo.transform.localPosition = centre;
                districtGo.AddComponent<District>().Configure(d, DistrictNames[d]);

                for (int s = 0; s < InteriorsPerDistrict[d]; s++, interiorIndex++)
                {
                    Door outside = BuildBuilding(districtGo.transform, BuildingSlots[s], "Building" + (s + 1), materials);
                    Door inside = BuildInterior(root.transform, interiorIndex, materials);
                    Door.Link(outside, inside);
                }

                Door entrance = BuildUndercroftEntrance(districtGo.transform, UndercroftEntranceSlot, materials);
                Door.Link(entrance, undercroftDoors[d]);

                for (int p = 0; p < SpawnSlots.Length; p++)
                {
                    GameObject spawn = new GameObject("SpawnPoint" + (p + 1));
                    spawn.transform.SetParent(districtGo.transform, false);
                    spawn.transform.localPosition = new Vector3(SpawnSlots[p].x, SpawnHeight, SpawnSlots[p].y);
                    spawnPoints.Add(spawn.transform);
                }

                if (d == WaygateDistrictA)
                    waygatePosts[0] = CreatePost(districtGo.transform, "WaygatePost", WaygateSlotA, Vector3.right);
                if (d == WaygateDistrictB)
                    waygatePosts[1] = CreatePost(districtGo.transform, "WaygatePost", WaygateSlotB, Vector3.left);

                CreateBox(districtGo.transform, "Cover", new Vector3(0f, 0.75f, 6f), new Vector3(4f, 1.5f, 1f), materials.Wall);
            }

            return new Layout { Root = root, SpawnPoints = spawnPoints.ToArray(), WaygatePosts = waygatePosts };
        }

        private static Transform CreatePost(Transform parent, string name, Vector2 slot, Vector3 facing)
        {
            GameObject post = new GameObject(name);
            post.transform.SetParent(parent, false);
            post.transform.localPosition = new Vector3(slot.x, 0f, slot.y);
            post.transform.localRotation = Quaternion.LookRotation(facing, Vector3.up);
            return post.transform;
        }

        /// <summary>The Layer a post stands in: its nearest LayerInfo ancestor.</summary>
        internal static int LayerOf(Transform t)
        {
            LayerInfo info = t.GetComponentInParent<LayerInfo>(true);
            return info != null ? info.LayerId : LayerMember.Surface;
        }

        private static void BuildWaygateSpawner(NetworkManager networkManager, Transform[] posts)
        {
            NetworkObject prefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(SessionAssetPaths.WaygatePrefab);
            if (prefab == null)
                throw new System.InvalidOperationException("Expected the Waygate prefab at '" + SessionAssetPaths.WaygatePrefab + "' - run GenerateAll so WaygatePrefabGenerator creates it first.");

            WaygateSpawner spawner = networkManager.gameObject.AddComponent<WaygateSpawner>();
            spawner.SetNetworkManager(networkManager);
            spawner.SetWaygatePrefab(prefab);
            spawner.SetPosts(posts[0], LayerOf(posts[0]), posts[1], LayerOf(posts[1]));
            EditorUtility.SetDirty(spawner);
        }

        internal static Vector3 DistrictCentre(int district) => new Vector3((district - (DistrictCount - 1) * 0.5f) * DistrictSize, 0f, 0f);

        internal static Vector3 InteriorOrigin(int interiorIndex) => FirstInteriorOrigin + new Vector3(interiorIndex * InteriorSpacing, 0f, 0f);

        private static GameObject CreateLayerRoot(Transform parent, string name, int layerId, string displayName, LayerSize size, Vector3 origin)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = origin;
            go.AddComponent<LayerInfo>().Configure(layerId, displayName, size);
            return go;
        }

        private static void BuildSurfaceShell(Transform surface, Materials materials)
        {
            float length = DistrictSize * DistrictCount;
            float half = length * 0.5f;
            float halfDepth = DistrictSize * 0.5f;

            CreateBox(surface, "Floor", new Vector3(0f, -0.5f, 0f), new Vector3(length, 1f, DistrictSize), materials.Floor);
            BuildPerimeter(surface, Vector3.zero, half, halfDepth, materials.Wall);

            // Divider walls between districts, each with one central gap.
            float segment = (DistrictSize - DividerGap) * 0.5f;
            for (int d = 1; d < DistrictCount; d++)
            {
                float x = -half + d * DistrictSize;
                float offset = DividerGap * 0.5f + segment * 0.5f;
                CreateBox(surface, "Divider" + d + "N", new Vector3(x, WallHeight * 0.5f, offset), new Vector3(WallThickness, WallHeight, segment), materials.Wall);
                CreateBox(surface, "Divider" + d + "S", new Vector3(x, WallHeight * 0.5f, -offset), new Vector3(WallThickness, WallHeight, segment), materials.Wall);
            }
        }

        /// <summary>Four walls enclosing a halfX by halfZ rectangle around a local centre.</summary>
        private static void BuildPerimeter(Transform parent, Vector3 centre, float halfX, float halfZ, Material material)
        {
            float y = WallHeight * 0.5f;
            float spanX = halfX * 2f + WallThickness * 2f;
            float spanZ = halfZ * 2f;
            CreateBox(parent, "WallN", centre + new Vector3(0f, y, halfZ + WallThickness * 0.5f), new Vector3(spanX, WallHeight, WallThickness), material);
            CreateBox(parent, "WallS", centre + new Vector3(0f, y, -halfZ - WallThickness * 0.5f), new Vector3(spanX, WallHeight, WallThickness), material);
            CreateBox(parent, "WallE", centre + new Vector3(halfX + WallThickness * 0.5f, y, 0f), new Vector3(WallThickness, WallHeight, spanZ), material);
            CreateBox(parent, "WallW", centre + new Vector3(-halfX - WallThickness * 0.5f, y, 0f), new Vector3(WallThickness, WallHeight, spanZ), material);
        }

        /// <summary>
        /// A solid building block on the Surface with its Door on the face that
        /// looks toward the district's centre line (z = 0), facing outward.
        /// </summary>
        private static Door BuildBuilding(Transform district, Vector2 slot, string name, Materials materials)
        {
            GameObject building = new GameObject(name);
            building.transform.SetParent(district, false);
            building.transform.localPosition = new Vector3(slot.x, 0f, slot.y);
            CreateBox(building.transform, "Block", new Vector3(0f, BuildingHeight * 0.5f, 0f), new Vector3(BuildingSize, BuildingHeight, BuildingSize), materials.Building);

            float facing = slot.y > 0f ? -1f : 1f;
            Vector3 doorPos = new Vector3(0f, 0f, facing * (BuildingSize * 0.5f + DoorStandOff));
            return CreateDoor(building.transform, "Door", doorPos, new Vector3(0f, 0f, facing), LayerMember.Surface, materials.Door);
        }

        private static Door BuildUndercroftEntrance(Transform district, Vector2 slot, Materials materials)
        {
            GameObject entrance = new GameObject("UndercroftEntrance");
            entrance.transform.SetParent(district, false);
            entrance.transform.localPosition = new Vector3(slot.x, 0f, slot.y);
            CreateBox(entrance.transform, "Stairwell", new Vector3(0f, 0.5f, 0f), new Vector3(EntranceSize, 1f, EntranceSize), materials.Undercroft);

            float facing = slot.y > 0f ? -1f : 1f;
            Vector3 doorPos = new Vector3(0f, 0f, facing * (EntranceSize * 0.5f + DoorStandOff));
            return CreateDoor(entrance.transform, "Door", doorPos, new Vector3(0f, 0f, facing), LayerMember.Surface, materials.Door);
        }

        /// <summary>
        /// One straight tunnel under (well, far from) the Surface, with one door
        /// per district at the matching X, on the south wall facing into the tunnel.
        /// </summary>
        private static Door[] BuildUndercroft(Transform undercroft, Materials materials)
        {
            float length = DistrictSize * DistrictCount;
            float halfWidth = UndercroftWidth * 0.5f;
            CreateBox(undercroft, "Floor", new Vector3(0f, -0.5f, 0f), new Vector3(length, 1f, UndercroftWidth), materials.Undercroft);
            BuildPerimeter(undercroft, Vector3.zero, length * 0.5f, halfWidth, materials.Wall);

            Door[] doors = new Door[DistrictCount];
            for (int d = 0; d < DistrictCount; d++)
            {
                /* Same X as the district's entrance, so the tunnel reads as running
                 * beneath the districts. The exit lands mid-tunnel. */
                float x = DistrictCentre(d).x + UndercroftEntranceSlot.x;
                Vector3 doorPos = new Vector3(x, 0f, -halfWidth + DoorStandOff);
                doors[d] = CreateDoor(undercroft, "Entrance" + (d + 1), doorPos, Vector3.forward, UndercroftLayer, materials.Door, halfWidth - DoorStandOff);
            }

            return doors;
        }

        /// <summary>
        /// One enclosed room in its own Layer, far from everything else, with its
        /// Door on the south wall facing into the room.
        /// </summary>
        private static Door BuildInterior(Transform mapRoot, int interiorIndex, Materials materials)
        {
            bool large = IsLarge(interiorIndex);
            int layerId = FirstInteriorLayer + interiorIndex;
            float size = large ? LargeInteriorSize : SmallInteriorSize;
            float half = size * 0.5f;

            GameObject interior = CreateLayerRoot(mapRoot, "Interior" + (interiorIndex + 1), layerId, InteriorNames[interiorIndex],
                large ? LayerSize.Large : LayerSize.Small, InteriorOrigin(interiorIndex));
            CreateBox(interior.transform, "Floor", new Vector3(0f, -0.5f, 0f), new Vector3(size, 1f, size), materials.Interior);
            BuildPerimeter(interior.transform, Vector3.zero, half, half, materials.Wall);

            // Close-quarters cover: one block in a Small room, two in a Large one.
            CreateBox(interior.transform, "Cover1", new Vector3(half * 0.4f, 0.75f, half * 0.3f), new Vector3(2f, 1.5f, 2f), materials.Wall);
            if (large)
                CreateBox(interior.transform, "Cover2", new Vector3(-half * 0.4f, 0.75f, half * 0.5f), new Vector3(3f, 1.5f, 1f), materials.Wall);

            Vector3 doorPos = new Vector3(0f, 0f, -half + DoorStandOff);
            return CreateDoor(interior.transform, "Door", doorPos, Vector3.forward, layerId, materials.Door);
        }

        /// <summary>
        /// A Door marker at floor level facing <paramref name="facing"/>, with an
        /// explicit exit point in front of it and a collider-free visual slab.
        /// </summary>
        private static Door CreateDoor(Transform parent, string name, Vector3 localPosition, Vector3 facing, int layerId, Material material,
            float exitDistance = Door.DefaultExitDistance)
        {
            GameObject doorGo = new GameObject(name);
            doorGo.transform.SetParent(parent, false);
            doorGo.transform.localPosition = localPosition;
            doorGo.transform.localRotation = Quaternion.LookRotation(facing, Vector3.up);

            GameObject exit = new GameObject("ExitPoint");
            exit.transform.SetParent(doorGo.transform, false);
            exit.transform.localPosition = new Vector3(0f, 0f, exitDistance);

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(doorGo.transform, false);
            visual.transform.localPosition = new Vector3(0f, 1.1f, -0.3f);
            visual.transform.localScale = new Vector3(1.4f, 2.2f, 0.2f);
            /* No collider: the door is a teleport marker, never an obstacle. */
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            if (material != null)
                visual.GetComponent<Renderer>().sharedMaterial = material;

            Door door = doorGo.AddComponent<Door>();
            door.Configure(layerId, null, exit.transform);
            return door;
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 localPosition, Vector3 scale, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = scale;
            if (material != null)
                box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }
    }
}
