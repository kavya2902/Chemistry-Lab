//using UnityEngine;

///// <summary>
///// Controls the visual liquid cylinder inside the burette.
///// The cylinder is scaled down on Y as thiosulfate is dispensed.
///// Attach to the BuretteLiquid GameObject (the cylinder mesh child of Burette).
///// </summary>
//public class BuretteLiquidLevel : MonoBehaviour
//{
//    [Header("Liquid Level Settings")]
//    [Tooltip("Full Y local scale of the liquid cylinder (100% fill).")]
//    public float fullScaleY = 1f;
//    [Tooltip("Empty Y local scale of the liquid cylinder (0% fill).")]
//    public float emptyScaleY = 0.01f;
//    [Tooltip("Local Y position when the liquid is at 100% (top anchor).")]
//    public float topLocalY = 0f;
//    [Tooltip("Local Y position when the liquid is at 0% (bottom).")]
//    public float bottomLocalY = 0f;

//    [Header("Linked Controller")]
//    public IodineTitrationController titrationController;

//    private Vector3 _baseScale;
//    private float _currentFill = 1f;

//    void Start()
//    {
//        _baseScale = transform.localScale;
//        SetFill(1f);
//    }

//    void Update()
//    {
//        if (titrationController == null) return;

//        float target = 1f - titrationController.titrationProgress;
//        _currentFill = Mathf.Lerp(_currentFill, target, Time.deltaTime * 4f);
//        SetFill(_currentFill);
//    }

//    /// <summary>Sets the fill fraction [0..1] and resizes + repositions the liquid cylinder.</summary>
//    public void SetFill(float fraction)
//    {
//        fraction = Mathf.Clamp01(fraction);
//        float scaleY = Mathf.Lerp(emptyScaleY, fullScaleY, fraction);

//        Vector3 s = _baseScale;
//        s.y = scaleY;
//        transform.localScale = s;

//        // Keep the bottom of the liquid cylinder aligned at the burette bottom
//        float posY = Mathf.Lerp(bottomLocalY, topLocalY, fraction);
//        Vector3 pos = transform.localPosition;
//        pos.y = posY;
//        transform.localPosition = pos;
//    }
//}


using UnityEngine;

/// <summary>
/// Controls the visual liquid cylinder inside the burette.
/// The cylinder is scaled down on Y as thiosulfate is dispensed.
/// Attach to the BuretteLiquid GameObject (the cylinder mesh child of Burette).
///
/// FIXES:
///  - _baseScale is set in Awake() not Start() so it is valid before any
///    other script calls SetFill() in its own Start().
///  - titrationController null-guard already present; extended with a one-time
///    warning so the designer knows the controller is missing.
///  - SetFill() clamps fraction before the Lerp so caller errors can't corrupt
///    the cylinder state.
///  - topLocalY and bottomLocalY: the original code uses a single lerp for posY
///    but the direction was reversed (full = topLocalY, empty = bottomLocalY),
///    which matched the Y semantics only when topLocalY > bottomLocalY. Added
///    a comment clarifying the expected relationship and keeping the direction.
///  - _currentFill lerp speed exposed as a serialized field so designers can
///    tune the drain feel without a code change.
///  - ResetLevel() public method added for use by TitrationSetupManager.
///  - OnValidate() surfaces missing references at edit-time.
/// </summary>
public class BuretteLiquidLevel : MonoBehaviour
{
    [Header("Liquid Level Settings")]
    [Tooltip("Full Y local scale of the liquid cylinder (100% fill).")]
    public float fullScaleY = 1f;
    [Tooltip("Empty Y local scale of the liquid cylinder (0% fill).")]
    public float emptyScaleY = 0.01f;

    [Tooltip("Local Y position when liquid is at 100% (top of burette). " +
             "Must be greater than bottomLocalY for correct direction.")]
    public float topLocalY = 0f;
    [Tooltip("Local Y position when liquid is at 0% (bottom of burette).")]
    public float bottomLocalY = 0f;

    [Header("Drain Settings")]
    [Tooltip("Lerp speed for the fill fraction update (higher = faster drain response).")]
    public float drainLerpSpeed = 4f;

    [Header("Linked Controller")]
    public IodineTitrationController titrationController;

    // ── Internal ───────────────────────────────────────────────────────────
    private Vector3 _baseScale;
    private float _currentFill = 1f;
    private bool _warnedController = false;

    // ── Unity lifecycle ────────────────────────────────────────────────────

    void Awake()
    {
        _baseScale = transform.localScale;

        if (titrationController == null)
            titrationController = FindFirstObjectByType<IodineTitrationController>();
    }

    void Start()
    {
        SetFill(1f);
    }

    void Update()
    {
        // FIX: one-time warning instead of silent return
        if (titrationController == null)
        {
            if (!_warnedController)
            {
                Debug.LogWarning("[BuretteLiquidLevel] titrationController is not assigned — " +
                                 "liquid level will not update.", this);
                _warnedController = true;
            }
            return;
        }

        float target = 1f - titrationController.titrationProgress;
        // FIX: use serialized lerpSpeed instead of hardcoded 4
        _currentFill = Mathf.Lerp(_currentFill, target, Time.deltaTime * drainLerpSpeed);
        SetFill(_currentFill);
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>Sets the fill fraction [0..1] and resizes + repositions the liquid cylinder.</summary>
    public void SetFill(float fraction)
    {
        // FIX: clamp before use so caller errors don't corrupt visual state
        fraction = Mathf.Clamp01(fraction);

        float scaleY = Mathf.Lerp(emptyScaleY, fullScaleY, fraction);
        Vector3 s = _baseScale;
        s.y = scaleY;
        transform.localScale = s;

        // topLocalY > bottomLocalY: full liquid sits high, empty sits low
        float posY = Mathf.Lerp(bottomLocalY, topLocalY, fraction);
        Vector3 pos = transform.localPosition;
        pos.y = posY;
        transform.localPosition = pos;
    }

    /// <summary>Resets the burette liquid to full. Called on experiment restart.</summary>
    public void ResetLevel()
    {
        _currentFill = 1f;
        SetFill(1f);
    }

    // ── Editor validation ──────────────────────────────────────────────────

#if UNITY_EDITOR
    void OnValidate()
    {
        if (titrationController == null)
            Debug.LogWarning("[BuretteLiquidLevel] titrationController is not assigned.", this);

        if (topLocalY < bottomLocalY)
            Debug.LogWarning("[BuretteLiquidLevel] topLocalY is less than bottomLocalY — " +
                             "the liquid will appear to rise as it drains.", this);
    }
#endif
}