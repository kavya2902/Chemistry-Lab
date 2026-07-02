using UnityEngine;

/// <summary>
/// Controls the visual starch liquid inside the beaker and detects when it is
/// poured into the conical flask.
///
/// FIX SUMMARY (this revision):
///  - pourTiltThreshold lowered to 30° (was 40°) — more natural pouring angle.
///  - maxPourDistance increased to 0.35 m (was 0.2 m) — beaker mouth to flask mouth.
///  - requireGrab defaults to true; set false in Inspector for quick testing.
///  - Grab detection now tries SelectingPointsCount, SelectingInteractorsCount, AND
///    IsGrabbed so it covers multiple Meta SDK versions.
///  - pourPoint / receivePoint null-guards log a one-time warning and fall back
///    gracefully so the script never throws even when transforms are unassigned.
///  - debugLogInterval field: prints grabbed state, tilt angle, distance, and
///    fill amount every N frames so you can diagnose issues live in Play mode.
///  - Gizmos (Editor-only) draw pourPoint sphere and receivePoint sphere + range ring.
///  - pourDrainRate exposed and lowered to 0.45 (smoother visible drain).
///  - CommitPour fires both flaskLiquid.SetStarchAdded() and
///    titrationController.OnStarchPoured() so either listener path works.
/// </summary>
public class BeakerLiquid : MonoBehaviour
{
    [Header("Beaker Liquid Cylinder")]
    [Tooltip("MeshRenderer of the liquid cylinder inside the beaker.")]
    public MeshRenderer liquidRenderer;

    [Header("Pour Stream")]
    [Tooltip("GameObject (cylinder/quad) shown as the pour stream while liquid flows.")]
    public GameObject pourStreamObject;
    [Tooltip("Optional: precise spout position on the beaker rim. Falls back to auto-calculation.")]
    public Transform pourSpout;

    [Header("Level Settings")]
    [Tooltip("Local Y scale when the beaker is full.")]
    public float fullScaleY = 0.06f;
    [Tooltip("Local Y scale when the beaker is empty.")]
    public float emptyScaleY = 0.005f;
    [Tooltip("Local Y position when the beaker is full.")]
    public float fullPosY = 0f;
    [Tooltip("Local Y position when the beaker is empty.")]
    public float emptyPosY = -0.03f;

    [Header("Pour Settings")]
    [Tooltip("Degrees from resting orientation required to trigger a pour. 50-60° prevents accidental contact triggers.")]
    public float pourTiltThreshold = 55f;
    [Tooltip("Seconds the beaker must stay tilted before the flask turns blue. 20 s gives a natural 'pouring contact' feel.")]
    [Range(0f, 30f)] public float pourHoldDuration = 20f;
    [Tooltip("Require the beaker to be grabbed in VR before pouring is allowed.\n\n" +
             "HAND TRACKING: Set this to FALSE. Meta hand-tracking grab state is not " +
             "detectable via the Grabbable.SelectingPointsCount property — so with hands, " +
             "this will always read as 'not grabbed' and block the pour entirely.\n\n" +
             "CONTROLLERS: Can be set to true if you want explicit grip confirmation.")]
    public bool requireGrab = false;
    [Tooltip("Fraction of liquid drained per second while actively pouring. 0.45 = empties in ~2 s.")]
    [Range(0f, 1f)] public float pourDrainRate = 0.45f;
    [Tooltip("Max mouth-to-mouth distance (pourPoint → receivePoint) to allow pouring. Recommended: 0.30-0.40 m.")]
    public float maxPourDistance = 0.35f;
    [Tooltip("Pour is committed when fill drops below this fraction (0-1).")]
    [Range(0f, 1f)] public float pourCommitThreshold = 0.5f;

    [Header("Pour Point References")]
    [Tooltip("Empty Transform placed at the beaker lip / rim (the pouring edge). " +
             "Parent it to the Beaker so it moves with the object.")]
    public Transform pourPoint;
    [Tooltip("Empty Transform placed at the flask mouth / opening. " +
             "Parent it to the ConicalFlask so it stays at the top.")]
    public Transform receivePoint;

    [Header("Pour Stream Auto-Positioning (fallback when pourSpout is null)")]
    public float pourSpoutRadius = 0.06f;
    public float pourSpoutHeightOffset = 0.09f;
    public float flaskOpeningHeight = 0.12f;

