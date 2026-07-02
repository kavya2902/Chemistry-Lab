using UnityEngine;

/// <summary>
/// Controls the burette stopcock (nozzle handle).
/// Toggles the liquid flow on/off and notifies IodineTitrationController.
///
/// Interaction modes (choose one per deployment):
///   A. VR poke / grab: wire ToggleNozzle() to a PokeInteractable.WhenSelectingInteractorAdded event.
///   B. Trigger collider: set useCollisionTrigger = true and place a Trigger Collider
///      on this GameObject so any hand/controller entering the collider toggles the nozzle.
///   C. Code: call SetNozzleOpen(bool) from another script.
///
/// CHANGES (this revision):
///  - useCollisionTrigger defaults to FALSE to prevent accidental toggles from
///    nearby colliders / VR controller proximity. Enable it deliberately if needed.
///  - titrationController null-guard with an early LogError in Start so you see it
///    immediately at play time, not only when the nozzle is first toggled.
///  - liquidStream null warning fires only once (_streamWarnLogged flag).
///  - ResetNozzle() forcibly closes the nozzle and plays the closed-state audio.
///  - Gizmos: shows nozzle handle position and open/closed state as a colored dot.
/// </summary>
public class BuretteNozzleController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("IodineTitrationController that manages the experiment state machine.")]
    public IodineTitrationController titrationController;

    [Tooltip("Optional: legacy particle system drip effect.")]
    public ParticleSystem dripParticleSystem;

    [Tooltip("Optional: cylinder-based liquid stream (BuretteLiquidStream component).")]
    public BuretteLiquidStream liquidStream;

    [Tooltip("The nozzle handle Transform that rotates visually when the stopcock opens.")]
    public Transform nozzleHandle;

    [Header("State")]
    [Tooltip("Current open/closed state. Read-only at runtime — use ToggleNozzle() or SetNozzleOpen().")]
    public bool isOpen = false;

    [Header("Nozzle Visual Settings")]
    [Tooltip("Z-axis rotation (degrees) applied to nozzleHandle when the stopcock is open.")]
    public float openRotationZ = 90f;

    [Header("Touch / Poke Trigger")]
    [Tooltip("When TRUE, any Collider entering this object's Trigger Collider toggles the nozzle. " +
             "Keep FALSE when using PokeInteractable events directly — avoids ghost toggles.")]
    public bool useCollisionTrigger = false;

    [Tooltip("Optional tag filter for collision trigger. Leave empty to accept any collider.")]
    public string triggerTag = "";

    [Tooltip("Minimum seconds between consecutive toggle events (debounce).")]
    public float toggleCooldown = 0.5f;

    // ── Internal ───────────────────────────────────────────────────────────
    private Quaternion _closedRotation;
    private bool _streamWarnLogged;
    private float _lastToggleTime = -999f;

    // ── Unity lifecycle ────────────────────────────────────────────────────

    void Start()
    {
        // Always start closed — prevents accidental poke events on scene activation
        // from opening the nozzle before the user has added starch.
        isOpen = false;

        if (nozzleHandle != null)
            _closedRotation = nozzleHandle.localRotation;
        else
            Debug.LogWarning("[BuretteNozzle] nozzleHandle is not assigned — " +
                             "handle will not rotate on open/close.", this);

        if (titrationController == null)
            Debug.LogError("[BuretteNozzle] titrationController is not assigned — " +
                           "nozzle toggles will not affect the experiment.", this);

        // Ensure stream starts hidden
        if (liquidStream != null)
            liquidStream.gameObject.SetActive(false);
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>
    /// Toggles the nozzle open ↔ closed.
    /// Wire to a PokeInteractable's WhenSelectingInteractorAdded UnityEvent.
    /// </summary>
    public void ToggleNozzle()
    {
        isOpen = !isOpen;
        ApplyState();
    }

    /// <summary>Directly sets the nozzle state without toggling.</summary>
    public void SetNozzleOpen(bool open)
    {
        if (isOpen == open) return;
        isOpen = open;
        ApplyState();
    }

    /// <summary>Closes the nozzle and resets visual state. Used on experiment restart.</summary>
    public void ResetNozzle()
    {
        isOpen = false;
        ApplyState();
    }

    // ── Collision / Touch trigger ──────────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        if (!useCollisionTrigger) return;
        if (!string.IsNullOrEmpty(triggerTag) && !other.CompareTag(triggerTag)) return;
        if (Time.time - _lastToggleTime < toggleCooldown) return;

        _lastToggleTime = Time.time;
        ToggleNozzle();
    }

    // ── Internal ───────────────────────────────────────────────────────────

    private void ApplyState()
    {
        // Notify the titration controller
        if (titrationController == null)
        {
            Debug.LogWarning("[BuretteNozzle] Cannot apply nozzle state — " +
                             "titrationController is not assigned.", this);
        }
        else
        {
            // Starch must be added via the beaker interaction — never auto-advance here.
            // BuretteDripSpawner's phase gate prevents drops from spawning in SetupVisible.
            titrationController.SetBuretteFlowing(isOpen);
        }

        // Rotate handle visually
        if (nozzleHandle != null)
        {
            nozzleHandle.localRotation = isOpen
                ? _closedRotation * Quaternion.Euler(0f, 0f, openRotationZ)
                : _closedRotation;
        }

        // Control optional drip particle system
        if (dripParticleSystem != null)
        {
            if (isOpen)
            {
                if (!dripParticleSystem.isPlaying) dripParticleSystem.Play();
            }
            else
            {
                dripParticleSystem.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        // Control cylinder-based stream
        if (liquidStream != null)
        {
            liquidStream.gameObject.SetActive(isOpen);
        }
        else if (!_streamWarnLogged)
        {
            Debug.LogWarning("[BuretteNozzle] liquidStream is not assigned — " +
                             "no cylinder stream will be shown.", this);
            _streamWarnLogged = true;
        }

        Debug.Log($"[BuretteNozzle] Nozzle {(isOpen ? "OPENED" : "CLOSED")}");
    }

    // ── Editor validation & gizmos ─────────────────────────────────────────

#if UNITY_EDITOR
    void OnValidate()
    {
        if (titrationController == null)
            Debug.LogWarning("[BuretteNozzle] titrationController is not assigned.", this);
        if (nozzleHandle == null)
            Debug.LogWarning("[BuretteNozzle] nozzleHandle is not assigned.", this);
    }

    void OnDrawGizmosSelected()
    {
        if (nozzleHandle == null) return;
        Gizmos.color = isOpen ? Color.green : Color.red;
        Gizmos.DrawWireSphere(nozzleHandle.position, 0.01f);
        UnityEditor.Handles.Label(
            nozzleHandle.position + Vector3.up * 0.02f,
            isOpen ? "NOZZLE OPEN" : "NOZZLE CLOSED");
    }
#endif
}
