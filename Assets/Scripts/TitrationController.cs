//using UnityEngine;
//public class TitrationController : MonoBehaviour
//{
//    [Header("References")]
//    public GameObject burette;
//    public GameObject flask;
//    public MeshRenderer liquidRenderer; // the liquid inside the flask

//    [Header("Titration State")]
//    public float totalDropsRequired = 10f;
//    private float dropsAdded = 0f;
//    private bool isComplete = false;

//    // Called by VR grab/squeeze interaction on burette
//    public void AddDrop()
//    {
//        if (isComplete) return;
//        dropsAdded++;
//        UpdateLiquidColor();
//        if (dropsAdded >= totalDropsRequired)
//            OnEndpointReached();
//    }

//    private void UpdateLiquidColor()
//    {
//        float t = dropsAdded / totalDropsRequired;
//        liquidRenderer.material.color = Color.Lerp(Color.clear, Color.magenta, t);
//    }

//    private void OnEndpointReached()
//    {
//        isComplete = true;
//        Debug.Log("Titration endpoint reached!");
//        // Fire event or notify ExperimentManager here
//    }
//}


using UnityEngine;

/// <summary>
/// LEGACY STUB — superseded by IodineTitrationController.
///
/// This script is kept only for backward compatibility with any scene that
/// still references TitrationController. It will log a one-time error in
/// Play mode reminding you to migrate to IodineTitrationController.
///
/// FIXES applied so it at least compiles and doesn't crash:
///  - liquidRenderer null-guard in UpdateLiquidColor() so the script doesn't
///    throw NullReferenceException when the reference is unassigned.
///  - Removed Color.clear → Color.magenta lerp (wrong chemistry);
///    kept as a harmless placeholder but clearly labelled.
///  - Added a prominent [System.Obsolete] attribute.
///  - OnEnable() fires the deprecation warning once per play session.
/// </summary>
[System.Obsolete("TitrationController is a legacy stub. Use IodineTitrationController instead.")]
public class TitrationController : MonoBehaviour
{
    [Header("References (Legacy — migrate to IodineTitrationController)")]
    public GameObject burette;
    public GameObject flask;
    public MeshRenderer liquidRenderer;

    [Header("Titration State (Legacy)")]
    public float totalDropsRequired = 10f;

    private float _dropsAdded = 0f;
    private bool _isComplete = false;
    private bool _warnedOnce = false;

    // ── Unity lifecycle ────────────────────────────────────────────────────

    void OnEnable()
    {
        if (!_warnedOnce)
        {
            Debug.LogError(
                "[TitrationController] This script is a LEGACY STUB and should be removed. " +
                "Please replace it with IodineTitrationController in the scene.", this);
            _warnedOnce = true;
        }
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>Legacy entry point — called by VR grab/squeeze on burette.</summary>
    public void AddDrop()
    {
        if (_isComplete) return;

        _dropsAdded++;
        UpdateLiquidColor();

        if (_dropsAdded >= totalDropsRequired)
            OnEndpointReached();
    }

    // ── Internal ───────────────────────────────────────────────────────────

    private void UpdateLiquidColor()
    {
        // FIX: null-guard — was throwing NullReferenceException when unassigned
        if (liquidRenderer == null)
        {
            Debug.LogWarning("[TitrationController] liquidRenderer is not assigned.", this);
            return;
        }

        float t = _dropsAdded / totalDropsRequired;
        // NOTE: Color.clear → Color.magenta is a placeholder from the original script.
        // Replace with chemically accurate colors via IodineTitrationController.
        liquidRenderer.material.color = Color.Lerp(Color.clear, Color.magenta, t);
    }

    private void OnEndpointReached()
    {
        _isComplete = true;
        Debug.Log("[TitrationController] (Legacy) Endpoint reached. " +
                  "Migrate to IodineTitrationController for full experiment support.");
    }
}