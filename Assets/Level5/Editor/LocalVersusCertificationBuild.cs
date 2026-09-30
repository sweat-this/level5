using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Editor-only entry point that builds the standalone Windows player used for Local Versus
/// process-level certification (docs/versus-architecture.md, "Local Versus certification").
///
/// The one thing it does differently from an ordinary player build is give the player its own
/// product name. <c>Application.persistentDataPath</c> is derived from company/product, so the
/// certification player - and the <c>versus</c> folder <see cref="FileVersusSeriesRepository"/>
/// writes under it - lives in its own directory and can never read or overwrite a developer's
/// ordinary Local Versus saves. The product name is restored before the method returns, so no
/// <c>ProjectSettings</c> change is left behind.
///
/// Nothing here changes what ships: same scenes, same scripting backend, same code. It contains no
/// certification logic; the player is driven from outside by real input.
///
/// Run from the repository root (no <c>-quit</c>: this method exits the editor itself):
///   "&lt;UnityPath&gt;\Unity.exe" -batchmode -nographics -projectPath . -executeMethod
///     LocalVersusCertificationBuild.BuildWindows64 -logFile build.log
/// Output folder: <c>LEVEL5_LV_CERT_PLAYER_DIR</c>, default <c>TestResults/lv-cert/player</c>.
/// </summary>
public static class LocalVersusCertificationBuild
{
    public const string CertificationProductName = "level5-local-versus-cert";
    public const string OutputDirectoryEnvironmentVariable = "LEVEL5_LV_CERT_PLAYER_DIR";

    public static void BuildWindows64()
    {
        int exitCode = 1;
        string originalProductName = PlayerSettings.productName;
        try
        {
            string directory = Environment.GetEnvironmentVariable(OutputDirectoryEnvironmentVariable);
            if (string.IsNullOrEmpty(directory))
            {
                directory = Path.Combine("TestResults", "lv-cert", "player");
            }

            Directory.CreateDirectory(directory);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                locationPathName = Path.Combine(directory, CertificationProductName + ".exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            PlayerSettings.productName = CertificationProductName;
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log(
                $"LocalVersusCertificationBuild: {report.summary.result}, {report.summary.totalErrors} error(s), "
                + $"{report.summary.totalTime}, {options.locationPathName}");
            exitCode = report.summary.result == BuildResult.Succeeded ? 0 : 1;
        }
        catch (Exception exception)
        {
            Debug.LogError("LocalVersusCertificationBuild failed: " + exception);
        }
        finally
        {
            PlayerSettings.productName = originalProductName;
        }

        EditorApplication.Exit(exitCode);
    }
}
