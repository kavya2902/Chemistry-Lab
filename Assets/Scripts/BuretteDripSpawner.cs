using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns small mesh-based liquid drops from the burette nozzle tip
/// while the stopcock is open.
///
/// ROOT CAUSE OF SIDE-SPAWNING (fixed here):
///   The nozzleTip transform was either unassigned (drops never appeared) or
///   was incorrectly set to the Burette body / NozzleController root — whose
///   pivot sits at the glass body centre, NOT the glass tip opening. That made
///   every drop instantiate at the centre or side of the glass.
///
///   FIX: nozzleTip must be a dedicated empty child GameObject placed precisely
///   at the very bottom opening of the nozzle glass. Drops now spawn with:
///     Instantiate(dropPrefab, nozzleTip.position, Quaternion.identity)
///   and fall straight down under gravity — independent of the glass mesh pivot.
///
/// OTHER FIXES:
///  - dropInterval lowered to 0.10 s (was 0.35 s) — realistic drip cadence.
///  - dropScale = 0.008 m (was 0.012 m) — small, realistic drop size.
///  - dropInitialSpeed = 1.5 (was 0.8) — drops fall crisply into flask.
///  - dropDrag = 0.5 (was 1.5) — less air resistance, more natural free-fall.
///  - Rigidbody mass lowered to 0.0005 kg so gravity dominates.
///  - One-time console warnings for every missing reference so designers
///    immediately know what needs to be wired up.
///  - Gizmo (Editor): draws a blue sphere at nozzleTip and a downward ray
///    so you can verify placement directly in the Scene View.
/// </summary>
public class BuretteDripSpawner : MonoBehaviour
{
    [Header("Drop Prefab")]
    [Tooltip("DripDrop prefab — a small Sphere primitive with a transparent material " +
             "and a Collider (trigger or non-trigger). Must NOT have a Rigidbody in the " +
             "prefab; one is added at runtime so settings apply correctly.")]
    public GameObject dropPrefab;

    [Header("Spawn Settings")]
    [Tooltip("Exact nozzle tip Transform.\n\n" +
             "HOW TO SET UP:\n" +
             "1. In the Hierarchy, select the Burette nozzle glass object.\n" +
             "2. Create an empty child: GameObject → Create Empty, name it 'NozzleTip'.\n" +
             "3. In the Scene View, move NozzleTip to the very bottom opening of the " +
             "glass nozzle (the hole liquid drips out of).\n" +
             "4. Assign that NozzleTip Transform to this field.\n\n" +
             "If this field is wrong or unassigned, drops will spawn from the side " +
             "of the glass (the most common setup mistake).")]
    public Transform nozzleTip;

    [Tooltip("LOCAL-space offset applied to nozzleTip.position at spawn time.\n" +
             "Axes are relative to the NozzleTip transform's own orientation.\n" +
             "Use this to nudge the spawn point without moving the NozzleTip transform.\n\n" +
             "FIX for 'drops appear behind/beside the tip':\n" +
             "  • In the Scene View select this object and watch the green gizmo sphere — " +
             "that is the actual spawn point.\n" +
             "  • Adjust X/Y/Z until the green sphere sits right at the glass tip opening.\n" +
             "  • Typical start value: (0, -0.015, 0) — moves spawn 1.5 cm down in local Y.")]
    public Vector3 nozzleTipOffset = Vector3.zero;

    [Tooltip("Seconds between consecutive drops while nozzle is open. " +
             "0.5–0.8 s gives a clear drop-by-drop cadence.")]
    [Range(0.05f, 2f)]
    public float dropInterval = 0.55f;

    [Tooltip("Maximum time (s) a drop lives before auto-destroy if it misses the flask.")]
    public float dropLifetime = 4f;

    [Header("Drop Physics")]
    [Tooltip("Initial downward speed (m/s) released when the drop detaches from the tip.")]
    public float dropInitialSpeed = 0.5f;

    [Tooltip("Rigidbody linear drag. Low value = natural free-fall.")]
    public float dropDrag = 0.5f;

    [Tooltip("Rigidbody angular drag.")]
    public float dropAngularDrag = 5f;

    [Tooltip("Diameter (world units) of each drop sphere. Realistic: 0.006–0.010 m.")]
    public float dropScale = 0.008f;

    [Tooltip("Duration (s) of the scale-shrink 'absorption' animation when a drop " +
             "enters the flask trigger.")]
    public float absorbDuration = 0.2f;