    [Header("References")]
    public Transform flaskTransform;
    public ConicalFlaskLiquid flaskLiquid;
    public IodineTitrationController titrationController;

    [Header("Debug")]
    [Tooltip("Log grab state, tilt, distance, and fill every N frames. 0 = disabled.")]
    public int debugLogInterval = 0;

    // ── Internal ───────────────────────────────────────────────────────────
    private float _fillFraction = 1f;
    private bool _isPouringVisually;
    private bool _hasPouredToFlask;
    private float _pourHoldTimer;
    private Material _liquidMat;
    private Vector3 _restingUp;

    private Component _grabbable;
    private System.Reflection.PropertyInfo _selectingCountProp;
    private bool _grabbableResolved;
    private int _debugFrameCount;

    private static readonly Color StarchColor = new Color(0.04f, 0.04f, 0.62f, 0.92f);

    // ── Unity lifecycle ────────────────────────────────────────────────────

    void Start()
    {
        _restingUp = transform.up;
        ResolveGrabbable();

        if (liquidRenderer != null)
        {
            _liquidMat = new Material(liquidRenderer.sharedMaterial);
            ForceTransparentMode(_liquidMat);
            liquidRenderer.material = _liquidMat;
            ApplyColor(StarchColor);
        }

        SetPourStream(false);
        ApplyFill(_fillFraction);

        // Detach stream from beaker hierarchy so it stays in world space
        if (pourStreamObject != null)
            pourStreamObject.transform.SetParent(null, true);
    }

    void Update()
    {
        if (_hasPouredToFlask) return;
        if (titrationController == null) return;

        if (titrationController.currentPhase !=
            IodineTitrationController.ExperimentPhase.SetupVisible)
        {
            if (_isPouringVisually) CancelPour();
            return;
        }

        // ── Grab check ────────────────────────────────────────────────────
        bool isGrabbed = requireGrab ? IsBeingGrabbed() : true;

        float tilt = Vector3.Angle(transform.up, _restingUp);
        bool tiltOk = tilt > pourTiltThreshold;

        // ── Distance check ────────────────────────────────────────────────
        // When pourPoint/receivePoint are not assigned we fall back to object
        // centres — add 0.2 m leniency so unassigned references don't silently
        // block the pour when the beaker is physically close to the flask.
        bool usingFallback = pourPoint == null || receivePoint == null;
        Vector3 pourPos = pourPoint   != null ? pourPoint.position   : transform.position;
        Vector3 recvPos = receivePoint!= null ? receivePoint.position
                        : flaskTransform != null ? flaskTransform.position
                        : transform.position;
        float dist          = Vector3.Distance(pourPos, recvPos);
        float effectiveDist = usingFallback ? maxPourDistance + 0.2f : maxPourDistance;
        bool  distOk        = dist < effectiveDist;

        bool conditionsMet = isGrabbed && tiltOk && distOk && _fillFraction > 0f;

        // ── Hold timer — require sustained tilt before drain begins ──────
        if (conditionsMet)
            _pourHoldTimer += Time.deltaTime;
        else
            _pourHoldTimer = 0f;

        bool shouldPour = conditionsMet && _pourHoldTimer >= pourHoldDuration;

        // ── Debug log ─────────────────────────────────────────────────────
        if (debugLogInterval > 0 && ++_debugFrameCount >= debugLogInterval)
        {
            _debugFrameCount = 0;
            Debug.Log(
                $"[BeakerLiquid] Grabbed:{isGrabbed}(requireGrab:{requireGrab}) | " +
                $"Tilt:{tilt:F1}° (need>{pourTiltThreshold}°) OK:{tiltOk} | " +
                $"Dist:{dist:F3}m (need<{effectiveDist:F2}m{(usingFallback ? " fallback" : "")}) OK:{distOk} | " +
                $"HoldTimer:{_pourHoldTimer:F2}/{pourHoldDuration:F2} | Fill:{_fillFraction:F2} | Pouring:{shouldPour}");
        }

        // ── Draw debug line between pour and receive points ────────────────
        if (pourPoint != null && receivePoint != null)
            Debug.DrawLine(pourPos, recvPos, shouldPour ? Color.green : Color.red);

        // ── State machine ─────────────────────────────────────────────────
        if (shouldPour)
        {
            if (!_isPouringVisually)
            {
                _isPouringVisually = true;
                SetPourStream(true);
                Debug.Log($"[BeakerLiquid] Pour STARTED — tilt:{tilt:F1}° dist:{dist:F3}m");
            }

            _fillFraction -= pourDrainRate * Time.deltaTime;
            _fillFraction  = Mathf.Clamp01(_fillFraction);
            ApplyFill(_fillFraction);
            UpdatePourStreamTransform();

            if (_fillFraction < pourCommitThreshold)
                CommitPour();
        }
        else
        {
            if (_isPouringVisually) CancelPour();
        }
    }

