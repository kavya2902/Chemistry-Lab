using UnityEngine;

/// <summary>
/// Attach to NozzleEnd (child of BuretteModel).
///
/// This script is the ONLY thing on NozzleEnd that needs to know about flow.
/// It calls BuretteNozzleController.ToggleNozzle() which already handles:
///   - Playing / stopping BuretteFlow_PS
///   - Enabling / disabling LiquidStream
///   - Driving BuretteDripSpawner (via nozzleController.isOpen)
///   - Notifying IodineTitrationController
///
/// Liquid spawns from NozzleTip (set on BuretteDripSpawner.nozzleTip),
/// NOT from NozzleEnd — the two objects stay independent.
///
/// WIRING (Inspector):
///   PointableUnityEventWrapper → WhenSelect (UnityEvent)
///     → NozzlePokeFlowBridge.OnPoked
/// </summary>
public class NozzlePokeFlowBridge : MonoBehaviour
{
    [Header("Required")]
    [Tooltip("BuretteNozzleController sitting on the BuretteNozzle object.")]
    public BuretteNozzleController nozzleController;

    [Header("Settings")]
    [Tooltip("Seconds that must pass before a second poke is accepted. Prevents double-toggle.")]
    public float pokeCooldown = 0.5f;

    private float _lastPokeTime = -999f;

    // ── Called by PointableUnityEventWrapper → WhenSelect ─────────────────

    /// <summary>
    /// Wire this to PointableUnityEventWrapper.WhenSelect in the Inspector.
    /// Each call toggles liquid flow on or off.
    /// </summary>
    public void OnPoked()
    {
        if (Time.time - _lastPokeTime < pokeCooldown) return;
        _lastPokeTime = Time.time;

        if (nozzleController == null)
        {
            Debug.LogError("[NozzlePokeFlowBridge] nozzleController is not assigned on " +
                           gameObject.name + ". Drag BuretteNozzleController here.", this);
            return;
        }

        nozzleController.ToggleNozzle();
        Debug.Log("[NozzlePokeFlowBridge] Poke on NozzleEnd → nozzle is now " +
                  (nozzleController.isOpen ? "OPEN (flowing)" : "CLOSED"));
    }

    // ── Editor validation ──────────────────────────────────────────────────

#if UNITY_EDITOR
    void OnValidate()
    {
        if (nozzleController == null)
            Debug.LogWarning("[NozzlePokeFlowBridge] nozzleController is not assigned on " +
                             gameObject.name, this);
    }
#endif
}
