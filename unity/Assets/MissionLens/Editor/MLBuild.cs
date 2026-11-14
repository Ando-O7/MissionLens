using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MissionLens.EditorTools
{
    /// <summary>The scene and the Quest build, as code. The app builds everything else itself at run time.</summary>
    public static class MLBuild
    {
        public const string ScenePath = "Assets/MissionLens/Scenes/Main.unity";
        const string FontOverlayPath = "Assets/MissionLens/Resources/MLMat/FontOverlay.mat";

        /// <summary>Text resources the UI needs: TextMeshPro's default font, and a copy of its material that draws over everything.</summary>
        public static string PrepareText()
        {
            if (!File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
            {
                TMP_PackageResourceImporter.ImportResources(true, false, false);
                return "imported TextMeshPro essentials; run PrepareText again once the import has finished";
            }
            var font = TMP_Settings.defaultFontAsset;
            if (font == null) return "TextMeshPro default font not available yet";
            var overlay = Shader.Find("TextMeshPro/Distance Field Overlay");
            if (overlay == null) return "TextMeshPro overlay shader not found";
            Directory.CreateDirectory(Path.GetDirectoryName(FontOverlayPath));
            var mat = AssetDatabase.LoadAssetAtPath<Material>(FontOverlayPath);
            if (mat == null) { mat = new Material(font.material) { name = "FontOverlay" }; AssetDatabase.CreateAsset(mat, FontOverlayPath); }
            mat.shader = overlay; EditorUtility.SetDirty(mat); AssetDatabase.SaveAssets();
            return "font " + font.name + ", overlay material at " + FontOverlayPath;
        }

        /// <summary>A scene that holds nothing but the app object.</summary>
        public static string CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Mission Lens").AddComponent<MLApp>();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            return "scene saved: " + ScenePath;
        }

        /// <summary>Builds the Quest APK to Builds/MissionLens.apk (outside Assets, not in git).</summary>
        public static string BuildApk()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) EditorSceneManager.SaveOpenScenes();
            Directory.CreateDirectory("Builds");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = "Builds/MissionLens.apk", target = BuildTarget.Android, options = BuildOptions.None });
            var s = report.summary;
            var errors = report.steps.SelectMany(st => st.messages).Where(m => m.type == LogType.Error || m.type == LogType.Exception).Select(m => m.content).Take(8);
            return $"{s.result} · {s.totalSize / 1e6:0.0} MB · {s.totalTime.TotalSeconds:0} s · {s.outputPath}\n" + string.Join("\n", errors);
        }
    }
}