    // ── Public API ─────────────────────────────────────────────────────────

    public void ResetBeaker()
    {
        _fillFraction      = 1f;
        _hasPouredToFlask  = false;
        _isPouringVisually = false;
        _pourHoldTimer     = 0f;
        _restingUp         = transform.up;

        SetPourStream(false);

        if (liquidRenderer != null && liquidRenderer.sharedMaterial != null)
        {
            if (_liquidMat != null) Object.Destroy(_liquidMat);
            _liquidMat = new Material(liquidRenderer.sharedMaterial);
            ForceTransparentMode(_liquidMat);
            liquidRenderer.material = _liquidMat;
            ApplyColor(StarchColor);
        }

        ApplyFill(1f);
    }

    // ── Internal ───────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the Oculus Interaction SDK Grabbable component and the property
    /// used to check whether it is currently selected (grabbed).
    /// Tries three property names to cover multiple SDK versions.
    /// </summary>
    private void ResolveGrabbable()
    {
        if (_grabbableResolved) return;
        _grabbableResolved = true;

        var grabbableType = System.Type.GetType(
            "Oculus.Interaction.Grabbable, Oculus.Interaction.Runtime");

        if (grabbableType == null)
        {
            Debug.LogWarning("[BeakerLiquid] Oculus Grabbable type not found in assembly. " +
                             "If requireGrab=true, pouring will never trigger. " +
                             "Set requireGrab=false to bypass.", this);
            return;
        }

        _grabbable = GetComponent(grabbableType) ?? GetComponentInChildren(grabbableType);

        if (_grabbable == null)
        {
            Debug.LogWarning("[BeakerLiquid] No Grabbable component on Beaker or its children. " +
                             "Set requireGrab=false to test without VR grab.", this);
            return;
        }

        // Walk the type hierarchy; try all known property names for SDK compatibility
        string[] candidateNames =
        {
            "SelectingPointsCount",       // Meta Interaction SDK v57+
            "SelectingInteractorsCount",  // older SDK
            "IsGrabbed"                   // some custom SDK wrappers
        };

        var type = _grabbable.GetType();
        while (type != null && _selectingCountProp == null)
        {
            foreach (var name in candidateNames)
            {
                _selectingCountProp = type.GetProperty(
                    name,
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance);
                if (_selectingCountProp != null) break;
            }
            type = type.BaseType;
        }

        if (_selectingCountProp != null)
            Debug.Log($"[BeakerLiquid] Grab property resolved: " +
                      $"{_selectingCountProp.DeclaringType?.Name}.{_selectingCountProp.Name}");
        else
            Debug.LogWarning("[BeakerLiquid] Could not find grab property on Grabbable hierarchy. " +
                             "Set requireGrab=false to bypass.", this);
    }

    private bool IsBeingGrabbed()
    {
        if (_grabbable == null || _selectingCountProp == null) return false;
        try
        {
            var val = _selectingCountProp.GetValue(_grabbable);
            if (val is int  count) return count > 0;
            if (val is bool flag)  return flag;
            return false;
        }
        catch { return false; }
    }

    private void CommitPour()
    {
        _hasPouredToFlask = true;
        SetPourStream(false);
        Debug.Log("[BeakerLiquid] Pour COMMITTED — starch transferred to flask.");

        if (flaskLiquid != null)
            flaskLiquid.SetStarchAdded();

        if (titrationController != null &&
            titrationController.currentPhase ==
            IodineTitrationController.ExperimentPhase.SetupVisible)
        {
            titrationController.OnStarchPoured();
        }
    }

