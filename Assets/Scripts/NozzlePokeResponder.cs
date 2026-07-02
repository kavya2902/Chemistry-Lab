//using UnityEngine;

///// <summary>
///// Lightweight responder that forwards a poke / select event to the
///// BuretteNozzleController. Wire this GameObject's OnSelect (or the
///// PokeInteractable's WhenSelectingInteractorAdded) UnityEvent to
///// the OnPoked() method below.
///// Attach to the NozzleHandle or BuretteNozzle GameObject.
///// </summary>
//public class NozzlePokeResponder : MonoBehaviour
//{
//    [Header("Reference")]
//    public BuretteNozzleController nozzleController;

//    /// <summary>Call this from any VR interaction event (poke, grab select, UI button).</summary>
//    public void OnPoked()
//    {
//        if (nozzleController != null)
//            nozzleController.ToggleNozzle();
//    }
//}


using UnityEngine;

/// <summary>
/// Lightweight responder that forwards a poke / select event to BuretteNozzleController.
///
/// FIXES:
///  - Runtime null-guard with a descriptive error if nozzleController is missing.
///  - Haptic feedback via OVRInput (Oculus) with graceful fallback when OVR is absent.
///  - Audio click feedback via AudioSource (optional).
///  - OnValidate() warns in the Inspector if the reference is unassigned.
///  - IsInteractable() gate so pokes are ignored if the experiment is not in the
///    correct phase (prevents the nozzle being opened before starch is poured).
/// </summary>
public class NozzlePokeResponder : MonoBehaviour
{
    [Header("Reference")]
    public BuretteNozzleController nozzleController;
    public IodineTitrationController titrationController;  // phase gate

    [Header("Phase Gate")]
    [Tooltip("When TRUE, poke works at any experiment phase (recommended for quick setup).\n" +
             "When FALSE, poke is blocked until StarchAdded or TitrationInProgress phase.")]
    public bool bypassPhaseGate = true;

    [Header("Feedback (optional)")]
    [Tooltip("AudioSource for click sound on poke. Leave empty to skip.")]
    public AudioSource clickAudio;

    [Tooltip("Haptic duration in seconds sent to the Oculus controller.")]
    public float hapticDuration = 0.05f;
    [Tooltip("Haptic amplitude 0-1.")]
    public float hapticAmplitude = 0.4f;

    // ── Unity lifecycle ────────────────────────────────────────────────────

    void Start()
    {
        if (nozzleController == null)
            Debug.LogError("[NozzlePokeResponder] nozzleController is not assigned — " +
                           "poke events will be silently ignored.", this);
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>
    /// Call this from any VR interaction event (PokeInteractable.WhenSelectingInteractorAdded,
    /// GrabInteractable OnSelect, or a UI Button OnClick).
    /// </summary>
    public void OnPoked()
    {
        // FIX: null guard with informative log
        if (nozzleController == null)
        {
            Debug.LogWarning("[NozzlePokeResponder] OnPoked() called but nozzleController " +
                             "is not assigned.", this);
            return;
        }

        // FIX: phase gate — nozzle should only be operable after starch is added
        if (!IsInteractable())
        {
            Debug.Log("[NozzlePokeResponder] Poke ignored — experiment is not in the " +
                      "correct phase yet.", this);
            return;
        }

        nozzleController.ToggleNozzle();
        PlayFeedback();
    }

    // ── Internal ───────────────────────────────────────────────────────────

    /// <summary>Returns true only when the experiment phase allows nozzle operation.</summary>
    private bool IsInteractable()
    {
        if (bypassPhaseGate) return true;            // bypass: always allow
        if (titrationController == null) return true; // no controller — always allow

        var phase = titrationController.currentPhase;
        return phase == IodineTitrationController.ExperimentPhase.StarchAdded ||
               phase == IodineTitrationController.ExperimentPhase.TitrationInProgress;
    }

    private void PlayFeedback()
    {
        // Audio click
        if (clickAudio != null)
            clickAudio.Play();

        // FIX: Haptic via OVRInput — wrapped in try/catch so it doesn't throw
        //      in builds where the Oculus SDK is not present.
        TryHaptic();
    }

    private void TryHaptic()
    {
        try
        {
            // OVRInput.SetControllerVibration(frequency, amplitude, controller)
            var ovrInputType = System.Type.GetType("OVRInput, Oculus.VR");
            if (ovrInputType == null) return;

            // SetControllerVibration(float freq, float amp, OVRInput.Controller ctrl)
            var method = ovrInputType.GetMethod("SetControllerVibration",
                new[] { typeof(float), typeof(float),
                        ovrInputType.Assembly.GetType("OVRInput+Controller") });
            if (method == null) return;

            // Controller.RTouch = 4, Controller.LTouch = 8
            // We vibrate both hands since we don't track which hand poked
            object rTouch = System.Enum.ToObject(
                ovrInputType.Assembly.GetType("OVRInput+Controller"), 4);
            object lTouch = System.Enum.ToObject(
                ovrInputType.Assembly.GetType("OVRInput+Controller"), 8);

            method.Invoke(null, new object[] { 1f, hapticAmplitude, rTouch });
            method.Invoke(null, new object[] { 1f, hapticAmplitude, lTouch });

            // Stop vibration after hapticDuration
            StartCoroutine(StopHapticAfter(hapticDuration, method, rTouch, lTouch));
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[NozzlePokeResponder] Haptic call failed: " + ex.Message);
        }
    }

    private System.Collections.IEnumerator StopHapticAfter(
        float delay, System.Reflection.MethodInfo method, object rTouch, object lTouch)
    {
        yield return new WaitForSeconds(delay);
        try
        {
            method.Invoke(null, new object[] { 0f, 0f, rTouch });
            method.Invoke(null, new object[] { 0f, 0f, lTouch });
        }
        catch { /* SDK unavailable — ignore */ }
    }

    // ── Editor validation ──────────────────────────────────────────────────

#if UNITY_EDITOR
    void OnValidate()
    {
        if (nozzleController == null)
            Debug.LogWarning("[NozzlePokeResponder] nozzleController is not assigned.", this);
    }
#endif
}