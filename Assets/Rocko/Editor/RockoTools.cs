using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Rocko.Editor
{
    public static class RockoTools
    {
        //
        // @description - Open the supplied sandbox scene without overwriting user changes
        // @return {Void} - Selects Rocko's launch scene after Unity's normal save prompt
        //
        [MenuItem("Rocko/Open Prototype Scene")]
        public static void OpenScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene("Assets/Rocko/Scenes/Rocko.unity");
        }

        //
        // @description - Create an editable baseline specification and assign it to the open bootstrap
        // @return {Void} - Saves a unique asset; never overwrites an existing tune
        //
        [MenuItem("Rocko/Create Editable Baseline Tune")]
        public static void CreateTune()
        {
            VehicleSpec spec = VehicleSpec.CreateBaseline();
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Rocko/RockoBaseline.asset");
            AssetDatabase.CreateAsset(spec, path); AssetDatabase.SaveAssets();
            RockoBootstrap bootstrap = Object.FindFirstObjectByType<RockoBootstrap>();
            if (bootstrap != null)
            {
                Undo.RecordObject(bootstrap, "Assign Rocko tune"); bootstrap.specification = spec;
                EditorUtility.SetDirty(bootstrap); EditorSceneManager.MarkSceneDirty(bootstrap.gameObject.scene);
            }
            Selection.activeObject = spec;
        }

        //
        // @description - Build the included scene as a Windows x64 development player
        // @return {Void} - Writes the build and raises an error if Unity reports failure
        //
        [MenuItem("Rocko/Build Windows Prototype")]
        public static void BuildWindows()
        {
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Rocko/Scenes/Rocko.unity" },
                locationPathName = "Builds/Windows/Rocko.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };
            UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception("Rocko build failed: " + report.summary.result);
        }
    }
}