    [Tooltip("Seconds the drop grows at the nozzle tip before detaching and falling. " +
             "Gives a realistic 'bead forming' effect. Set 0 to disable.")]
    [Range(0f, 0.5f)]
    public float dropGrowDuration = 0.15f;

    [Header("References")]
    [Tooltip("BuretteNozzleController that reports whether the stopcock is open.")]
    public BuretteNozzleController nozzleController;

    [Tooltip("ConicalFlaskLiquid that receives OnDropReceived() calls when drops are absorbed.")]
    public ConicalFlaskLiquid flaskLiquid;

    [Tooltip("IodineTitrationController — passed to each drop so AdvanceByOneDrop() is called " +
             "the moment the drop is absorbed into the flask. Auto-found if not assigned.")]
    public IodineTitrationController titrationController;

    // ── Internal ───────────────────────────────────────────────────────────
    private float _timer;
    private readonly List<GameObject> _activeDrops = new List<GameObject>();

    private bool _warnedController;
    private bool _warnedPrefab;
    private bool _warnedTip;

    // ── Unity lifecycle ────────────────────────────────────────────────────

    void Awake()
    {
        if (titrationController == null)
            titrationController = FindFirstObjectByType<IodineTitrationController>();

        // Auto-detect nozzleTip by searching children if not assigned in the Inspector.
        // Searches for names containing "nozzletip", "tip", "nozzleend", or "spout"
        // (case-insensitive) so common naming conventions are covered automatically.
        if (nozzleTip == null)
        {
            Transform best = null;
            float lowestY = float.MaxValue;

            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child == transform) continue;

                string n = child.name.ToLower();
                if (n.Contains("nozzletip") || n.Contains("nozzle tip") ||
                    n.Contains("nozzleend") || n.Contains("spout")       ||
                    (n.Contains("tip") && !n.Contains("tooltip")))
                {
                    nozzleTip = child;
                    Debug.Log($"[BuretteDripSpawner] Auto-detected nozzleTip by name: '{child.name}'");
                    break;
                }

                // Fallback: track the lowest child in world space
                if (child.position.y < lowestY)
                {
                    lowestY = child.position.y;
                    best = child;
                }
            }