    private void CancelPour()
    {
        _isPouringVisually = false;
        SetPourStream(false);
    }

    private void UpdatePourStreamTransform()
    {
        if (pourStreamObject == null || flaskTransform == null) return;

        Vector3 spoutPos;
        if (pourSpout != null)
        {
            spoutPos = pourSpout.position;
        }
        else
        {
            Vector3 horizontalTilt = new Vector3(transform.up.x, 0f, transform.up.z);
            if (horizontalTilt.sqrMagnitude < 0.001f) return;

            Vector3 pourDir = horizontalTilt.normalized;
            spoutPos = transform.position
                     + pourDir * pourSpoutRadius
                     + Vector3.up * pourSpoutHeightOffset;
        }

        float flaskMouthY   = flaskTransform.position.y + flaskOpeningHeight;
        float streamLength  = spoutPos.y - flaskMouthY;
        if (streamLength < 0.01f) return;

        pourStreamObject.transform.position = new Vector3(
            flaskTransform.position.x,
            spoutPos.y - streamLength * 0.5f,
            flaskTransform.position.z);
        pourStreamObject.transform.rotation = Quaternion.identity;

        Vector3 s = pourStreamObject.transform.localScale;
        s.y = Mathf.Max(0.001f, streamLength * 0.5f);
        pourStreamObject.transform.localScale = s;
    }

    private void ApplyFill(float fraction)
    {
        if (liquidRenderer == null) return;
        fraction = Mathf.Clamp01(fraction);

        Vector3 scale = liquidRenderer.transform.localScale;
        scale.y = Mathf.Lerp(emptyScaleY, fullScaleY, fraction);
        liquidRenderer.transform.localScale = scale;

        Vector3 pos = liquidRenderer.transform.localPosition;
        pos.y = Mathf.Lerp(emptyPosY, fullPosY, fraction);
        liquidRenderer.transform.localPosition = pos;
    }

    private void ApplyColor(Color color)
    {
        if (_liquidMat == null) return;
        _liquidMat.color = color;
        if (_liquidMat.HasProperty("_BaseColor"))
            _liquidMat.SetColor("_BaseColor", color);
        if (_liquidMat.HasProperty("_Color"))
            _liquidMat.SetColor("_Color", color);
    }

    private void SetPourStream(bool active)
    {
        if (pourStreamObject != null)
            pourStreamObject.SetActive(active);
    }

    private static void ForceTransparentMode(Material mat)
    {
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        else if (mat.HasProperty("_Mode"))
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

    void OnDestroy()
    {
        if (_liquidMat != null) Object.Destroy(_liquidMat);
    }

    // ── Editor validation & gizmos ─────────────────────────────────────────

#if UNITY_EDITOR
    void OnValidate()
    {
        if (liquidRenderer == null)
            Debug.LogWarning("[BeakerLiquid] liquidRenderer is not assigned.", this);
        if (flaskTransform == null)
            Debug.LogWarning("[BeakerLiquid] flaskTransform is not assigned.", this);
        if (titrationController == null)
            Debug.LogWarning("[BeakerLiquid] titrationController is not assigned.", this);
        if (flaskLiquid == null)
            Debug.LogWarning("[BeakerLiquid] flaskLiquid is not assigned.", this);
        if (pourPoint == null)
            Debug.LogWarning("[BeakerLiquid] pourPoint not assigned — create an empty child " +
                             "Transform at the beaker lip and assign it here.", this);
        if (receivePoint == null)
            Debug.LogWarning("[BeakerLiquid] receivePoint not assigned — create an empty child " +
                             "Transform at the flask mouth and assign it here.", this);
    }

    void OnDrawGizmosSelected()
    {
        // Cyan sphere = beaker pour point
        if (pourPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(pourPoint.position, 0.015f);
            UnityEditor.Handles.Label(pourPoint.position + Vector3.up * 0.03f, "pourPoint");
        }
        // Yellow sphere + green range ring = flask receive point
        if (receivePoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(receivePoint.position, 0.02f);
            UnityEditor.Handles.Label(receivePoint.position + Vector3.up * 0.03f, "receivePoint");
            Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
            Gizmos.DrawWireSphere(receivePoint.position, maxPourDistance);
        }
    }
#endif
}
