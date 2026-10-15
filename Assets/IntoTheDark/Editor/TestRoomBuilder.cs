using System;
using System.Linq;
using IntoTheDark.Aether;
using IntoTheDark.Cycle;
using IntoTheDark.Game;
using IntoTheDark.Lighting;
using IntoTheDark.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace IntoTheDark.EditorTools
{
    /// <summary>
    /// Menu "Into the Dark > 2. Construire la salle de test" : génère une scène de base sous-marine
    /// plongée dans le noir (grande salle, couloir bas, petite pièce) et le joueur FPS.
    /// Relancer le menu régénère la scène.
    /// </summary>
    public static class TestRoomBuilder
    {
        private const string ScenesFolder = "Assets/IntoTheDark/Scenes";
        private const string ScenePath = ScenesFolder + "/TestRoom.unity";
        private const string MaterialsFolder = "Assets/IntoTheDark/Materials";

        private struct Palette
        {
            public Material Floor, Wall, Ceiling, Pipe, Debris, EmergencyStrip, AetherGlow;
        }

        [MenuItem("Into the Dark/2. Construire la salle de test", priority = 2)]
        public static void Build()
        {
            if (!ProjectSetup.IsUrpActive)
            {
                if (!EditorUtility.DisplayDialog("Into the Dark",
                        "URP n'est pas encore configuré. Le configurer maintenant ?", "Oui", "Annuler"))
                    return;
                ProjectSetup.ConfigureUrp();
                if (!ProjectSetup.IsUrpActive)
                    return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            ConfigureDarkness();
            var palette = CreatePalette();
            var level = new GameObject("Level").transform;
            BuildHall(level, palette);
            BuildCorridor(level, palette);
            BuildSideRoom(level, palette);
            var player = BuildPlayer(new Vector3(0f, 0.05f, -3f));
            BuildPostProcessing();
            BuildCycle();
            new GameObject("GameSession").AddComponent<GameSession>();

            ProjectSetup.EnsureFolder(ScenesFolder);
            EditorSceneManager.SaveScene(scene, ScenePath);
            ProjectSetup.RegisterScenes();

            Selection.activeGameObject = player;
            Debug.Log("[Into the Dark] Salle de test générée : " + ScenePath);
        }

        // ---------- Ambiance ----------

        private static void ConfigureDarkness()
        {
            RenderSettings.skybox = null;
            RenderSettings.sun = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.ambientIntensity = 0f;
            RenderSettings.reflectionIntensity = 0f;

            // Les ténèbres viennent du shader DarknessLit : brouillard opaque hors des LightZone.
            var darkness = new GameObject("Darkness").AddComponent<DarknessSettings>();
            darkness.Apply();

            RenderSettings.fog = false;
            RenderSettings.fogColor = darkness.FogColor;
        }

        private static Palette CreatePalette()
        {
            ProjectSetup.EnsureFolder(MaterialsFolder);
            return new Palette
            {
                Floor = EnvMat("M_SteelFloor", new Color(0.42f, 0.43f, 0.45f), 0.35f, 0.25f),
                Wall = EnvMat("M_SteelWall", new Color(0.55f, 0.58f, 0.6f), 0.45f, 0.35f),
                Ceiling = EnvMat("M_SteelCeiling", new Color(0.32f, 0.33f, 0.35f), 0.25f, 0.15f),
                Pipe = EnvMat("M_Pipe", new Color(0.55f, 0.45f, 0.35f), 0.55f, 0.45f),
                Debris = EnvMat("M_Debris", new Color(0.45f, 0.35f, 0.27f), 0.2f, 0.1f),
                EmergencyStrip = EnvMat("M_EmergencyStrip", Color.black, 0.5f, 0.2f, new Color(0.6f, 0.04f, 0.02f)),
                AetherGlow = EnvMat("M_AetherGlow", Color.black, 0.8f, 0.2f, new Color(0.1f, 0.55f, 0.7f)),
            };
        }

        /// <summary>Matériau de décor : shader DarknessLit, noyé dans les ténèbres hors de la lumière.</summary>
        private static Material EnvMat(string name, Color baseColor, float smoothness, float specular,
            Color? emission = null, float emissionThroughDarkness = 0f, float lightClamp = 0f, bool visibleFromAfar = false)
        {
            var mat = LoadOrCreateMaterial(name, "IntoTheDark/DarknessLit");
            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_SpecularStrength", specular);
            mat.SetColor("_EmissionColor", emission ?? Color.black);
            mat.SetFloat("_EmissionThroughDarkness", emissionThroughDarkness);
            mat.SetFloat("_LightClamp", lightClamp);
            mat.SetFloat("_VisibleFromAfar", visibleFromAfar ? 1f : 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material LoadOrCreateMaterial(string name, string shaderName)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            var shader = Shader.Find(shaderName);
            if (shader == null)
                throw new InvalidOperationException("Shader introuvable : " + shaderName);

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }
            return mat;
        }

        /// <summary>Matériau URP Lit standard (objets tenus en main, non concernés par les ténèbres).</summary>
        private static Material Mat(string name, Color baseColor, float metallic, float smoothness, Color? emission = null)
        {
            var mat = LoadOrCreateMaterial(name, "Universal Render Pipeline/Lit");

            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);

            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------- Géométrie ----------
        // Grande salle : x [-20, 20], z [-16, 16], plafond à 3 m. Phare au centre.
        // Couloir : x [-1.5, 1.5], z [16, 28], plafond à 2,4 m.
        // Petite pièce : x [-4, 4], z [28, 36], plafond à 2,6 m.
        private const float HallHalfX = 20f, HallHalfZ = 16f, HallHeight = 3f;
        private const float CorridorStart = HallHalfZ, CorridorLength = 12f;
        private const float RoomStart = CorridorStart + CorridorLength, RoomDepth = 8f;

        /// <summary>Rayon du phare : jusqu'à 1 m du mur le plus proche.</summary>
        private const float BeaconRadius = HallHalfZ - 1f;

        private static void BuildHall(Transform level, Palette p)
        {
            var hall = Group("Hall", level);
            float w = HallHalfX * 2f, d = HallHalfZ * 2f, h = HallHeight, t = 0.3f;

            Box("Floor", hall, new Vector3(0f, -0.15f, 0f), new Vector3(w, t, d), p.Floor);
            Box("Ceiling", hall, new Vector3(0f, h + 0.15f, 0f), new Vector3(w, t, d), p.Ceiling);
            Box("Wall_West", hall, new Vector3(-HallHalfX - 0.15f, h / 2f, 0f), new Vector3(t, h, d + 0.6f), p.Wall);
            Box("Wall_East", hall, new Vector3(HallHalfX + 0.15f, h / 2f, 0f), new Vector3(t, h, d + 0.6f), p.Wall);
            Box("Wall_South", hall, new Vector3(0f, h / 2f, -HallHalfZ - 0.15f), new Vector3(w + 0.6f, h, t), p.Wall);
            float northSegment = HallHalfX + 0.3f - 1.5f;
            Box("Wall_North_L", hall, new Vector3(-(1.5f + northSegment / 2f), h / 2f, HallHalfZ + 0.15f), new Vector3(northSegment, h, t), p.Wall);
            Box("Wall_North_R", hall, new Vector3(1.5f + northSegment / 2f, h / 2f, HallHalfZ + 0.15f), new Vector3(northSegment, h, t), p.Wall);
            Box("Lintel", hall, new Vector3(0f, 2.7f, HallHalfZ + 0.15f), new Vector3(3f, 0.6f, t), p.Wall);

            // Piliers
            foreach (var x in new[] { -10f, 10f })
            foreach (var z in new[] { -7f, 7f })
                Box("Pillar", hall, new Vector3(x, h / 2f, z), new Vector3(0.8f, h, 0.8f), p.Wall);

            // Tuyères au plafond et le long des murs
            Pipe("Pipe_Ceiling_A", hall, new Vector3(0f, 2.7f, -12f), w, 0.35f, Axis.X, p.Pipe);
            Pipe("Pipe_Ceiling_B", hall, new Vector3(0f, 2.75f, 12.4f), w, 0.25f, Axis.X, p.Pipe);
            Pipe("Pipe_Wall_West", hall, new Vector3(-HallHalfX + 0.4f, 0.6f, 0f), d, 0.3f, Axis.Z, p.Pipe);
            Pipe("Pipe_Wall_East", hall, new Vector3(HallHalfX - 0.4f, 2.2f, 0f), d, 0.25f, Axis.Z, p.Pipe);
            Pipe("Pipe_Vertical_NE", hall, new Vector3(HallHalfX - 0.5f, h / 2f, HallHalfZ - 0.6f), h, 0.45f, Axis.Y, p.Pipe);
            Pipe("Pipe_Vertical_SW", hall, new Vector3(-HallHalfX + 0.5f, h / 2f, -HallHalfZ + 0.6f), h, 0.45f, Axis.Y, p.Pipe);

            // Débris
            Box("Debris_1", hall, new Vector3(13f, 0.3f, -11f), new Vector3(1.2f, 0.6f, 0.8f), p.Debris, yaw: 20f);
            Box("Debris_2", hall, new Vector3(14.8f, 0.2f, -9.2f), new Vector3(0.6f, 0.4f, 0.6f), p.Debris, yaw: -35f);
            Box("Debris_3", hall, new Vector3(-14f, 0.4f, 10f), new Vector3(1.6f, 0.8f, 1f), p.Debris, yaw: 70f);
            Box("Debris_4", hall, new Vector3(-6f, 0.25f, -9f), new Vector3(0.9f, 0.5f, 0.7f), p.Debris, yaw: 10f);
            Box("Debris_5", hall, new Vector3(17f, 0.35f, 6f), new Vector3(1.1f, 0.7f, 1.3f), p.Debris, yaw: -50f);

            // Tuyère d'éther, et le phare posé dessus
            var vent = Pipe("AetherVent", hall, new Vector3(0f, 0.15f, 0f), 0.3f, 1.6f, Axis.Y, p.Pipe);
            // Un CapsuleCollider sur un cylindre plat ferait un dôme invisible : boîte à la place.
            UnityEngine.Object.DestroyImmediate(vent.GetComponent<Collider>());
            vent.AddComponent<BoxCollider>();
            var deposit = vent.AddComponent<AetherDeposit>();   // 500 éther, +1 par cycle
            BuildBeacon(hall, new Vector3(0f, 0.3f, 0f), p, deposit);
        }

        // ---------- Phare ----------

        private static void BuildBeacon(Transform parent, Vector3 position, Palette p, AetherDeposit deposit)
        {
            var coreMat = Mat("M_BeaconCore", new Color(0.3f, 0.75f, 0.9f), 0f, 0.9f, new Color(0.4f, 1.5f, 2f));
            var haloMat = LoadOrCreateMaterial("M_BeaconHalo", "IntoTheDark/Glow");
            haloMat.SetColor("_Color", new Color(0.1f, 0.6f, 0.8f, 0.35f));
            haloMat.SetFloat("_Falloff", 3f);
            EditorUtility.SetDirty(haloMat);

            // Pièces du phare : la lumière qu'elles reçoivent est plafonnée (elles sont collées à la source).
            var beaconMetal = EnvMat("M_BeaconMetal", new Color(0.35f, 0.38f, 0.4f), 0.5f, 0.3f, lightClamp: 0.6f);
            var beaconCage = EnvMat("M_BeaconCage", new Color(0.25f, 0.27f, 0.3f), 0.4f, 0.3f, lightClamp: 0.45f);

            var root = Group("Beacon", parent);
            root.localPosition = position;

            // Fût, collerette, cage autour du foyer, chapeau (mètres, depuis le haut de la tuyère)
            Pipe("Pedestal", root, new Vector3(0f, 0.8f, 0f), 1.6f, 0.5f, Axis.Y, beaconMetal);
            Decor("Collar", root, new Vector3(0f, 1.64f, 0f), new Vector3(0.8f, 0.04f, 0.8f), beaconMetal, PrimitiveType.Cylinder);
            for (int i = 0; i < 4; i++)
            {
                float angle = (45f + 90f * i) * Mathf.Deg2Rad;
                Decor("CageBar", root, new Vector3(Mathf.Cos(angle) * 0.26f, 1.96f, Mathf.Sin(angle) * 0.26f),
                    new Vector3(0.03f, 0.6f, 0.03f), beaconCage);
            }
            Decor("Cap", root, new Vector3(0f, 2.28f, 0f), new Vector3(0.65f, 0.03f, 0.65f), beaconMetal, PrimitiveType.Cylinder);

            // Foyer : matériau URP Lit émissif, non soumis aux ténèbres → visible de partout.
            var core = Decor("Core", root, new Vector3(0f, 1.96f, 0f), Vector3.one * 0.32f, coreMat, PrimitiveType.Sphere);
            core.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            // Zone de visée généreuse autour du foyer pour la touche E
            var aim = core.AddComponent<SphereCollider>();
            aim.isTrigger = true;
            aim.radius = 1.2f;

            var halo = Decor("Halo", root, new Vector3(0f, 1.96f, 0f), Vector3.one * 1.4f, haloMat, PrimitiveType.Quad);
            halo.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            halo.AddComponent<Billboard>();

            var lightGo = new GameObject("BeaconLight");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.96f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            // URP atténue en 1/d² : forte intensité et portée un peu au-delà de la zone
            light.range = BeaconRadius + 5f;
            light.intensity = 180f;
            light.color = new Color(0.4f, 0.85f, 1f);
            light.shadows = LightShadows.Soft;
            light.shadowNearPlane = 0.2f;

            var zone = lightGo.AddComponent<LightZone>();
            zone.Radius = BeaconRadius;
            zone.EdgeColor = new Color(0.3f, 0.85f, 1f);

            // Jauge de réserve sur le fût, côté point de départ : tube d'éther gradué (25/50/75 %).
            var gaugeMat = EnvMat("M_BeaconAether", new Color(0.1f, 0.35f, 0.45f), 0.8f, 0.4f,
                emission: new Color(0.2f, 1f, 1.4f), lightClamp: 0.6f);
            const float gaugeBottom = 0.3f, gaugeHeight = 1f, gaugeZ = -0.29f;
            Decor("GaugePlate", root, new Vector3(0f, gaugeBottom + gaugeHeight / 2f, -0.262f),
                new Vector3(0.1f, gaugeHeight + 0.06f, 0.012f), beaconCage);
            Decor("GaugeCapTop", root, new Vector3(0f, gaugeBottom + gaugeHeight + 0.01f, gaugeZ),
                new Vector3(0.06f, 0.01f, 0.06f), beaconMetal, PrimitiveType.Cylinder);
            Decor("GaugeCapBottom", root, new Vector3(0f, gaugeBottom - 0.01f, gaugeZ),
                new Vector3(0.06f, 0.01f, 0.06f), beaconMetal, PrimitiveType.Cylinder);
            foreach (float mark in new[] { 0.25f, 0.5f, 0.75f })
                Decor("GaugeMark", root, new Vector3(0f, gaugeBottom + gaugeHeight * mark, gaugeZ - 0.005f),
                    new Vector3(0.09f, 0.008f, 0.008f), beaconMetal);
            var gaugePivot = new GameObject("GaugeFluid").transform;
            gaugePivot.SetParent(root, false);
            gaugePivot.localPosition = new Vector3(0f, gaugeBottom, gaugeZ);
            var gaugeFluid = Decor("Fluid", gaugePivot, new Vector3(0f, gaugeHeight / 2f, 0f),
                new Vector3(0.04f, gaugeHeight / 2f, 0.04f), gaugeMat, PrimitiveType.Cylinder);

            var beacon = root.gameObject.AddComponent<Beacon>();
            var serialized = new SerializedObject(beacon);
            serialized.FindProperty("deposit").objectReferenceValue = deposit;
            serialized.FindProperty("gaugeFluid").objectReferenceValue = gaugePivot;
            serialized.FindProperty("gaugeRenderer").objectReferenceValue = gaugeFluid.GetComponent<Renderer>();
            serialized.FindProperty("beaconLight").objectReferenceValue = light;
            serialized.FindProperty("lightZone").objectReferenceValue = zone;
            serialized.FindProperty("halo").objectReferenceValue = halo;
            var glow = serialized.FindProperty("glowRenderers");
            glow.arraySize = 1;
            glow.GetArrayElementAtIndex(0).objectReferenceValue = core.GetComponent<Renderer>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildCorridor(Transform level, Palette p)
        {
            var corridor = Group("Corridor", level);
            float mid = CorridorStart + CorridorLength / 2f;
            float wallLength = CorridorLength - 0.45f;
            float wallMid = CorridorStart + 0.3f + wallLength / 2f;

            Box("Floor", corridor, new Vector3(0f, -0.15f, mid), new Vector3(3f, 0.3f, CorridorLength), p.Floor);
            Box("Ceiling", corridor, new Vector3(0f, 2.55f, mid), new Vector3(3.6f, 0.3f, CorridorLength), p.Ceiling);
            Box("Wall_West", corridor, new Vector3(-1.65f, 1.2f, wallMid), new Vector3(0.3f, 2.4f, wallLength), p.Wall);
            Box("Wall_East", corridor, new Vector3(1.65f, 1.2f, wallMid), new Vector3(0.3f, 2.4f, wallLength), p.Wall);

            Pipe("Pipe_Ceiling", corridor, new Vector3(-0.9f, 2.2f, mid), CorridorLength, 0.2f, Axis.Z, p.Pipe);

            // Bandes de secours rouges au sol (visibles seulement dans la lumière)
            Decor("EmergencyStrip_L", corridor, new Vector3(-1.45f, 0.02f, mid), new Vector3(0.06f, 0.04f, CorridorLength - 1f), p.EmergencyStrip);
            Decor("EmergencyStrip_R", corridor, new Vector3(1.45f, 0.02f, mid), new Vector3(0.06f, 0.04f, CorridorLength - 1f), p.EmergencyStrip);
        }

        private static void BuildSideRoom(Transform level, Palette p)
        {
            var room = Group("SideRoom", level);
            float mid = RoomStart + RoomDepth / 2f;

            Box("Floor", room, new Vector3(0f, -0.15f, mid), new Vector3(8f, 0.3f, RoomDepth), p.Floor);
            Box("Ceiling", room, new Vector3(0f, 2.75f, mid), new Vector3(8f, 0.3f, RoomDepth), p.Ceiling);
            Box("Wall_South_L", room, new Vector3(-2.9f, 1.3f, RoomStart), new Vector3(2.8f, 2.6f, 0.3f), p.Wall);
            Box("Wall_South_R", room, new Vector3(2.9f, 1.3f, RoomStart), new Vector3(2.8f, 2.6f, 0.3f), p.Wall);
            Box("Lintel", room, new Vector3(0f, 2.5f, RoomStart), new Vector3(3f, 0.2f, 0.3f), p.Wall);
            Box("Wall_West", room, new Vector3(-4.15f, 1.3f, mid), new Vector3(0.3f, 2.6f, RoomDepth + 0.6f), p.Wall);
            Box("Wall_East", room, new Vector3(4.15f, 1.3f, mid), new Vector3(0.3f, 2.6f, RoomDepth + 0.6f), p.Wall);
            Box("Wall_North", room, new Vector3(0f, 1.3f, RoomStart + RoomDepth + 0.15f), new Vector3(8.6f, 2.6f, 0.3f), p.Wall);

            // Passage bas pour tester l'accroupi (1,3 m sous la plaque)
            Box("LowDuct", room, new Vector3(2.5f, 1.55f, RoomStart + 5.5f), new Vector3(2.6f, 0.5f, 3f), p.Pipe);

            Box("Debris_1", room, new Vector3(-2.5f, 0.35f, RoomStart + 6.5f), new Vector3(1.4f, 0.7f, 0.9f), p.Debris, yaw: 15f);
        }

        // ---------- Joueur ----------

        private static GameObject BuildPlayer(Vector3 position)
        {
            var player = new GameObject("Player") { tag = "Player" };
            player.transform.position = position;

            var cameraRoot = new GameObject("CameraRoot").transform;
            cameraRoot.SetParent(player.transform, false);
            cameraRoot.localPosition = new Vector3(0f, 1.65f, 0f);

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraGo.transform.SetParent(cameraRoot, false);
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = RenderSettings.fogColor;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 80f;
            camera.fieldOfView = 75f;
            cameraGo.AddComponent<AudioListener>();
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;

            // Les objets tenus en main (lampe) sont sur un calque à part, rendus par une caméra
            // superposée : ils ne traversent jamais les murs.
            int viewModelLayer = ProjectSetup.EnsureLayer(ViewModelLayerName);
            camera.cullingMask = ~(1 << viewModelLayer);

            var overlayGo = new GameObject("ViewModel Camera");
            overlayGo.transform.SetParent(cameraGo.transform, false);
            var overlay = overlayGo.AddComponent<Camera>();
            overlay.cullingMask = 1 << viewModelLayer;
            overlay.nearClipPlane = 0.01f;
            overlay.farClipPlane = 5f;
            overlay.fieldOfView = camera.fieldOfView;
            overlay.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
            cameraData.cameraStack.Add(overlay);

            BuildLantern(cameraGo.transform, viewModelLayer);

            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.35f;
            controller.slopeLimit = 45f;
            controller.skinWidth = 0.035f;

            player.AddComponent<PlayerInputHandler>();
            SetReference(player.AddComponent<PlayerMovement>(), "cameraRoot", cameraRoot);
            SetReference(player.AddComponent<PlayerLook>(), "cameraRoot", cameraRoot);

            player.AddComponent<PlayerLightState>();
            player.AddComponent<PlayerHealth>();

            var interactor = new SerializedObject(player.AddComponent<PlayerInteractor>());
            interactor.FindProperty("cameraRoot").objectReferenceValue = cameraRoot;
            interactor.FindProperty("interactionMask").intValue = ~(1 << viewModelLayer);
            interactor.ApplyModifiedPropertiesWithoutUndo();

            return player;
        }

        // ---------- Lampe tempête ----------

        private const string ViewModelLayerName = "ViewModel";

        private static void BuildLantern(Transform cameraTransform, int layer)
        {
            var paint = Mat("M_LanternPaint", new Color(0.4f, 0.1f, 0.07f), 0.3f, 0.5f);
            var brass = Mat("M_LanternBrass", new Color(0.65f, 0.48f, 0.22f), 0.5f, 0.6f);
            var glass = Mat("M_LanternGlass", new Color(0.9f, 0.72f, 0.45f), 0f, 0.9f, new Color(2.4f, 1.3f, 0.45f));

            var wood = Mat("M_PoleWood", new Color(0.32f, 0.22f, 0.14f), 0f, 0.25f);

            // Ensemble porté sur le dos : bâton qui passe au-dessus de l'épaule gauche,
            // lampe suspendue à son extrémité. C'est ce "LanternRig" qui balancera plus tard.
            var rig = new GameObject("LanternRig").transform;
            rig.SetParent(cameraTransform, false);

            var poleStart = new Vector3(-0.12f, -0.25f, -0.45f);   // dans le dos, sous l'épaule
            var poleTip = new Vector3(-0.42f, 0.5f, 0.8f);       // au-dessus, en haut à gauche de la vue
            Rod("Pole", rig, poleStart, poleTip, 0.03f, wood, layer);
            Part("PoleTipCap", rig, PrimitiveType.Sphere, poleTip, Vector3.one * 0.036f, brass, layer);

            // Crochet + anneau sous l'extrémité du bâton
            var hookBottom = poleTip + new Vector3(0f, -0.07f, 0f);
            Rod("Hook", rig, poleTip, hookBottom, 0.006f, brass, layer);

            var root = new GameObject("StormLantern").transform;
            root.SetParent(rig, false);
            // L'anse culmine à 0,2 m au-dessus de la base de la lampe : elle s'accroche au crochet.
            root.localPosition = hookBottom - new Vector3(0f, 0.2f, 0f);
            root.localRotation = Quaternion.Euler(0f, 20f, 0f);

            // Socle, verre, cage, chapeau, cheminée, anse (mètres)
            Part("Base", root, PrimitiveType.Cylinder, new Vector3(0f, 0.0125f, 0f), new Vector3(0.11f, 0.0125f, 0.11f), paint, layer);
            var glassPart = Part("Glass", root, PrimitiveType.Cylinder, new Vector3(0f, 0.07f, 0f), new Vector3(0.078f, 0.045f, 0.078f), glass, layer);
            for (int i = 0; i < 4; i++)
            {
                float angle = (45f + 90f * i) * Mathf.Deg2Rad;
                Part("CageBar", root, PrimitiveType.Cube,
                    new Vector3(Mathf.Cos(angle) * 0.045f, 0.07f, Mathf.Sin(angle) * 0.045f),
                    new Vector3(0.007f, 0.1f, 0.007f), paint, layer);
            }
            Part("Cap", root, PrimitiveType.Cylinder, new Vector3(0f, 0.125f, 0f), new Vector3(0.1f, 0.01f, 0.1f), paint, layer);
            Part("Chimney", root, PrimitiveType.Cylinder, new Vector3(0f, 0.145f, 0f), new Vector3(0.045f, 0.015f, 0.045f), paint, layer);
            Part("HandlePost_L", root, PrimitiveType.Cube, new Vector3(-0.05f, 0.165f, 0f), new Vector3(0.006f, 0.07f, 0.006f), brass, layer);
            Part("HandlePost_R", root, PrimitiveType.Cube, new Vector3(0.05f, 0.165f, 0f), new Vector3(0.006f, 0.07f, 0.006f), brass, layer);
            Part("HandleTop", root, PrimitiveType.Cube, new Vector3(0f, 0.2f, 0f), new Vector3(0.106f, 0.006f, 0.006f), brass, layer);

            // Halo : lumière ponctuelle au cœur de la lampe (URP atténue en 1/d²).
            var lightGo = new GameObject("LanternLight");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.07f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 7f;        // un peu plus que la zone, pour bien éclairer jusqu'au bord
            light.intensity = 18f;
            light.color = new Color(1f, 0.72f, 0.42f);
            light.shadows = LightShadows.Soft;
            light.shadowNearPlane = 0.1f;

            var zone = lightGo.AddComponent<LightZone>();
            zone.Radius = 5f;
            zone.EdgeColor = new Color(1f, 0.72f, 0.4f);

            // Jauge diégétique : tube d'éther sur le flanc tourné vers le joueur.
            var aether = Mat("M_LanternAether", new Color(0.2f, 0.8f, 1f), 0f, 0.9f, new Color(0.3f, 1.6f, 2.2f));
            Vector3 toCamera = Quaternion.Inverse(root.localRotation) * -root.localPosition;
            toCamera.y = 0f;
            toCamera.Normalize();
            var facing = Quaternion.LookRotation(toCamera, Vector3.up);
            const float gaugeY = 0.07f, gaugeHeight = 0.08f;

            var plate = Part("GaugePlate", root, PrimitiveType.Cube, toCamera * 0.055f + Vector3.up * gaugeY,
                new Vector3(0.022f, gaugeHeight + 0.01f, 0.004f), paint, layer);
            plate.transform.localRotation = facing;
            Part("GaugeCapTop", root, PrimitiveType.Cylinder, toCamera * 0.062f + Vector3.up * (gaugeY + gaugeHeight * 0.5f + 0.002f),
                new Vector3(0.016f, 0.003f, 0.016f), brass, layer);
            Part("GaugeCapBottom", root, PrimitiveType.Cylinder, toCamera * 0.062f + Vector3.up * (gaugeY - gaugeHeight * 0.5f - 0.002f),
                new Vector3(0.016f, 0.003f, 0.016f), brass, layer);

            var fluidPivot = new GameObject("GaugeFluid").transform;   // pivot en bas, étiré selon la charge
            fluidPivot.SetParent(root, false);
            fluidPivot.gameObject.layer = layer;
            fluidPivot.localPosition = toCamera * 0.062f + Vector3.up * (gaugeY - gaugeHeight * 0.5f);
            var fluid = Part("Fluid", fluidPivot, PrimitiveType.Cylinder, new Vector3(0f, gaugeHeight * 0.5f, 0f),
                new Vector3(0.01f, gaugeHeight * 0.5f, 0.01f), aether, layer);

            var lantern = root.gameObject.AddComponent<StormLantern>();
            var serialized = new SerializedObject(lantern);
            serialized.FindProperty("lanternLight").objectReferenceValue = light;
            serialized.FindProperty("lightZone").objectReferenceValue = zone;
            serialized.FindProperty("gaugeFluid").objectReferenceValue = fluidPivot;
            serialized.FindProperty("gaugeRenderer").objectReferenceValue = fluid.GetComponent<Renderer>();
            var glow = serialized.FindProperty("glowRenderers");
            glow.arraySize = 1;
            glow.GetArrayElementAtIndex(0).objectReferenceValue = glassPart.GetComponent<Renderer>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Pièce de modèle tenu en main : sans collision ni ombre, sur le calque ViewModel.</summary>
        private static GameObject Part(string name, Transform parent, PrimitiveType shape, Vector3 position, Vector3 scale,
            Material mat, int layer)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = name;
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        /// <summary>Cylindre fin tendu entre deux points (bâton, crochet).</summary>
        private static GameObject Rod(string name, Transform parent, Vector3 from, Vector3 to, float diameter,
            Material mat, int layer)
        {
            Vector3 direction = to - from;
            var go = Part(name, parent, PrimitiveType.Cylinder, (from + to) * 0.5f,
                new Vector3(diameter, direction.magnitude * 0.5f, diameter), mat, layer);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
            return go;
        }

        // ---------- Cycle ----------

        private static void BuildCycle()
        {
            var go = new GameObject("Cycle");
            go.AddComponent<CycleManager>();          // mode test coché par défaut (cycle d'environ 2 min)
            go.AddComponent<AudioSource>();
            go.AddComponent<AmbientRumble>();
        }

        // ---------- Post-traitement ----------

        private static void BuildPostProcessing()
        {
            const string path = ProjectSetup.SettingsFolder + "/IntoTheDark_PostProcess.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                ProjectSetup.EnsureFolder(ProjectSetup.SettingsFolder);
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
                AddOverride<Bloom>(profile, b =>
                {
                    b.intensity.Override(0.7f);
                    b.threshold.Override(1.1f);
                    b.scatter.Override(0.6f);
                });
                AddOverride<Vignette>(profile, v =>
                {
                    v.intensity.Override(0.4f);
                    v.smoothness.Override(0.5f);
                });
            }

            // Compression des hautes lumières : les zones très éclairées gardent leurs détails.
            if (!profile.Has<Tonemapping>())
                AddOverride<Tonemapping>(profile, t => t.mode.Override(TonemappingMode.Neutral));
            AssetDatabase.SaveAssets();

            var volume = new GameObject("PostProcess").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        private static void AddOverride<T>(VolumeProfile profile, Action<T> setup) where T : VolumeComponent
        {
            var component = profile.Add<T>(true);
            setup(component);
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
        }

        // ---------- Aides ----------

        private enum Axis { X, Y, Z }

        private static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material mat, float yaw = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        private static GameObject Pipe(string name, Transform parent, Vector3 center, float length, float diameter, Axis axis, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = axis switch
            {
                Axis.X => Quaternion.Euler(0f, 0f, 90f),
                Axis.Z => Quaternion.Euler(90f, 0f, 0f),
                _ => Quaternion.identity,
            };
            // Le cylindre Unity mesure 2 m de haut et 1 m de diamètre.
            go.transform.localScale = new Vector3(diameter, length * 0.5f, diameter);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        /// <summary>Élément visuel sans collision.</summary>
        private static GameObject Decor(string name, Transform parent, Vector3 center, Vector3 size, Material mat,
            PrimitiveType shape = PrimitiveType.Cube)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null) return;
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path))
                return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
