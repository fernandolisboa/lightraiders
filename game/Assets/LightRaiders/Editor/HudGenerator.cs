using System;
using FishNet.Managing;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LightRaiders.Editor
{
    /// <summary>
    /// Authors the graybox HUD into the open arena scene: a screen-space Canvas with
    /// two Filled Image bars (Shield over Health) wired to a RaiderHud, plus the
    /// client-local WorldSpaceHealthBars manager wired with its graybox bar
    /// materials. Same fail-fast, idempotent style as the other generators - the
    /// scene roots it creates are torn down by ArenaSceneGenerator's naming-contract
    /// sweep before each rebuild (see ScreenHudRootName / WorldBarsRootName).
    ///
    /// The world-space bars themselves are built dynamically at runtime by the
    /// manager, not baked into any prefab, so #17's damageable-prefab work never
    /// collides with them; this generator only authors the screen-space Canvas and
    /// wires the two client-local managers.
    /// </summary>
    public static class HudGenerator
    {
        /* Root-object naming contract, mirrored in ArenaSceneGenerator's idempotency
         * sweep: these roots are destroyed by name before every rebuild. */
        public const string ScreenHudRootName = "RaiderHud";
        public const string WorldBarsRootName = "WorldSpaceHealthBars";

        // Unity's built-in sliced UI sprite; gives the Filled Images something to fill.
        private const string BuiltinUiSprite = "UI/Skin/UISprite.psd";

        // Graybox bar geometry (screen pixels at the reference resolution).
        private static readonly Vector2 BarSize = new Vector2(320f, 22f);
        private static readonly Vector2 HealthBarPosition = new Vector2(24f, 20f);
        private static readonly Vector2 ShieldBarPosition = new Vector2(24f, 48f);

        private static readonly Color BarBackgroundColor = new Color(0.08f, 0.08f, 0.08f, 0.75f);
        private static readonly Color ShieldFillColor = new Color(0.20f, 0.70f, 0.95f, 1f);
        private static readonly Color HealthFillColor = new Color(0.20f, 0.80f, 0.30f, 1f);

        /// <summary>
        /// Builds both HUD roots in the currently open scene and wires them to the
        /// session NetworkManager. Call after the scene is open and the session
        /// exists (ArenaSceneGenerator does so after BuildTargetSpawner).
        /// </summary>
        public static void BuildHud(NetworkManager networkManager)
        {
            SessionAssetPaths.EnsureAllFolders();
            BuildScreenHud(networkManager);
            BuildWorldBars(networkManager);
        }

        private static void BuildScreenHud(NetworkManager networkManager)
        {
            /* Fail fast: -executeMethod exits 0 on silent partial success, so a
             * renamed/removed builtin sprite must abort loudly rather than leave the
             * bars unfillable. */
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(BuiltinUiSprite);
            if (uiSprite == null)
                throw new InvalidOperationException(
                    "Builtin UI sprite '" + BuiltinUiSprite + "' not found — Unity changed its builtin UI resources; update HudGenerator.");

            GameObject root = new GameObject(ScreenHudRootName);
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            /* Sole HUD canvas; a high sort order keeps it above any later world/UI
             * canvases. No GraphicRaycaster/EventSystem: the bars are display-only. */
            canvas.sortingOrder = 100;
            root.AddComponent<CanvasScaler>();

            Image healthFill = CreateBarPair(root.transform, "Health", HealthBarPosition, uiSprite, HealthFillColor);
            Image shieldFill = CreateBarPair(root.transform, "Shield", ShieldBarPosition, uiSprite, ShieldFillColor);

            RaiderHud hud = root.AddComponent<RaiderHud>();
            hud.SetNetworkManager(networkManager);
            hud.SetBars(shieldFill, healthFill);
            EditorUtility.SetDirty(hud);
        }

        /// <summary>
        /// Builds a background + Filled-fill Image pair sharing one rect, and returns
        /// the fill Image (the one RaiderHud drives).
        /// </summary>
        private static Image CreateBarPair(Transform parent, string name, Vector2 anchoredPosition, Sprite sprite, Color fillColor)
        {
            Image background = CreateImage(name + "Background", parent, anchoredPosition, sprite);
            background.type = Image.Type.Sliced;
            background.color = BarBackgroundColor;

            Image fill = CreateImage(name + "Fill", parent, anchoredPosition, sprite);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            fill.color = fillColor;
            return fill;
        }

        private static Image CreateImage(string name, Transform parent, Vector2 anchoredPosition, Sprite sprite)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            // Anchor + pivot bottom-left so the pixel positions read straight from the arena origin.
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = BarSize;

            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            return image;
        }

        private static void BuildWorldBars(NetworkManager networkManager)
        {
            Material background = RaiderPrefabGenerator.CreateOrUpdateMaterial(
                SessionAssetPaths.HealthBarBackgroundMaterial, new Color(0.08f, 0.08f, 0.08f));
            Material fill = RaiderPrefabGenerator.CreateOrUpdateMaterial(
                SessionAssetPaths.HealthBarFillMaterial, new Color(0.20f, 0.80f, 0.30f));

            GameObject root = new GameObject(WorldBarsRootName);
            WorldSpaceHealthBars manager = root.AddComponent<WorldSpaceHealthBars>();
            manager.SetNetworkManager(networkManager);
            manager.SetBarMaterials(background, fill);
            EditorUtility.SetDirty(manager);
        }
    }
}
