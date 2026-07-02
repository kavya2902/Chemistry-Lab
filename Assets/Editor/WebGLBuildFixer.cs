using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEngine.XR.Management;

// Runs before OpenXR's pre-build step (callbackOrder 0) to clear XR loaders for WebGL.
// OpenXR has no WebGL loader library, so it throws BuildFailedException if active.
public class WebGLBuildFixer : IPreprocessBuildWithReport
{
    public int callbackOrder => -1;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platformGroup != BuildTargetGroup.WebGL) return;

        var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.WebGL);
        if (settings == null || settings.Manager == null) return;

        settings.Manager.loaders.Clear();
    }
}
