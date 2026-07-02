using UnityEngine;

/// <summary>
/// Bridges the "Start Titration" UI button to ExperimentManager.
/// Delegates all show/hide logic to ExperimentManager.StartTitration() so the
/// dashboard is properly hidden and the back button is properly shown in one call.
/// </summary>
public class TitrationSetupManager : MonoBehaviour
{
    [Tooltip("Auto-found if not assigned.")]
    public ExperimentManager experimentManager;

    void Awake()
    {
        if (experimentManager == null)
            experimentManager = FindFirstObjectByType<ExperimentManager>();

        if (experimentManager == null)
            Debug.LogError("[TitrationSetupManager] ExperimentManager not found. Assign it in the Inspector.", this);
    }

    /// <summary>Called by the 'Start Titration' UI button OnClick event.</summary>
    public void StartTitration()
    {
        if (experimentManager != null)
            experimentManager.StartTitration();
        else
            Debug.LogError("[TitrationSetupManager] Cannot start — ExperimentManager is null.", this);
    }
}
