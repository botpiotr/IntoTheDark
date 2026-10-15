using IntoTheDark.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace IntoTheDark.EditorTools
{
    /// <summary>
    /// Menu "Into the Dark > 3. Construire le menu principal" : génère la scène du menu principal
    /// (l'écran lui-même est construit par MainMenuController au lancement).
    /// </summary>
    public static class MainMenuBuilder
    {
        [MenuItem("Into the Dark/3. Construire le menu principal", priority = 3)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.skybox = null;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.fog = false;

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;

            new GameObject("MainMenu").AddComponent<MainMenuController>();

            ProjectSetup.EnsureFolder(ProjectSetup.ScenesFolder);
            EditorSceneManager.SaveScene(scene, ProjectSetup.MainMenuScenePath);
            ProjectSetup.RegisterScenes();
            Debug.Log("[Into the Dark] Menu principal généré : " + ProjectSetup.MainMenuScenePath);
        }
    }
}
