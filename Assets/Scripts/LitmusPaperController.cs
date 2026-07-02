using System.Collections;
using TMPro;
using UnityEngine;

/// Attach to the ROOT of each litmus paper (BlueLitmusPaper / PinkLitmusPaper).
///
/// ── REQUIRED HIERARCHY ──────────────────────────────────────────────────────
///   BlueLitmusPaper  (this script + Rigidbody + XR Grab Interactable)
///     TopPart        Renderer + BoxCollider  Is Trigger = FALSE
///     BottomPart     Renderer + BoxCollider  Is Trigger = TRUE
///
///   PinkLitmusPaper  (same structure, litmusType = Pink)
///
/// ── BEAKER LIQUID TRIGGER SETUP ─────────────────────────────────────────────
///   For each beaker, add a child GameObject inside the liquid volume:
///     Tag              = "Beaker1" / "Beaker2" / "Beaker3"
///     BoxCollider      Is Trigger = true  (sized to the liquid surface)
///     BeakerSolutionType component  →  set solutionType = Acid / Base / Neutral
///
/// ── TAGS REQUIRED IN PROJECT SETTINGS ───────────────────────────────────────
///   Beaker1, Beaker2, Beaker3, BlueLitmusPaper, PinkLitmusPaper
///
/// ── CHEMISTRY RULES ─────────────────────────────────────────────────────────
///   Blue  + Acid    → BottomPart turns Red/Pink    paper locks (ResetPaper() to reuse)
///   Pink  + Base    → BottomPart turns Blue         paper locks
///   Blue  + Base    → no colour change              paper NOT locked
///   Pink  + Acid    → no colour change              paper NOT locked
///   Any   + Neutral → no colour change              paper NOT locked
[RequireComponent(typeof(Rigidbody))]
public class LitmusPaperController : MonoBehaviour
{
    public enum LitmusType { Blue, Pink }

    [Header("Litmus Type")]
    public LitmusType litmusType = LitmusType.Blue;

    // ── Paper Part References ────────────────────────────────────────────────
    [Header("Paper Parts (assign in Inspector)")]
    [Tooltip("TopPart Transform — stays original colour, used as alignment reference.")]
    public Transform topPart;
    [Tooltip("BottomPart Transform — has IsTrigger=true BoxCollider, colour changes here.")]
    public Transform bottomPart;

    [Header("Auto-Align on Awake")]
    [Tooltip("Snap BottomPart flush below TopPart with no gap (assumes paper stands upright).")]
    public bool autoAlignParts = true;

    // ── Materials for BottomPart ─────────────────────────────────────────────
    [Header("BottomPart Materials")]
    [Tooltip("Original material (auto-read from BottomPart renderer if left empty).")]
    public Material originalMaterial;
    [Tooltip("Applied when Blue paper dips into Acid — red/pink colour.")]
    public Material changedRedMaterial;
    [Tooltip("Applied when Pink paper dips into Base — blue colour.")]
    public Material changedBlueMaterial;

    // ── Reaction Timing ──────────────────────────────────────────────────────
    [Header("Reaction Timing")]
    [Range(0.3f, 5f)]
    public float transitionDuration = 1.5f;
    [Range(0f, 3f)]
    [Tooltip("Paper absorbs liquid for this many seconds before colour visibly changes.")]
    public float reactionDelay = 0.5f;

    // ── Result Display ───────────────────────────────────────────────────────
    [Header("Result Display")]
    [Tooltip("World-space Canvas panel (LitmusResultDisplayUI). Optional — floating " +
             "label used automatically when null.")]
    public LitmusResultDisplayUI resultDisplayUI;
    [Range(1f, 8f)]
    public float textDisplayDuration = 3.5f;

    // ── Narration ────────────────────────────────────────────────────────────
    [Header("Narration")]
    [Tooltip("Shared LitmusNarrationManager in the scene. Leave null to disable audio narration.")]
    public LitmusNarrationManager narrationManager;

    // ── Overlap Fallback ─────────────────────────────────────────────────────
    [Header("Overlap Fallback (VR-recommended)")]
    [Tooltip("Checks BottomPart position every frame — more reliable than triggers in VR.")]
    public bool useOverlapFallback    = true;
    public Vector3 overlapHalfExtents = new Vector3(0.022f, 0.008f, 0.042f);
    public LayerMask liquidLayerMask  = ~0;

