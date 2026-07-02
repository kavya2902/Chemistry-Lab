using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pipette interaction controller — handles filling from the starch beaker,
/// dispensing drops into the conical flask, and triggering the flask color change.
///
/// FULL FLOW:
///   1. Grab pipette → dip tip into starch beaker → fills (blue liquid visible)
///   2. Hold tip above conical flask opening → drops fall automatically
///   3. After starchDropThreshold drops → flask turns deep blue (starch + iodine)
///   4. Poke burette nozzle → Na₂S₂O₃ drips → flask fades to colorless
///
/// SCENE SETUP CHECKLIST:
///   [x] This script on the Pipette root GameObject
///   [x] Rigidbody (Interpolate=Interpolate, CollisionDetection=ContinuousDynamic)
///   [x] Oculus.Interaction.Grabbable  OR  XR Grab Interactable
///   [x] pipetteTip → empty child GO placed at the glass tip opening (bottom point)
///   [x] pipetteLiquidRenderer → MeshRenderer of liquid cylinder inside the pipette
///   [x] Starch beaker liquid trigger → Collider (IsTrigger=true), Tag = "StarchBeaker"
///   [x] flaskOpening → empty child GO on the flask at the top of its opening
///   [x] dropPrefab → same sphere prefab used by BuretteDripSpawner
///   [x] titrationController + flaskLiquid references assigned
/// </summary>
public class PipetteController : MonoBehaviour
{
    // ── Transforms ─────────────────────────────────────────────────────────────
    [Header("Pipette Transforms")]
    [Tooltip("Empty child GameObject placed at the very tip of the glass (where drops come out).")]
    public Transform pipetteTip;

    // ── Liquid visual ──────────────────────────────────────────────────────────
    [Header("Liquid Visual (optional)")]
    [Tooltip("MeshRenderer of the liquid column inside the pipette glass — hidden when empty.")]
    public MeshRenderer pipetteLiquidRenderer;
    [Tooltip("Color of the starch indicator solution.")]
    public Color starchColor = new Color(0.10f, 0.22f, 0.85f, 0.80f);

    // ── Fill detection ─────────────────────────────────────────────────────────
    [Header("Fill Detection")]
    [Tooltip("Tag on the starch beaker liquid trigger collider.")]
    public string starchBeakerTag = "StarchBeaker";
    [Tooltip("Physics.OverlapBox fallback fill detection — fixes cases where triggers miss in VR.")]
    public bool useFillOverlapFallback = true;
    [Tooltip("Half-extents of tip fill-detection box in world-metres.")]
    public Vector3 fillOverlapHalfExtents = new Vector3(0.015f, 0.03f, 0.015f);

    // ── Dispense detection ─────────────────────────────────────────────────────
    [Header("Dispense Detection")]
    [Tooltip("Transform at the top opening of the conical flask — drops dispense when tip is above this.")]
    public Transform flaskOpening;
    [Tooltip("Horizontal radius (metres) within which the tip must be over the flask to dispense.")]
    public float dispenseLateralRadius = 0.07f;
    [Tooltip("Tip must be within this many metres above the flask opening to dispense.")]
    public float dispenseVerticalRange = 0.15f;

    // ── Drop settings ──────────────────────────────────────────────────────────
    [Header("Drop Settings")]
    [Tooltip("Sphere prefab used by BuretteDripSpawner works perfectly here.")]
    public GameObject dropPrefab;
    [Range(0.05f, 1f)]
    [Tooltip("Seconds between consecutive drops while dispensing.")]
    public float dropInterval = 0.20f;
    [Tooltip("Initial downward speed of each spawned drop (m/s).")]
    public float dropInitialSpeed = 1.2f;
    [Tooltip("Diameter of each drop in world units.")]
    public float dropScale = 0.008f;
    [Tooltip("Seconds before an unabsorbed drop auto-destroys.")]
    public float dropLifetime = 4f;

    // ── Starch threshold ───────────────────────────────────────────────────────
    [Header("Starch Threshold")]
    [Tooltip("Number of drops to dispense before the flask turns deep blue.")]
    public int starchDropThreshold = 6;
    [Tooltip("Total number of drops the full pipette holds.")]
    public int totalDropCapacity = 20;

