using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.XR.CoreUtils.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace MissionLens.EditorTools
{
    /// <summary>
    /// Project configuration for Quest 3 through Unity OpenXR: Meta, written as code so it can be
    /// re-applied and reviewed. Each method returns a short report of what it found and changed.
    /// </summary>
    public static class MLSetup
    {
        const BuildTargetGroup G = BuildTargetGroup.Android;
        const string MetaFeatureSet = "com.unity.openxr.featureset.meta";

        // OpenXR features this app uses. Everything else in the Meta Quest group is switched off:
        // planes, meshes, anchors and occlusion need the scene permission and nothing here reads them.
        static readonly string[] FeaturesOn =
        {
            "com.unity.openxr.feature.metaquest",
            "com.unity.openxr.feature.input.oculustouch",
            "com.unity.openxr.feature.input.metaquestplus",
            "com.unity.openxr.feature.input.handinteraction",
            "com.unity.openxr.feature.arfoundation-meta-session",
            "com.unity.openxr.feature.arfoundation-meta-camera",
            "com.unity.openxr.feature.compositionlayers",
            "com.unity.openxr.feature.input.handtrackingsubsystem",
            "com.unity.openxr.feature.input.metahandtrackingaim",
        };

        public static string ConfigureAll() => ConfigureXR() + "\n" + ConfigurePlayer() + "\n" + ConfigureGraphics();

        public static string ConfigureXR()
        {
            var log = new StringBuilder("XR\n");
            XRGeneralSettingsPerBuildTarget per;
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out per) || per == null)
            {
                var found = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget");
                if (found.Length > 0) per = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(AssetDatabase.GUIDToAssetPath(found[0]));
                if (per == null)
                {
                    per = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                    AssetDatabase.CreateAsset(per, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                    log.AppendLine("  created Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                }
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, per, true);
            }
            if (!per.HasSettingsForBuildTarget(G)) per.CreateDefaultSettingsForBuildTarget(G);
            if (!per.HasManagerSettingsForBuildTarget(G)) per.CreateDefaultManagerSettingsForBuildTarget(G);
            var general = per.SettingsForBuildTarget(G);
            var manager = per.ManagerSettingsForBuildTarget(G);
            if (!manager.activeLoaders.Any(l => l is OpenXRLoader))
                log.AppendLine("  assign OpenXR loader: " + XRPackageMetadataStore.AssignLoader(manager, typeof(OpenXRLoader).FullName, G));
            general.InitManagerOnStart = true;

            var set = OpenXRFeatureSetManager.GetFeatureSetWithId(G, MetaFeatureSet);
            if (set != null) { set.isEnabled = true; OpenXRFeatureSetManager.SetFeaturesFromEnabledFeatureSets(G); }
            else log.AppendLine("  WARNING: feature set not found: " + MetaFeatureSet);

            var oxr = OpenXRSettings.GetSettingsForBuildTargetGroup(G);
            oxr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            var seen = new HashSet<string>();
            foreach (var f in oxr.GetFeatures())
            {
                var attr = (OpenXRFeatureAttribute)Attribute.GetCustomAttribute(f.GetType(), typeof(OpenXRFeatureAttribute));
                if (attr == null) continue;
                bool want = FeaturesOn.Contains(attr.FeatureId);
                if (want) seen.Add(attr.FeatureId);
                if (f.enabled != want) { f.enabled = want; EditorUtility.SetDirty(f); }
                if (want) log.AppendLine("  on: " + attr.UiName + " (" + attr.FeatureId + ")");
            }
            foreach (var id in FeaturesOn.Where(id => !seen.Contains(id))) log.AppendLine("  WARNING: feature not present: " + id);

            EditorUtility.SetDirty(per); EditorUtility.SetDirty(general); EditorUtility.SetDirty(manager); EditorUtility.SetDirty(oxr);
            AssetDatabase.SaveAssets();
            log.AppendLine("  loaders: " + string.Join(", ", manager.activeLoaders.Select(l => l.GetType().Name)) + " · init on start: " + general.InitManagerOnStart + " · render mode: " + oxr.renderMode);
            return log.ToString();
        }

        public static string ConfigurePlayer()
        {
            var android = NamedBuildTarget.Android;
            // Builds log one NullReferenceException from an OpenXR validation rule (MetaQuestFeature.cs:539). It looks
            // for the Meta Quest feature under the platform *selected* in the build settings window, which is
            // Standalone here. It is only a log line; the build is unaffected. Do not "fix" it by selecting Android:
            // a package then rewrites the Android scripting defines on every entry to Play mode, and the recompile
            // that follows wipes the running app's state.
            PlayerSettings.companyName = "Mission Lens";
            PlayerSettings.productName = "Mission Lens";
            PlayerSettings.SetApplicationIdentifier(android, "com.missionlens.eva01");
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            AssetDatabase.SaveAssets();
            return "Player\n  id " + PlayerSettings.GetApplicationIdentifier(android) + " · " + PlayerSettings.GetScriptingBackend(android) + " · " + PlayerSettings.Android.targetArchitectures
                   + " · min API " + (int)PlayerSettings.Android.minSdkVersion + " · " + string.Join(",", PlayerSettings.GetGraphicsAPIs(BuildTarget.Android)) + " · " + PlayerSettings.colorSpace + "\n";
        }

        /// <summary>URP settings Unity recommends for passthrough on Quest: no HDR, no post-processing, intermediate texture only when needed.</summary>
        public static string ConfigureGraphics()
        {
            var log = new StringBuilder("Graphics\n");
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("Mobile")) continue;
                var so = new SerializedObject(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path));
                Set(so, "m_SupportsHDR", false); Set(so, "m_SupportsTerrainHoles", false); SetInt(so, "m_MSAA", 4);
                so.ApplyModifiedPropertiesWithoutUndo();
                log.AppendLine("  " + path + ": HDR off, terrain holes off, MSAA 4x");
            }
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("Mobile")) continue;
                var so = new SerializedObject(AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path));
                var pp = so.FindProperty("postProcessData"); if (pp != null) pp.objectReferenceValue = null;
                SetInt(so, "m_IntermediateTextureMode", 0);
                so.ApplyModifiedPropertiesWithoutUndo();
                log.AppendLine("  " + path + ": post-processing off, intermediate texture auto");
            }
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        static void Set(SerializedObject so, string name, bool v) { var p = so.FindProperty(name); if (p != null) p.boolValue = v; else Debug.LogWarning("MLSetup: no property " + name); }
        static void SetInt(SerializedObject so, string name, int v) { var p = so.FindProperty(name); if (p != null) p.intValue = v; else Debug.LogWarning("MLSetup: no property " + name); }

        /// <summary>What the XR packages' own project validation still objects to. Pass fix=true to apply the automatic fixes.</summary>
        public static string Validate(bool fix = false)
        {
            // The validator's own listing stops at the first rule that throws, so walk the rules one by one.
            var field = typeof(BuildValidator).GetField("s_PlatformRules", BindingFlags.Static | BindingFlags.NonPublic);
            var all = field?.GetValue(null) as System.Collections.IDictionary;
            if (all == null || !all.Contains(G)) return "no validation rules registered for " + G;
            var open = new List<BuildValidationRule>();
            var log = new StringBuilder();
            foreach (BuildValidationRule r in (System.Collections.IEnumerable)all[G])
            {
                try { if (r.IsRuleEnabled.Invoke() && (r.CheckPredicate == null || !r.CheckPredicate.Invoke())) open.Add(r); }
                catch (Exception e) { log.AppendLine("  could not evaluate: " + r.Category + ": " + r.Message + " — " + e.GetType().Name); }
            }
            foreach (var r in open) log.AppendLine("  " + (r.Error ? "ERROR " : "warn  ") + (r.FixItAutomatic ? "[auto] " : "[manual] ") + r.Category + ": " + r.Message);
            if (fix)
            {
                var auto = open.Where(r => r.FixIt != null && r.FixItAutomatic).ToList();
                foreach (var r in auto) { try { r.FixIt(); } catch (Exception e) { log.AppendLine("  fix failed: " + r.Message + " — " + e.Message); } }
                AssetDatabase.SaveAssets();
                log.AppendLine("  applied " + auto.Count + " automatic fixes");
            }
            return "Validation (" + open.Count + " open)\n" + log;
        }

        /// <summary>Removes the empty folders a first import can leave behind ("XR 1", "Settings 1").</summary>
        public static string CleanStrayFolders()
        {
            var log = new StringBuilder();
            foreach (var p in new[] { "Assets/XR 1", "Assets/XR/Settings 1" })
                if (AssetDatabase.IsValidFolder(p) && Directory.GetFiles(p, "*", SearchOption.AllDirectories).All(f => f.EndsWith(".meta")))
                    log.AppendLine((AssetDatabase.DeleteAsset(p) ? "removed " : "could not remove ") + p);
            return log.Length == 0 ? "nothing to clean" : log.ToString();
        }
    }
}
