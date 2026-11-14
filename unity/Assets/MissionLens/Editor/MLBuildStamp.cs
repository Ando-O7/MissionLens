using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace MissionLens.EditorTools
{
    /// <summary>
    /// Writes when (and from which commit) a build was made into Resources/ML/build.txt, just before the build.
    /// The app shows it on its title, so that someone in the headset can tell which build they are looking at.
    /// </summary>
    public class MLBuildStamp : IPreprocessBuildWithReport
    {
        public const string Path = "Assets/MissionLens/Resources/ML/build.txt";
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) => Write();

        public static string Write()
        {
            string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), commit = Commit();
            if (commit.Length > 0) stamp += " · " + commit;
            File.WriteAllText(Path, stamp);
            AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceSynchronousImport);
            return stamp;
        }

        // Short hash of the commit the working tree is on, with a + if there are uncommitted changes. Empty if git is not available.
        static string Commit()
        {
            try
            {
                string hash = Git("rev-parse --short HEAD"); if (hash.Length == 0) return "";
                return hash + (Git("status --porcelain -- unity/Assets tools").Length > 0 ? "+" : "");
            }
            catch (Exception) { return ""; }
        }
        static string Git(string args)
        {
            var p = Process.Start(new ProcessStartInfo("git", args) { WorkingDirectory = "..", RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true });
            string o = p.StandardOutput.ReadToEnd().Trim(); p.WaitForExit(4000); return p.ExitCode == 0 ? o : "";
        }
    }
}