    // ── Experiment references ─────────────────────────────────────────────────
    [Header("Experiment References")]
    public IodineTitrationController titrationController;
    public ConicalFlaskLiquid flaskLiquid;

    // ── Internal state ─────────────────────────────────────────────────────────
    private bool  _filled;
    private bool  _inBeaker;
    private int   _dropsDispensed;
    private bool  _starchPoured;
    private float _dropTimer;

    private Material _liquidMat;
    private float    _liquidMaxScaleY;

    private readonly List<GameObject> _activeDrops = new List<GameObject>();

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        // ── Rigidbody: ensure one exists on this object for VR grabbing ──────────
        // Only destroy a local Rigidbody if it's truly a NESTED duplicate (this object's
        // direct parent also carries one). Do NOT use GetComponentInParent which walks all
        // the way up and can inadvertently match rigs/scene roots.
        var localRb = GetComponent<Rigidbody>();
        if (localRb != null && transform.parent != null)
        {
            var parentRb = transform.parent.GetComponent<Rigidbody>();
            if (parentRb != null)
            {
                Debug.Log("[PipetteController] Destroying child Rigidbody — direct parent already owns one.");
                Destroy(localRb);
                localRb = null;
            }
        }

        // If there is still no Rigidbody (never had one, or just destroyed), add one so
        // VR grab interactors can pick up the object.
        if (localRb == null && GetComponent<Rigidbody>() == null)
        {
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Debug.Log("[PipetteController] Added Rigidbody automatically for VR grabbing.");
        }

        // ── Collider: resize only truly oversized auto-generated colliders ────────
        // Keep isTrigger as-is — do NOT force it to false, or OnTriggerEnter
        // (used for beaker fill detection) may stop firing.
        var localBox = GetComponent<BoxCollider>();
        if (localBox != null && (localBox.size.x > 1f || localBox.size.y > 1f || localBox.size.z > 1f))
        {
            localBox.size   = new Vector3(0.04f, 0.22f, 0.04f);
            localBox.center = Vector3.zero;
            Debug.Log("[PipetteController] Resized oversized BoxCollider to pipette grab dimensions.");
        }

