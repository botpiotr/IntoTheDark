using System;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace IntoTheDark.EditorTools
{
    /// <summary>
    /// Menu "Into the Dark > 1. Configurer le rendu URP" : crée l'asset URP du projet et l'active
    /// dans Graphics et dans tous les niveaux de Quality.
    /// </summary>
    public static class ProjectSetup
    {
        private const string UrpPackagePath = "Packages/com.unity.render-pipelines.universal";
        private const string PostProcessDataPath = UrpPackagePath + "/Runtime/Data/PostProcessData.asset";

        public const string SettingsFolder = "Assets/IntoTheDark/Settings";
        private const string PipelinePath = SettingsFolder + "/IntoTheDark_URP.asset";
        private const string RendererPath = SettingsFolder + "/IntoTheDark_Renderer.asset";

        public static bool IsUrpActive => GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset;

        [MenuItem("Into the Dark/1. Configurer le rendu URP", priority = 1)]
        public static void ConfigureUrp()
        {
            try
            {
                var pipeline = LoadOrCreatePipeline();

                GraphicsSettings.defaultRenderPipeline = pipeline;

                int previousLevel = QualitySettings.GetQualityLevel();
                for (int i = 0; i < QualitySettings.names.Length; i++)
                {
                    QualitySettings.SetQualityLevel(i, false);
                    QualitySettings.renderPipeline = pipeline;
                }
                QualitySettings.SetQualityLevel(previousLevel, false);

                AssetDatabase.SaveAssets();
                Debug.Log("[Into the Dark] URP configuré : " + PipelinePath);
            }
            catch (Exception e)
            {
                Debug.LogError("[Into the Dark] Échec de la configuration URP : " + e +
                               "\nSolution manuelle : Assets > Create > Rendering > URP Asset (with Universal Renderer), " +
                               "puis le glisser dans Edit > Project Settings > Graphics > Scriptable Render Pipeline Settings.");
            }
        }

        private static UniversalRenderPipelineAsset LoadOrCreatePipeline()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline != null)
                return pipeline;

            EnsureFolder(SettingsFolder);

            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            if (rendererData.shaders == null)
                rendererData.shaders = new UniversalRendererData.ShaderResources();
            ResourceReloader.ReloadAllNullIn(rendererData, UrpPackagePath);

            rendererData.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(PostProcessDataPath);
            if (rendererData.postProcessData == null)
                Debug.LogWarning("[Into the Dark] PostProcessData introuvable : le post-traitement sera désactivé.");

            AssetDatabase.CreateAsset(rendererData, RendererPath);

            pipeline = UniversalRenderPipelineAsset.Create(rendererData);
            pipeline.shadowDistance = 30f;
            AssetDatabase.CreateAsset(pipeline, PipelinePath);

            // Ombres des lumières additionnelles (phares, lampes) : essentielles pour l'ambiance.
            var serialized = new SerializedObject(pipeline);
            var additionalShadows = serialized.FindProperty("m_AdditionalLightShadowsSupported");
            if (additionalShadows != null)
            {
                additionalShadows.boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(pipeline);
            return pipeline;
        }

        /// <summary>Renvoie l'index du calque, en le créant dans le premier emplacement libre si besoin.</summary>
        public static int EnsureLayer(string layerName)
        {
            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0)
                return existing;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                var layer = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(layer.stringValue))
                    continue;
                layer.stringValue = layerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }
            throw new InvalidOperationException("Aucun calque libre pour " + layerName);
        }

        public const string ScenesFolder = "Assets/IntoTheDark/Scenes";
        public const string MainMenuScenePath = ScenesFolder + "/MainMenu.unity";
        public const string GameScenePath = ScenesFolder + "/TestRoom.unity";

        /// <summary>
        /// Avant de régénérer une scène : les scènes générées (salle de test, menu) sont remplacées
        /// sans question ; on ne demande que pour les autres scènes modifiées.
        /// </summary>
        public static bool ConfirmReplaceOpenScenes()
        {
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (scene.isDirty && scene.path != GameScenePath && scene.path != MainMenuScenePath)
                    return UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            }
            return true;
        }

        /// <summary>Build Settings : menu principal en premier, puis la scène de jeu (celles qui existent).</summary>
        public static void RegisterScenes()
        {
            var ordered = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            foreach (var path in new[] { MainMenuScenePath, GameScenePath })
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                    ordered.Add(new EditorBuildSettingsScene(path, true));
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.path != MainMenuScenePath && scene.path != GameScenePath)
                    ordered.Add(scene);
            EditorBuildSettings.scenes = ordered.ToArray();
        }

        public static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
                return;

            string parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetFolder));
        }
    }
}
