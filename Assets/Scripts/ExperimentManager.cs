using UnityEngine;

public class ExperimentManager : MonoBehaviour
{
    [Header("Experiment Scene Groups")]
    public GameObject titrationSetup;
    public GameObject turbiditySetup;
    public GameObject litmusTestSetup;

    [Header("UI")]
    public GameObject dashboardUI;          // main selection menu
    public GameObject litmusDashboardUI;    // litmus instruction panel (LitmusDashboardUI)

    [Header("Back Buttons (assign the back-button GameObjects here)")]
    [Tooltip("The back button inside the titration scene. Shown only during titration.")]
    public GameObject titrationBackButton;
    [Tooltip("The back button inside the litmus scene. Shown only during litmus test.")]
    public GameObject litmusBackButton;

    [Header("Controllers (auto-found if not assigned)")]
    public IodineTitrationController titrationController;

    [Header("Teleport on Back (optional)")]
    [Tooltip("Place an empty GameObject in front of the main menu and assign it here. " +
             "If left empty the position is calculated automatically from dashboardUI.")]
    public Transform menuViewPoint;

    // Cached camera rig — found once, reused on every back-button press
    private Transform _rigTransform;

    private void Start() { } // ShowMainMenu moved to Awake so it runs before any child Start()

    private void Awake()
    {
        if (turbiditySetup != null && turbiditySetup == litmusTestSetup)
        {
            Debug.LogWarning("[ExperimentManager] turbiditySetup was incorrectly pointing to " +
                             "litmusTestSetup. Clearing turbiditySetup to prevent conflicts.");
            turbiditySetup = null;
        }

        // dashboardUI must be independent of titrationSetup in the hierarchy.
        // If it is currently a child of titrationSetup (or any other object that gets
        // hidden), detach it to the scene root so ShowMainMenu() can always make it visible.
        if (dashboardUI != null && dashboardUI.transform.parent != null)
        {
            Debug.Log($"[ExperimentManager] dashboardUI '{dashboardUI.name}' was a child of " +
                      $"'{dashboardUI.transform.parent.name}' — detaching to scene root so it " +
                      "is never hidden by its parent.", this);
            dashboardUI.transform.SetParent(null, true); // keep world position
        }

        if (dashboardUI == null)
            Debug.LogError("[ExperimentManager] dashboardUI is NOT assigned! Assign the main menu Canvas in the Inspector.", this);

        if (litmusDashboardUI == null)
        {
            var found = FindFirstObjectByType<LitmusDashboardUI>(FindObjectsInactive.Include);
            if (found != null)
            {
                litmusDashboardUI = found.gameObject;
                Debug.Log("[ExperimentManager] Auto-found LitmusDashboardUI: " + found.gameObject.name);
            }
        }

        if (titrationController == null && titrationSetup != null)
            titrationController = titrationSetup.GetComponentInChildren<IodineTitrationController>(true);

        // Hide all experiments immediately in Awake so no child Start() runs while active
        ShowMainMenu();
    }

    /// <summary>Activates the titration experiment and hides all others.</summary>
    public void StartTitration()
    {
        SetActive(titrationSetup,     true);
        SetActive(turbiditySetup,     false);
        SetActive(litmusTestSetup,    false);
        SetActive(dashboardUI,        false);
        SetActive(litmusDashboardUI,  false);
        SetActive(titrationBackButton, true);
        SetActive(litmusBackButton,   false);

        // Re-find controller if it became null after a reset
        if (titrationController == null && titrationSetup != null)
            titrationController = titrationSetup.GetComponentInChildren<IodineTitrationController>(true);

        titrationController?.StartExperiment();
    }

    /// <summary>Activates the turbidity experiment and hides all others.</summary>
    public void StartTurbidity()
    {
        // turbiditySetup is optional — only activate if assigned and distinct.
        if (turbiditySetup != null) SetActive(turbiditySetup, true);
        SetActive(titrationSetup,    false);
        SetActive(litmusTestSetup,   false);
        SetActive(dashboardUI,       false);
        SetActive(litmusDashboardUI, false);
    }

    /// <summary>Activates the litmus paper test and its dashboard; hides all others.</summary>
    public void StartLitmusTest()
    {
        SetActive(litmusTestSetup,    true);
        SetActive(litmusDashboardUI,  true);
        SetActive(titrationSetup,     false);
        SetActive(turbiditySetup,     false);
        SetActive(dashboardUI,        false);
        SetActive(litmusBackButton,   true);
        SetActive(titrationBackButton, false);
    }

    /// <summary>Returns to the main experiment-selection menu.</summary>
    public void ShowMainMenu()
    {
        SetActive(dashboardUI,         true);
        SetActive(titrationSetup,      false);
        SetActive(turbiditySetup,      false);
        SetActive(litmusTestSetup,     false);
        SetActive(litmusDashboardUI,   false);
        SetActive(titrationBackButton, false);
        SetActive(litmusBackButton,    false);
    }

    /// <summary>
    /// Called by the back button — returns to menu AND resets the titration
    /// so it starts fresh next time.
    /// </summary>
    public void ShowMainMenuAndReset()
    {
        titrationController?.ResetExperiment();
        TeleportPlayerToMenu();
        ShowMainMenu();
    }

    /// <summary>
    /// Moves the VR camera rig to a viewpoint directly in front of the main menu
    /// so the user immediately sees it without having to turn around or walk.
    /// </summary>
    private void TeleportPlayerToMenu()
    {
        // Find the camera rig once and cache it — FindFirstObjectByType is expensive
        if (_rigTransform == null)
        {
            var ovrRig = FindFirstObjectByType<OVRCameraRig>();
            if (ovrRig != null)
                _rigTransform = ovrRig.transform;
            else if (Camera.main != null)
                _rigTransform = Camera.main.transform.root;
        }

        Transform rig = _rigTransform;
        if (rig == null) return;

        if (menuViewPoint != null)
        {
            // Use the manually placed viewpoint if assigned in Inspector
            rig.SetPositionAndRotation(menuViewPoint.position, menuViewPoint.rotation);
            return;
        }

        if (dashboardUI == null) return;

        // Auto-calculate: stand 1.5 m in front of the menu canvas, same floor height
        Transform menuT = dashboardUI.transform;
        Vector3 targetPos = menuT.position - menuT.forward * 1.5f;
        targetPos.y = rig.position.y; // keep rig at floor level

        rig.position = targetPos;

        // Rotate rig to face the canvas (yaw only)
        Vector3 toMenu = menuT.position - targetPos;
        toMenu.y = 0f;
        if (toMenu.sqrMagnitude > 0.001f)
            rig.rotation = Quaternion.LookRotation(toMenu);
    }

    private static void SetActive(GameObject go, bool active)
    {
        if (go != null) go.SetActive(active);
    }
}