        // Auto-detect pipetteTip by searching named children first, then fallback to self
        if (pipetteTip == null)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child == transform) continue;
                string n = child.name.ToLower();
                if (n.Contains("tip") || n.Contains("nozzle") || n.Contains("point") || n.Contains("end"))
                {
                    pipetteTip = child;
                    Debug.Log($"[PipetteController] Auto-detected pipetteTip: '{child.name}'");
                    break;
                }
            }
        }
        if (pipetteTip == null)
        {
            pipetteTip = transform;
            Debug.LogWarning("[PipetteController] pipetteTip not assigned — using this object's " +
                             "transform as fallback. For accurate detection, create an empty child " +
                             "GO at the glass tip and assign it.", this);
        }

        // Auto-detect liquid renderer: search children for a MeshRenderer whose name contains
        // "liquid", "water", "fluid", or "fill" (case-insensitive)
        if (pipetteLiquidRenderer == null)
        {
            foreach (var mr in GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = mr.gameObject.name.ToLower();
                if (n.Contains("liquid") || n.Contains("water") ||
                    n.Contains("fluid")  || n.Contains("fill")  || n.Contains("cylinder"))
                {
                    pipetteLiquidRenderer = mr;
                    Debug.Log($"[PipetteController] Auto-detected liquid renderer: '{mr.gameObject.name}'");
                    break;
                }
            }
            if (pipetteLiquidRenderer == null)
                Debug.LogWarning("[PipetteController] pipetteLiquidRenderer not assigned and could not " +
                                 "be auto-detected. Assign the cylinder MeshRenderer in the Inspector.", this);
        }

        if (dropPrefab == null)
            Debug.LogWarning("[PipetteController] dropPrefab is not assigned — no drops will spawn.", this);

        // Auto-find flaskOpening if not assigned: search titrationController.conicalFlask children,
        // then FlaskLiquidTrigger, then any object named "FlaskOpening" / "FlaskMouth" in the scene.
        if (flaskOpening == null)
        {
            // 1. Try to find a child of the conical flask named "Opening" / "Mouth" / "Spout"
            if (titrationController != null && titrationController.conicalFlask != null)
            {
                foreach (Transform child in titrationController.conicalFlask.GetComponentsInChildren<Transform>(true))
                {
                    string n = child.name.ToLower();
                    if (n.Contains("opening") || n.Contains("mouth") || n.Contains("spout") || n.Contains("top"))
                    {
                        flaskOpening = child;
                        Debug.Log($"[PipetteController] Auto-detected flaskOpening from conicalFlask: '{child.name}'");
                        break;
                    }
                }
            }

            // 2. Fall back to FlaskLiquidTrigger transform
            if (flaskOpening == null)
            {
                var trigger = FindFirstObjectByType<FlaskLiquidTrigger>();
                if (trigger != null)
                {
                    flaskOpening = trigger.transform;
                    Debug.Log($"[PipetteController] Auto-detected flaskOpening from FlaskLiquidTrigger: '{trigger.gameObject.name}'");
                }
            }

            // 3. Scene-wide name search
            if (flaskOpening == null)
            {
                var allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
                foreach (var t in allTransforms)
                {
                    string n = t.name.ToLower();
                    if ((n.Contains("flask") || n.Contains("conical")) &&
                        (n.Contains("opening") || n.Contains("mouth") || n.Contains("top") || n.Contains("spout")))
                    {
                        flaskOpening = t;
                        Debug.Log($"[PipetteController] Auto-detected flaskOpening by name search: '{t.name}'");
                        break;
                    }
                }
            }

            if (flaskOpening == null)
                Debug.LogWarning("[PipetteController] flaskOpening could not be auto-detected. " +
                                 "Dispensing will use overlap-sphere fallback. " +
                                 "For accurate detection, create an empty child Transform at the flask top " +
                                 "and assign it to flaskOpening.", this);
        }

        // Instance the liquid material so we can modify color independently
        if (pipetteLiquidRenderer != null)
        {
            _liquidMat = new Material(pipetteLiquidRenderer.sharedMaterial);
            ForceTransparentMode(_liquidMat);
            _liquidMat.color = starchColor;
            if (_liquidMat.HasProperty("_BaseColor")) _liquidMat.SetColor("_BaseColor", starchColor);
            if (_liquidMat.HasProperty("_Color"))     _liquidMat.SetColor("_Color",     starchColor);
            pipetteLiquidRenderer.material = _liquidMat;
            _liquidMaxScaleY = pipetteLiquidRenderer.transform.localScale.y;
            pipetteLiquidRenderer.enabled = false; // hidden until filled
        }
    }

    private void Update()
    {
        // Overlap fallback fill detection
        if (!_filled && useFillOverlapFallback)
            CheckFillOverlap();

        // Dispense loop
        if (!_filled || _inBeaker) return;

        if (IsTipOverFlask())
        {
            _dropTimer += Time.deltaTime;
            if (_dropTimer >= dropInterval && _dropsDispensed < totalDropCapacity)
            {
                _dropTimer = 0f;
                SpawnDrop();
                _dropsDispensed++;
                UpdateLiquidVisual();

                if (!_starchPoured && _dropsDispensed >= starchDropThreshold)
                {
                    _starchPoured = true;
                    NotifyStarchPoured();
                }

                if (_dropsDispensed >= totalDropCapacity)
                    Deplete();
            }
        }
        else
        {
            _dropTimer = 0f;
        }
    }

    // ── Trigger detection ──────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (!IsStarchBeakerCollider(other)) return;
        _inBeaker = true;
        if (!_filled) Fill();
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsStarchBeakerCollider(other))
            _inBeaker = false;
    }

    // ── Fill ───────────────────────────────────────────────────────────────────

    private void CheckFillOverlap()
    {
        // Use a larger box than the Inspector default to ensure the tip hits the liquid volume
        Vector3 halfEx = new Vector3(
            Mathf.Max(fillOverlapHalfExtents.x, 0.04f),
            Mathf.Max(fillOverlapHalfExtents.y, 0.06f),
            Mathf.Max(fillOverlapHalfExtents.z, 0.04f));

        Collider[] hits = Physics.OverlapBox(
            pipetteTip.position, halfEx, pipetteTip.rotation,
            ~0, QueryTriggerInteraction.Collide);

        foreach (Collider hit in hits)
        {
            if (IsOwnCollider(hit)) continue;
            if (IsStarchBeakerCollider(hit))
            {
                _inBeaker = true;
                Fill();
                return;
            }
        }
    }

    private bool IsOwnCollider(Collider col)
    {
        // Exclude colliders that belong to this GameObject or any of its children
        Transform t = col.transform;
        while (t != null)
        {
            if (t == transform) return true;
            t = t.parent;
        }
        return false;
    }

    private bool IsStarchBeakerCollider(Collider col)
    {
        // Primary: tag check (set Tag = "StarchBeaker" on the beaker liquid trigger)
        if (!string.IsNullOrEmpty(starchBeakerTag) && col.CompareTag(starchBeakerTag))
            return true;

        // Secondary: the starch beaker has a BeakerLiquid component — check the hierarchy
        if (col.GetComponentInParent<BeakerLiquid>() != null)
            return true;

        // Tertiary: name-based detection (expanded to catch more naming conventions)
        string n = col.gameObject.name.ToLower();
        if (n.Contains("starch")) return true;
        if (n.Contains("beakerliquid")) return true;
        if (n.Contains("beaker") && (n.Contains("liquid") || n.Contains("trigger") ||
                                     n.Contains("water")  || n.Contains("solution") ||
                                     n.Contains("fill")   || n.Contains("volume")))
            return true;

        // Quaternary: also check parent/ancestor names so a plain "Beaker" parent with
        // a "Liquid" child trigger is detected even without a BeakerLiquid script.
        Transform t = col.transform.parent;
        while (t != null)
        {
            string pn = t.name.ToLower();
            if (pn.Contains("beaker") || pn.Contains("starch"))
            {
                // Only accept if this ancestor looks like a liquid container
                if (col.isTrigger) return true;
            }
            t = t.parent;
        }

        return false;
    }

    private void Fill()
    {
        if (_filled) return;
        _filled         = true;
        _dropsDispensed = 0;
        _starchPoured   = false;
        _dropTimer      = 0f;

        if (pipetteLiquidRenderer != null)
        {
            pipetteLiquidRenderer.enabled = true;
            SetLiquidFraction(1f);
        }

        Debug.Log("[PipetteController] Filled with starch indicator.");
    }

    // ── Dispense ───────────────────────────────────────────────────────────────

    private bool IsTipOverFlask()
    {
        if (pipetteTip == null) return false;

        // Primary: check against assigned flask opening Transform
        if (flaskOpening != null)
        {
            float dx    = pipetteTip.position.x - flaskOpening.position.x;
            float dz    = pipetteTip.position.z - flaskOpening.position.z;
            float hDist = Mathf.Sqrt(dx * dx + dz * dz);
            float yAbove = pipetteTip.position.y - flaskOpening.position.y;

            return hDist <= dispenseLateralRadius
                && yAbove >= -0.02f
                && yAbove <= dispenseVerticalRange;
        }

        // Fallback: overlap sphere looking for flask trigger
        Collider[] hits = Physics.OverlapSphere(
            pipetteTip.position, dispenseLateralRadius, ~0,
            QueryTriggerInteraction.Collide);

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("FlaskLiquid"))    return true;
            if (hit.name.Contains("FlaskTrigger")) return true;
            if (hit.name.ToLower().Contains("flask")) return true;
        }

        return false;
    }

    private void SpawnDrop()
    {
        if (dropPrefab == null || pipetteTip == null) return;

        GameObject drop = Instantiate(dropPrefab, pipetteTip.position, Quaternion.identity);
        drop.transform.localScale = Vector3.one * dropScale;

        // Physics — identical setup to BuretteDripSpawner
        Rigidbody rb = drop.GetComponent<Rigidbody>() ?? drop.AddComponent<Rigidbody>();
        rb.useGravity     = true;
        rb.linearDamping  = 0.5f;
        rb.angularDamping = 5f;
        rb.mass           = 0.0005f;
        rb.linearVelocity = Vector3.down * dropInitialSpeed;

        // Color drop blue (starch indicator)
        Renderer rend = drop.GetComponent<Renderer>();
        if (rend != null)
        {
            Material m = new Material(rend.sharedMaterial);
            ForceTransparentMode(m);
            m.color = starchColor;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", starchColor);
            if (m.HasProperty("_Color"))     m.SetColor("_Color",     starchColor);
            rend.material = m;
        }

        // NOTE: intentionally NO DripDropBehavior so FlaskLiquidTrigger does NOT count
        // these as Na₂S₂O₃ drops — pipette drops are starch indicator, not titrant.
        Destroy(drop, dropLifetime);
        _activeDrops.Add(drop);
    }

    private void UpdateLiquidVisual()
    {
        if (pipetteLiquidRenderer == null) return;
        float fraction = 1f - (float)_dropsDispensed / Mathf.Max(1, totalDropCapacity);
        SetLiquidFraction(Mathf.Clamp01(fraction));
    }

    private void SetLiquidFraction(float fraction)
    {
        Vector3 s = pipetteLiquidRenderer.transform.localScale;
        s.y = Mathf.Max(0.0001f, _liquidMaxScaleY * fraction);
        pipetteLiquidRenderer.transform.localScale = s;
    }

    // ── Starch pour notification ───────────────────────────────────────────────

    private void NotifyStarchPoured()
    {
        // IodineTitrationController: advances SetupVisible → StarchAdded,
        //   sets flask renderer to deepBlueColor directly.
        if (titrationController != null)
            titrationController.OnStarchPoured();

        // ConicalFlaskLiquid: transitions color target to deepBlue, raises liquid level.
        if (flaskLiquid != null)
            flaskLiquid.SetStarchAdded();

        Debug.Log($"[PipetteController] {starchDropThreshold} starch drops dispensed — " +
                  "flask turned deep blue. Poke the burette nozzle to start titration.");
    }

    // ── Deplete ────────────────────────────────────────────────────────────────

    private void Deplete()
    {
        _filled = false;

        if (pipetteLiquidRenderer != null)
            pipetteLiquidRenderer.enabled = false;

        Debug.Log("[PipetteController] Pipette fully emptied.");
    }

    // ── Public reset ───────────────────────────────────────────────────────────

    public void ResetPipette()
    {
        _filled         = false;
        _inBeaker       = false;
        _dropsDispensed = 0;
        _starchPoured   = false;
        _dropTimer      = 0f;

        foreach (var d in _activeDrops)
            if (d != null) Destroy(d);
        _activeDrops.Clear();

        if (pipetteLiquidRenderer != null)
        {
            pipetteLiquidRenderer.enabled = false;
            SetLiquidFraction(1f); // restore scale for next fill
        }

        Debug.Log("[PipetteController] Pipette reset.");
    }

    // ── Shared material helper ─────────────────────────────────────────────────

    private static void ForceTransparentMode(Material mat)
    {
        if (mat.HasProperty("_Surface"))          // URP Lit
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        else if (mat.HasProperty("_Mode"))        // Legacy Standard — Mode 3 = Transparent (premultiplied alpha)
        {
            mat.SetFloat("_Mode", 3f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetOverrideTag("RenderType", "Transparent");
        }
    }

    // ── Editor gizmos ──────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Pipette tip
        if (pipetteTip != null)
        {
            Gizmos.color = _filled ? new Color(0.2f, 0.4f, 1f, 1f) : Color.grey;
            Gizmos.DrawWireSphere(pipetteTip.position, 0.012f);
            UnityEditor.Handles.Label(pipetteTip.position + Vector3.right * 0.015f,
                _filled ? $"TIP  [{_dropsDispensed}/{totalDropCapacity}]" : "TIP  (empty)");
        }

        // Flask opening dispense zone
        if (flaskOpening != null)
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.35f);
            Gizmos.DrawWireSphere(flaskOpening.position, dispenseLateralRadius);
            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.15f);
            // Draw a cylinder representing the valid dispense zone
            Gizmos.DrawWireCube(
                flaskOpening.position + Vector3.up * (dispenseVerticalRange * 0.5f),
                new Vector3(dispenseLateralRadius * 2f, dispenseVerticalRange, dispenseLateralRadius * 2f));
            UnityEditor.Handles.Label(flaskOpening.position + Vector3.up * 0.02f, "Flask Opening");
        }
    }
#endif
}