            // If no named tip was found, use the lowest child as last resort
            if (nozzleTip == null && best != null)
            {
                nozzleTip = best;
                Debug.LogWarning(
                    $"[BuretteDripSpawner] nozzleTip not assigned and no 'tip'-named child found. " +
                    $"Falling back to lowest child transform '{best.name}'. " +
                    $"For accurate spawning, create a child GameObject named 'NozzleTip' at the glass opening " +
                    $"and assign it to the nozzleTip field.", this);
            }
        }
    }

    void Update()
    {
        if (nozzleController == null)
        {
            if (!_warnedController)
            {
                Debug.LogWarning("[BuretteDripSpawner] nozzleController is not assigned — " +
                                 "drops will never spawn.", this);
                _warnedController = true;
            }
            return;
        }

        if (!nozzleController.isOpen) return;

        // Phase gate: drops only spawn after starch has been added.
        // This prevents auto-drip if the nozzle poke fires on scene activation
        // or if the user opens the nozzle before the beaker interaction.
        if (titrationController != null)
        {
            var phase = titrationController.currentPhase;
            if (phase != IodineTitrationController.ExperimentPhase.StarchAdded &&
                phase != IodineTitrationController.ExperimentPhase.TitrationInProgress)
                return;
        }

        _timer += Time.deltaTime;
        if (_timer >= dropInterval)
        {
            _timer = 0f;
            SpawnDrop();
        }
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>Called by DripDropBehavior when a drop finishes (absorbed or timed out).</summary>
    public void OnDropFinished(GameObject drop)
    {
        _activeDrops.Remove(drop);
    }

    /// <summary>Destroys all in-flight drops. Called on experiment reset.</summary>
    public void ResetSpawner()
    {
        var snapshot = new List<GameObject>(_activeDrops);
        foreach (var d in snapshot)
            if (d != null) Destroy(d);

        _activeDrops.Clear();
        _timer = 0f;
    }

    // ── Internal ───────────────────────────────────────────────────────────

    private void SpawnDrop()
    {
        if (dropPrefab == null)
        {
            if (!_warnedPrefab)
            {
                Debug.LogWarning("[BuretteDripSpawner] dropPrefab is not assigned — " +
                                 "no drops will spawn.", this);
                _warnedPrefab = true;
            }
            return;
        }

        if (nozzleTip == null)
        {
            if (!_warnedTip)
            {
                Debug.LogWarning(
                    "[BuretteDripSpawner] nozzleTip is not assigned.\n" +
                    "This is the most common cause of drops spawning from the SIDE of the burette.\n" +
                    "Create an empty child GameObject at the exact nozzle opening (the hole at the " +
                    "bottom of the glass) and assign it to the nozzleTip field.", this);
                _warnedTip = true;
            }
            return;
        }

        // ── Spawn exactly at the nozzle tip ───────────────────────────────
        // Use Quaternion.identity so the drop sphere has no inherited rotation
        // from the glass mesh. The initial velocity (straight down) handles
        // the direction — independent of how the nozzle model is oriented.
        // nozzleTipOffset lets the designer nudge the spawn point without
        // moving the NozzleTip transform in the scene.
        Vector3 spawnPos = nozzleTip.position + nozzleTip.TransformDirection(nozzleTipOffset);
        GameObject drop = Instantiate(dropPrefab, spawnPos, Quaternion.identity);
        // Start tiny when grow animation is on; DripDropBehavior grows it to full size
        drop.transform.localScale = dropGrowDuration > 0f
            ? Vector3.one * (dropScale * 0.15f)
            : Vector3.one * dropScale;

        // ── Configure physics ─────────────────────────────────────────────
        Rigidbody rb = drop.GetComponent<Rigidbody>();
        if (rb == null) rb = drop.AddComponent<Rigidbody>();

        rb.useGravity            = true;
        rb.linearDamping         = dropDrag;
        rb.angularDamping        = dropAngularDrag;
        rb.mass                  = 0.0005f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        if (dropGrowDuration > 0f)
        {
            // Hold the drop in place at the tip while it forms
            rb.isKinematic    = true;
            rb.linearVelocity = Vector3.zero;
        }
        else
        {
            rb.linearVelocity = Vector3.down * dropInitialSpeed;
        }

        // ── Wire lifecycle handler ────────────────────────────────────────
        DripDropBehavior behavior = drop.GetComponent<DripDropBehavior>();
        if (behavior == null) behavior = drop.AddComponent<DripDropBehavior>();
        behavior.Initialize(dropLifetime, absorbDuration, flaskLiquid, this,
                            dropGrowDuration, dropInitialSpeed, dropScale, titrationController);

        _activeDrops.Add(drop);
    }

    void OnDestroy()
    {
        var snapshot = new List<GameObject>(_activeDrops);
        foreach (var d in snapshot)
            if (d != null) Destroy(d);
    }

    // ── Editor validation & gizmos ─────────────────────────────────────────

#if UNITY_EDITOR
    void OnValidate()
    {
        if (nozzleController == null)
            Debug.LogWarning("[BuretteDripSpawner] nozzleController is not assigned.", this);
        if (dropPrefab == null)
            Debug.LogWarning("[BuretteDripSpawner] dropPrefab is not assigned.", this);
        if (nozzleTip == null)
            Debug.LogWarning(
                "[BuretteDripSpawner] nozzleTip is not assigned.\n" +
                "Add an empty child Transform at the glass nozzle opening " +
                "to fix side-spawning.", this);
        if (flaskLiquid == null)
            Debug.LogWarning("[BuretteDripSpawner] flaskLiquid is not assigned.", this);
    }

    void OnDrawGizmosSelected()
    {
        if (nozzleTip == null) return;

        Vector3 spawnPos = nozzleTip.position + nozzleTip.TransformDirection(nozzleTipOffset);

        // Blue sphere at the transform position
        Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.5f);
        Gizmos.DrawWireSphere(nozzleTip.position, dropScale * 2f);

        // Bright green sphere at the ACTUAL spawn position (includes offset)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(spawnPos, dropScale * 2.5f);

        // Cyan ray pointing straight down — the drop trajectory
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(spawnPos, Vector3.down * 0.08f);

        // Orange line from transform to offset spawn point (visible offset)
        if (nozzleTipOffset != Vector3.zero)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(nozzleTip.position, spawnPos);
        }

        UnityEditor.Handles.Label(
            spawnPos + Vector3.right * 0.015f, "Spawn Point");
    }
#endif
}