    // ── Runtime State ────────────────────────────────────────────────────────
    private Renderer  _bottomRenderer;
    private Material  _bottomMat;        // instance material on BottomPart
    private bool      _transitioning;
    private bool      _reacted;          // true after first valid colour change
    private Coroutine _reactionRoutine;
    private string    _liquidContact;    // tag of beaker currently in contact

    // Floating label (used when resultDisplayUI is not assigned)
    private TextMeshPro _floatingLabel;
    private Coroutine   _labelRoutine;

    // All colliders that belong to this paper (self-filtered in OverlapBox)
    private Collider[] _ownColliders;

    private static readonly Color ColAcid     = new Color(1.00f, 0.38f, 0.28f);
    private static readonly Color ColBase     = new Color(0.38f, 0.65f, 1.00f);
    private static readonly Color ColNeutral  = new Color(0.40f, 1.00f, 0.60f);
    private static readonly Color ColNoChange = new Color(0.80f, 0.80f, 0.80f);

    // ── Awake ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        SetupRigidbody();
        ValidateReferences();
        InitBottomMaterial();
        CacheOwnColliders();
        if (autoAlignParts) AlignParts();
        BuildFloatingLabel();
    }

    private void SetupRigidbody()
    {
        var rb = GetComponent<Rigidbody>();
        if (rb == null) return;

        // Papers must NOT fall under gravity — they rest on the bench until grabbed.
        // The XR grab system takes over physics when the user picks them up.
        rb.useGravity              = false;
        rb.linearDamping           = 8f;   // stops quickly after being released mid-air
        rb.angularDamping          = 8f;
        rb.interpolation           = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode  = CollisionDetectionMode.ContinuousDynamic;
    }

    /// <summary>Caches every collider on this GameObject and its children so
    /// OverlapBox results can be filtered without repeated GetComponentsInChildren calls.</summary>
    private void CacheOwnColliders()
    {
        _ownColliders = GetComponentsInChildren<Collider>(true);
    }

    private void ValidateReferences()
    {
        if (bottomPart == null)
            Debug.LogError($"[LitmusPaperController] '{name}': BottomPart not assigned.");
        if (changedRedMaterial  == null)
            Debug.LogError($"[LitmusPaperController] '{name}': changedRedMaterial not assigned.");
        if (changedBlueMaterial == null)
            Debug.LogError($"[LitmusPaperController] '{name}': changedBlueMaterial not assigned.");

        // Warn if grab component is missing
        bool hasGrab = false;
        foreach (var c in GetComponents<MonoBehaviour>())
            if (c != null && (c.GetType().Name == "Grabbable" ||
                              c.GetType().Name == "XRGrabInteractable")) { hasGrab = true; break; }
        if (!hasGrab)
            Debug.LogWarning($"[LitmusPaperController] '{name}': no Grab component found (add Oculus Grabbable or XR Grab Interactable).");
    }

    private void InitBottomMaterial()
    {
        if (bottomPart == null) return;

        _bottomRenderer = bottomPart.GetComponent<Renderer>()
                       ?? bottomPart.GetComponentInChildren<Renderer>();
        if (_bottomRenderer == null)
        {
            Debug.LogError($"[LitmusPaperController] '{name}': No Renderer found on BottomPart.");
            return;
        }

        if (originalMaterial == null)
            originalMaterial = _bottomRenderer.sharedMaterial;

        // Create an instance so we can change colour without affecting the shared asset.
        _bottomMat = new Material(originalMaterial);
        _bottomRenderer.material = _bottomMat;
    }

    // ── Part Alignment ───────────────────────────────────────────────────────

    private void AlignParts()
    {
        if (topPart == null || bottomPart == null) return;

        var topRend = topPart.GetComponent<Renderer>()    ?? topPart.GetComponentInChildren<Renderer>();
        var botRend = bottomPart.GetComponent<Renderer>() ?? bottomPart.GetComponentInChildren<Renderer>();
        if (topRend == null || botRend == null) return;

        // World-space bottom edge of TopPart
        float topBottomY = topRend.bounds.center.y - topRend.bounds.extents.y;
        // Half height of BottomPart
        float botHalfH   = botRend.bounds.extents.y;

        // Place BottomPart centre exactly at TopPart's bottom edge
        Vector3 newPos = new Vector3(
            topRend.bounds.center.x,
            topBottomY - botHalfH,
            topRend.bounds.center.z);
        bottomPart.position = newPos;
    }

    // ── Floating Label ───────────────────────────────────────────────────────

    private void BuildFloatingLabel()
    {
        var go = new GameObject("DetectionLabel");
        go.transform.SetParent(transform);
        go.transform.localPosition = new Vector3(0f, 0.15f, 0f);
        go.transform.localScale    = Vector3.one * 0.003f;

        _floatingLabel                        = go.AddComponent<TextMeshPro>();
        _floatingLabel.fontSize               = 14f;
        _floatingLabel.fontStyle              = FontStyles.Bold;
        _floatingLabel.alignment              = TextAlignmentOptions.Center;
        _floatingLabel.enableWordWrapping     = false;
        _floatingLabel.color                  = new Color(1f, 1f, 1f, 0f); // invisible at start
        _floatingLabel.rectTransform.sizeDelta = new Vector2(500f, 80f);
    }

    private void LateUpdate()
    {
        // Billboard the floating label toward the player every frame.
        if (_floatingLabel == null || _floatingLabel.color.a <= 0f) return;
        var cam = Camera.main;
        if (cam == null) return;
        _floatingLabel.transform.LookAt(cam.transform.position);
        _floatingLabel.transform.Rotate(0f, 180f, 0f);
    }

    // ── Overlap Fallback (VR-safe Detection) ─────────────────────────────────

    private void Update()
    {
        // Skip while a colour transition is in progress or the paper is locked.
        if (!useOverlapFallback || _transitioning || _reacted || bottomPart == null) return;

        var hits = Physics.OverlapBox(
            bottomPart.position, overlapHalfExtents, bottomPart.rotation,
            liquidLayerMask, QueryTriggerInteraction.Collide);

        string found = null;
        foreach (var hit in hits)
        {
            if (IsOwnCollider(hit)) continue;
            if (IsBeakerTag(hit.tag)) { found = hit.tag; break; }
        }

        if (found == _liquidContact) return; // no change
        _liquidContact = found;
        if (found != null)
            ProcessDip(found, FindSolutionType(found, null));
    }

    // ── Trigger Detection ─────────────────────────────────────────────────────
    // Unity sends OnTriggerEnter to the Rigidbody's root when a CHILD trigger collider
    // (BottomPart) overlaps another trigger (beaker liquid).

    private void OnTriggerEnter(Collider other)
    {
        if (_transitioning) return;
        if (!IsBeakerTag(other.tag)) return;
        // Sync _liquidContact NOW so the overlap-fallback in Update() sees it on
        // the very next frame and skips — prevents the double-fire that would
        // cancel the pick_pink / completed follow-up sequence.
        _liquidContact = other.tag;
        ProcessDip(other.tag, FindSolutionType(other.tag, other));
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsBeakerTag(other.tag)) _liquidContact = null;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool IsBeakerTag(string t) =>
        t == "Beaker1" || t == "Beaker2" || t == "Beaker3";

    /// <summary>Returns true if the collider belongs to this paper's own hierarchy.</summary>
    private bool IsOwnCollider(Collider col)
    {
        if (_ownColliders == null) return false;
        foreach (var c in _ownColliders)
            if (c == col) return true;
        return false;
    }

    private BeakerSolutionType.SolutionType FindSolutionType(string beakerTag, Collider col)
    {
        // Check directly on the collider or its parent hierarchy.
        if (col != null)
        {
            var bst = col.GetComponent<BeakerSolutionType>()
                   ?? col.GetComponentInParent<BeakerSolutionType>();
            if (bst != null) return bst.solutionType;
        }

        // Fallback: search all BeakerSolutionType components in scene.
        foreach (var b in FindObjectsByType<BeakerSolutionType>(FindObjectsSortMode.None))
            if (b.CompareTag(beakerTag)) return b.solutionType;

        Debug.LogWarning($"[LitmusPaperController] No BeakerSolutionType found for '{beakerTag}'. " +
                         "Add the component to the beaker's liquid trigger. Defaulting to Neutral.");
        return BeakerSolutionType.SolutionType.Neutral;
    }

    // ── Reaction Logic ────────────────────────────────────────────────────────

    private void ProcessDip(string beakerTag, BeakerSolutionType.SolutionType solutionType)
    {
        // Always show result message and fire narration — even if paper already reacted.
        ShowResultMessage(solutionType);
        narrationManager?.OnPaperDipped(litmusType, solutionType);

        // Colour changes only once (paper locks after first valid reaction).
        if (_reacted) return;

        Material target = ResolveTarget(solutionType);
        if (target == null)
        {
            // Wrong combination or neutral — no colour change, paper remains usable.
            Debug.Log($"[LitmusPaperController] '{name}' ({litmusType}) + {solutionType} → no colour change.");
            return;
        }

        Debug.Log($"[LitmusPaperController] '{name}' ({litmusType}) reacting with {solutionType}.");
        if (_reactionRoutine != null) StopCoroutine(_reactionRoutine);
        _reactionRoutine = StartCoroutine(ColourTransition(target));
    }

    private Material ResolveTarget(BeakerSolutionType.SolutionType solutionType)
    {
        switch (litmusType)
        {
            case LitmusType.Blue:
                return solutionType == BeakerSolutionType.SolutionType.Acid
                    ? changedRedMaterial : null;
            case LitmusType.Pink:
                return solutionType == BeakerSolutionType.SolutionType.Base
                    ? changedBlueMaterial : null;
        }
        return null;
    }

    // ── Result Messages ───────────────────────────────────────────────────────

    private void ShowResultMessage(BeakerSolutionType.SolutionType solutionType)
    {
        string msg;
        Color  col;

        // Determine whether this paper/solution combo actually produces a reaction.
        // ResolveTarget returns a non-null material only for reactive pairs:
        //   Blue  + Acid → changedRedMaterial
        //   Pink  + Base → changedBlueMaterial
        bool wouldReact = ResolveTarget(solutionType) != null;

        if (wouldReact)
        {
            // Reactive pair: name the detected substance.
            if (solutionType == BeakerSolutionType.SolutionType.Acid)
            { msg = "Acid Detected"; col = ColAcid; }
            else
            { msg = "Base Detected"; col = ColBase; }
        }
        else if (solutionType == BeakerSolutionType.SolutionType.Neutral)
        {
            // Any paper in neutral — indicator stays unchanged.
            msg = "Neutral Solution"; col = ColNeutral;
        }
        else
        {
            // Wrong combination: Blue+Base or Pink+Acid — no indicator reaction.
            msg = "No Change"; col = ColNoChange;
        }

        if (resultDisplayUI != null)
            resultDisplayUI.ShowResult(msg, col, textDisplayDuration);

        ShowFloatingLabel(msg, col);
    }

    private void ShowFloatingLabel(string msg, Color col)
    {
        if (_floatingLabel == null) return;
        _floatingLabel.text  = msg;
        _floatingLabel.color = col;
        if (_labelRoutine != null) StopCoroutine(_labelRoutine);
        _labelRoutine = StartCoroutine(FadeLabel(col));
    }

    private IEnumerator FadeLabel(Color baseColor)
    {
        float holdTime = textDisplayDuration * 0.65f;
        float fadeTime = textDisplayDuration - holdTime;

        _floatingLabel.color = baseColor;
        yield return new WaitForSeconds(holdTime);

        float elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float a  = Mathf.Lerp(1f, 0f, elapsed / fadeTime);
            _floatingLabel.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
            yield return null;
        }
        _floatingLabel.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
    }

    // ── Colour Transition ─────────────────────────────────────────────────────

    private IEnumerator ColourTransition(Material target)
    {
        _transitioning = true;

        // Realistic pause: paper absorbs the liquid before colour visibly changes.
        if (reactionDelay > 0f)
            yield return new WaitForSeconds(reactionDelay);

        Color startColor  = _bottomMat.color;
        Color targetColor = target.color;
        float elapsed     = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t  = Mathf.Clamp01(elapsed / transitionDuration);
            t = t * t * (3f - 2f * t); // smoothstep easing
            _bottomMat.color = Color.Lerp(startColor, targetColor, t);
            yield return null;
        }

        _bottomMat.color = targetColor; // ensure exact final colour

        _reacted       = true;  // LOCK — no more colour changes until ResetPaper()
        _transitioning = false;
    }

    // ── Reset ─────────────────────────────────────────────────────────────────

    public void ResetPaper()
    {
        if (_reactionRoutine != null) StopCoroutine(_reactionRoutine);
        if (_labelRoutine    != null) StopCoroutine(_labelRoutine);

        if (_bottomMat != null && originalMaterial != null)
            _bottomMat.color = originalMaterial.color;

        if (_floatingLabel != null)
            _floatingLabel.color = new Color(0f, 0f, 0f, 0f);

        _reacted       = false;
        _transitioning = false;

        // Clear contact so the overlap fallback can re-detect when dipped again.
        _liquidContact = null;

        Debug.Log($"[LitmusPaperController] '{name}' reset.");
    }
}
