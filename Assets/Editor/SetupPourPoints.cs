using UnityEngine;
using UnityEditor;

public class SetupPourPoints
{
    public static void Execute()
    {
        // ── 1. Create PourPoint on beaker (mouth/rim) ─────────────────────
        var beaker = GameObject.Find("Titration/beaker");
        if (beaker == null) { Debug.LogError("Beaker not found!"); return; }

        // Beaker bounds: center (-2.199, 1.048, 6.240), max Y = 1.090
        // The beaker model has localRotation (270,0,0) and scale (0.01,0.01,0.01)
        // The "mouth" is at the top of the beaker — highest Y point
        // Beaker position: (-2.202, 1.005, 6.700) with bounds max Y at 1.090
        Transform pourPoint = beaker.transform.Find("PourPoint");
        if (pourPoint == null)
        {
            var pourPointObj = new GameObject("PourPoint");
            Undo.RegisterCreatedObjectUndo(pourPointObj, "Create PourPoint");
            pourPointObj.transform.SetParent(beaker.transform, false);
            // Position at the rim of the beaker (top, in local space)
            // Beaker is scaled 0.01, so local units are 100x world units
            // The beaker mesh top is approximately at local Y offset that maps to world Y ~1.09
            // From beaker world pos (y=1.005) to top (y=1.09) = 0.085m = 8.5 local units at 0.01 scale
            pourPointObj.transform.localPosition = new Vector3(0f, 0f, 8.5f);
            pourPoint = pourPointObj.transform;
            Debug.Log($"[Setup] Created PourPoint at local (0,0,8.5) → world {pourPoint.position}");
        }

        // ── 2. Create ReceivePoint on flask (opening) ─────────────────────
        var flask = GameObject.Find("Titration/ConicalFlask");
        if (flask == null) { Debug.LogError("ConicalFlask not found!"); return; }

        // Flask bounds: center (-2.312, 1.051, 6.378), max Y = 1.096
        // Flask position: (-2.498, 1.055, 7.020) with localScale (1,1,1)
        // The flask opening is at the top — the narrow neck
        Transform receivePoint = flask.transform.Find("ReceivePoint");
        if (receivePoint == null)
        {
            var receivePointObj = new GameObject("ReceivePoint");
            Undo.RegisterCreatedObjectUndo(receivePointObj, "Create ReceivePoint");
            receivePointObj.transform.SetParent(flask.transform, false);
            // Flask top is at approximately world Y = 1.096
            // Flask local pos Y = 1.055, so offset = 0.041
            receivePointObj.transform.localPosition = new Vector3(0f, 0.045f, 0f);
            receivePoint = receivePointObj.transform;
            Debug.Log($"[Setup] Created ReceivePoint at local (0,0.045,0) → world {receivePoint.position}");
        }

        // ── 3. Wire references on StarchPourDetector ──────────────────────
        var pourDetector = beaker.GetComponent<StarchPourDetector>();
        if (pourDetector != null)
        {
            pourDetector.pourPoint = pourPoint;
            pourDetector.receivePoint = receivePoint;
            pourDetector.tiltThreshold = 40f;
            pourDetector.maxPourDistance = 0.2f;
            pourDetector.pourHoldDuration = 0.4f;
            pourDetector.enableDebugLogs = true;
            EditorUtility.SetDirty(pourDetector);
            Debug.Log("[Setup] StarchPourDetector: pourPoint + receivePoint wired, tilt=40, dist=0.2, hold=0.4");
        }

        // ── 4. Wire references on BeakerLiquid ────────────────────────────
        var beakerLiquid = beaker.GetComponent<BeakerLiquid>();
        if (beakerLiquid != null)
        {
            beakerLiquid.pourPoint = pourPoint;
            beakerLiquid.receivePoint = receivePoint;
            beakerLiquid.pourTiltThreshold = 40f;
            beakerLiquid.maxPourDistance = 0.2f;
            beakerLiquid.requireGrab = true;
            EditorUtility.SetDirty(beakerLiquid);
            Debug.Log("[Setup] BeakerLiquid: pourPoint + receivePoint wired, tilt=40, dist=0.2");
        }

        // ── 5. Log current world positions for verification ───────────────
        Debug.Log($"[Setup] Beaker world pos: {beaker.transform.position}");
        Debug.Log($"[Setup] Beaker transform.up: {beaker.transform.up}");
        Debug.Log($"[Setup] PourPoint world pos: {pourPoint.position}");
        Debug.Log($"[Setup] Flask world pos: {flask.transform.position}");
        Debug.Log($"[Setup] ReceivePoint world pos: {receivePoint.position}");
        Debug.Log($"[Setup] Current distance (pourPoint→receivePoint): {Vector3.Distance(pourPoint.position, receivePoint.position):F3}m");
        Debug.Log($"[Setup] Current distance (beaker→flask centers): {Vector3.Distance(beaker.transform.position, flask.transform.position):F3}m");

        // Save
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

        Debug.Log("=== PourPoint setup complete ===");
    }
}
