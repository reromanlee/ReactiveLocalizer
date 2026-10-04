using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace reromanlee.ReactiveLocalizer.ContinuousIntegration
{
    /// <summary>
    /// Builds the Quick Start sample for the active build target. Continuous integration copies the package samples
    /// into Assets/Samples, then GameCI's builder calls <see cref="Build"/> with -executeMethod.
    /// </summary>
    public static class PlayerBuild
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/Samples/QuickStart/QuickStart.unity"
        };

        /// <summary>Builds to the path in GameCI's -customBuildPath argument; throwing makes Unity exit with code 1.</summary>
        public static void Build()
        {
            foreach (string scenePath in ScenePaths)
            {
                if (!File.Exists(scenePath))
                {
                    throw new BuildFailedException(
                        $"{scenePath} is missing; copy the package samples into Assets/Samples.");
                }
            }
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            if (target == BuildTarget.Android)
            {
                // What app stores require, on one architecture to keep the build short.
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            }

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = ScenePaths,
                target = target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                locationPathName = ValueOf("-customBuildPath")
            });
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"The {target} build ended as {summary.result} with {summary.totalErrors} errors.");
            }
        }

        private static string ValueOf(string argument)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, argument);
            if (index < 0 || index == arguments.Length - 1)
            {
                throw new ArgumentException($"The command line has no {argument} value.");
            }
            return arguments[index + 1];
        }
    }
}
