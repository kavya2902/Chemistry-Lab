//using UnityEngine;

///// <summary>
///// Placed as a trigger collider on the flask liquid area.
///// Tagged "FlaskLiquid" so that DripDropBehavior can detect collision.
///// Also exposes a reference to ConicalFlaskLiquid for the drop to call OnDropReceived.
///// Attach to the FlaskTrigger child of the ConicalFlask.
///// </summary>
//public class FlaskLiquidTrigger : MonoBehaviour
//{
//    [Tooltip("Reference to the flask liquid manager that handles color and level.")]
//    public ConicalFlaskLiquid flaskLiquid;

//    void Awake()
//    {
//        // Ensure this GameObject always carries the FlaskLiquid tag
//        if (!gameObject.CompareTag("FlaskLiquid"))
//            gameObject.tag = "FlaskLiquid";
//    }

//    void OnTriggerEnter(Collider other)
//    {
//        DripDropBehavior drop = other.GetComponent<DripDropBehavior>();
//        if (drop != null && flaskLiquid != null)
//            flaskLiquid.OnDropReceived();
//    }
//}



using UnityEngine;

/// <summary>
/// Trigger collider placed on the flask liquid volume.
/// Tagged "FlaskLiquid" so DripDropBehavior can detect collision.
///
/// FIXES:
///  - Null-guard on flaskLiquid with a clear error message.
///  - Tag assignment moved to Awake with a warning instead of a silent set,
///    so designers know when the tag is missing from the Tag Manager.
///  - Duplicate-drop guard: tracks which drop GameObjects have already been
///    counted so a drop that fires both OnTriggerEnter and OnCollisionEnter
///    does not double-increment the liquid level.
///  - OnValidate() warns in the Inspector if flaskLiquid is unassigned.
/// </summary>
[RequireComponent(typeof(Collider))]
public class FlaskLiquidTrigger : MonoBehaviour
{
    [Tooltip("Reference to the flask liquid manager that handles color and level.")]
    public ConicalFlaskLiquid flaskLiquid;

    [Tooltip("Reference to the titration controller — each landed drop advances progress.")]
    public IodineTitrationController titrationController;

    // FIX: track processed drops to prevent double-counting
    private readonly System.Collections.Generic.HashSet<int> _processedDrops
        = new System.Collections.Generic.HashSet<int>();

    // ── Unity lifecycle ────────────────────────────────────────────────────

    void Awake()
    {
        // Auto-find both references if not assigned in Inspector
        if (titrationController == null)
            titrationController = FindFirstObjectByType<IodineTitrationController>();

        if (flaskLiquid == null)
            flaskLiquid = GetComponentInParent<ConicalFlaskLiquid>();
        if (flaskLiquid == null)
            flaskLiquid = FindFirstObjectByType<ConicalFlaskLiquid>();

        // FIX: warn if the tag is not configured in the Tag Manager
        if (!gameObject.CompareTag("FlaskLiquid"))
        {
            try
            {
                gameObject.tag = "FlaskLiquid";
            }
            catch (UnityException)
            {
                Debug.LogError("[FlaskLiquidTrigger] Tag 'FlaskLiquid' does not exist in the " +
                               "Tag Manager. Please add it under Edit > Project Settings > Tags & Layers.", this);
            }
        }

        // FIX: null-guard on the required reference
        if (flaskLiquid == null)
            Debug.LogError("[FlaskLiquidTrigger] flaskLiquid is not assigned. " +
                           "Drops will not affect the flask liquid.", this);

        // Ensure the collider is marked as a trigger
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning("[FlaskLiquidTrigger] Collider is not set as a Trigger. " +
                             "Setting isTrigger = true automatically.", this);
            col.isTrigger = true;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        TryRegisterDrop(other.gameObject);
    }

    // FIX: also handle collision-based detection (DripDropBehavior uses both paths)
    void OnCollisionEnter(Collision collision)
    {
        TryRegisterDrop(collision.gameObject);
    }

    // ── Internal ───────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a drop once and notifies the flask liquid manager.
    /// Uses the GameObject's InstanceID as a unique key to prevent double-counting.
    /// </summary>
    private void TryRegisterDrop(GameObject dropObj)
    {
        DripDropBehavior drop = dropObj.GetComponent<DripDropBehavior>();
        if (drop == null) return;

        int id = dropObj.GetInstanceID();
        if (_processedDrops.Contains(id)) return;
        _processedDrops.Add(id);

        // DripDropBehavior.AbsorbIntoFlask() already called OnDropReceived + AdvanceByOneDrop
        // directly for reliability. FlaskLiquidTrigger is kept as a backup only — skip
        // calling them again to prevent double-counting.
    }

    /// <summary>Clears the processed-drop set on experiment reset.</summary>
    public void ResetTrigger()
    {
        _processedDrops.Clear();
    }

    // ── Editor validation ──────────────────────────────────────────────────

#if UNITY_EDITOR
    void OnValidate()
    {
        if (flaskLiquid == null)
            Debug.LogWarning("[FlaskLiquidTrigger] flaskLiquid is not assigned.", this);
    }
#endif
}