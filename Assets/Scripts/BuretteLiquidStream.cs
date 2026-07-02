using UnityEngine;

/// <summary>
/// Animates a cylinder GameObject to represent a continuous liquid stream
/// flowing straight down from the burette nozzle tip to the flask surface.
///
/// SETUP REQUIREMENT:
///   • nozzleTip must be the same empty child Transform used by BuretteDripSpawner —
///     the one placed at the exact glass nozzle opening.
///   • flaskSurface should be an empty child Transform on the flask, positioned
///     at the liquid surface level (inside or just above the flask mouth).
///   • The stream cylinder lives in WORLD SPACE (SetParent(null)) so it is not
///     affected by the burette's local rotation.
///
/// CHANGES (this revision):
///  - Stream falls straight down (world-space vertical) — not along the nozzle's
///    local forward axis. This prevents the stream angling sideways when the
///    glass mesh is rotated.
///  - nozzleTip null-guard fires a one-time error with setup instructions.
///  - Added Gizmo (Editor-only): draws the stream endpoint so you can verify
///    nozzleTip and flaskSurface placement without entering Play mode.
///  - ResetStream() clears the sentinel so the first LateUpdate after a reset
///    re-applies the correct active state.
/// </summary>
public class BuretteLiquidStream : MonoBehaviour
{
    [Header("References")]
    [Tooltip("BuretteNozzleController that reports whether the stopcock is open.")]
    public BuretteNozzleController nozzleController;

    [Tooltip("Exact nozzle tip Transform — must be the same empty child placed at the " +
             "glass nozzle opening that BuretteDripSpawner uses.")]
    public Transform nozzleTip;

    [Tooltip("Transform at the flask liquid surface — stream's lower endpoint. " +
             "Falls back to 0.4 m below nozzleTip if unassigned.")]
    public Transform flaskSurface;

    [Header("Stream Settings")]
    [Tooltip("When TRUE, hides the continuous-stream cylinder entirely and uses only " +
             "the discrete drops from BuretteDripSpawner. Recommended for drop-by-drop mode.")]
    public bool disableStream = true;

    [Tooltip("Minimum Y local scale so the stream cylinder never vanishes entirely.")]
    public float minScaleY = 0.001f;

    // ── Internal ───────────────────────────────────────────────────────────
    private bool? _wasVisible;
    private bool _warnedController;
    private bool _warnedTip;

    // ── Unity lifecycle ────────────────────────────────────────────────────

    void Start()
    {
        // Detach to world space so burette rotation doesn't skew the stream
        transform.SetParent(null, true);
        gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (disableStream)
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
            return;
        }

        if (nozzleController == null)
        {
            if (!_warnedController)
            {
                Debug.LogWarning("[BuretteLiquidStream] nozzleController is not assigned.", this);
                _warnedController = true;
            }
            return;
        }

        bool shouldShow = nozzleController.isOpen;
        if (_wasVisible == null || shouldShow != _wasVisible.Value)
        {
            gameObject.SetActive(shouldShow);
            _wasVisible = shouldShow;
        }

        if (!shouldShow) return;

        if (nozzleTip == null)
        {
            if (!_warnedTip)
            {
                Debug.LogWarning(
                    "[BuretteLiquidStream] nozzleTip is not assigned.\n" +
                    "Assign the same NozzleTip child Transform used by BuretteDripSpawner.", this);
                _warnedTip = true;
            }
            return;
        }

        Vector3 tipPos = nozzleTip.position;

        // Stream falls STRAIGHT DOWN — same X/Z as the nozzle tip.
        // Using the nozzle's local forward/down would cause an angled stream
        // whenever the glass mesh pivot is rotated.
        float bottomY = flaskSurface != null
            ? flaskSurface.position.y
            : tipPos.y - 0.4f;

        float streamLength = tipPos.y - bottomY;
        if (streamLength < 0.005f) return;

        // Centre the Unity default cylinder (2 units tall) between tip and flask surface
        transform.position = new Vector3(
            tipPos.x,
            tipPos.y - streamLength * 0.5f,
            tipPos.z);

        // Identity rotation: Unity cylinder Y-axis is already world-vertical
        transform.rotation = Quaternion.identity;

        // Scale: Unity default cylinder = 2 units along Y → scaleY = length / 2
        Vector3 s = transform.localScale;
        s.y = Mathf.Max(minScaleY, streamLength * 0.5f);
        transform.localScale = s;
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>Forcibly hides the stream. Called on experiment reset.</summary>
    public void ResetStream()
    {
        _wasVisible = null;
        gameObject.SetActive(false);
    }

    // ── Editor validation & gizmos ─────────────────────────────────────────

#if UNITY_EDITOR
    void OnValidate()
    {
        if (nozzleController == null)
            Debug.LogWarning("[BuretteLiquidStream] nozzleController is not assigned.", this);
        if (nozzleTip == null)
            Debug.LogWarning("[BuretteLiquidStream] nozzleTip is not assigned.", this);
        if (flaskSurface == null)
            Debug.LogWarning("[BuretteLiquidStream] flaskSurface is not assigned — " +
                             "will fall back to 0.4 m below nozzleTip.", this);
    }

    void OnDrawGizmosSelected()
    {
        if (nozzleTip == null) return;

        Vector3 tipPos  = nozzleTip.position;
        float   bottomY = flaskSurface != null ? flaskSurface.position.y : tipPos.y - 0.4f;
        Vector3 bottomPt = new Vector3(tipPos.x, bottomY, tipPos.z);

        // Blue line = stream path (straight down)
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.8f);
        Gizmos.DrawLine(tipPos, bottomPt);

        // Small sphere at tip
        Gizmos.DrawWireSphere(tipPos,    0.005f);
        // Small sphere at surface
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(bottomPt, 0.008f);

        UnityEditor.Handles.Label(tipPos    + Vector3.right * 0.01f, "Stream Start");
        UnityEditor.Handles.Label(bottomPt + Vector3.right * 0.01f, "Stream End");
    }
#endif
}
