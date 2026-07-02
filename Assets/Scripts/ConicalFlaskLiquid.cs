//using System.Collections;
//using UnityEngine;

///// <summary>
///// Manages the liquid level and color inside the conical flask.
///// - Starts with iodine color (yellow-brown).
///// - Turns deep blue when starch is poured.
///// - Transitions smoothly to colorless as thiosulfate is added (Lerp over time).
///// - Raises the liquid cylinder Y-scale slightly with each drop received.
///// Attach to the ConicalFlask root or TitrationController GameObject.
///// </summary>
//public class ConicalFlaskLiquid : MonoBehaviour
//{
//    [Header("Flask Liquid Renderer")]
//    [Tooltip("The cylinder mesh Renderer representing liquid inside the flask.")]
//    public MeshRenderer liquidRenderer;

//    [Header("Flask Liquid Level")]
//    [Tooltip("Local Y scale at 0% fill (starting fill with iodine).")]
//    public float minScaleY = 0.055f;
//    [Tooltip("Maximum local Y scale after all drops received (100% fill).")]
//    public float maxScaleY = 0.12f;
//    [Tooltip("How many drops correspond to max fill increase.")]
//    public int totalDropsForMaxFill = 60;
//    [Tooltip("Speed at which the scale lerps toward the target.")]
//    public float levelLerpSpeed = 4f;

//    [Header("Flask Colors")]
//    public Color iodineColor    = new Color(0.87f, 0.58f, 0.09f, 0.60f);
//    public Color deepBlueColor  = new Color(0.04f, 0.04f, 0.55f, 0.92f);
//    public Color colorlessColor = new Color(0.88f, 0.94f, 1.00f, 0.12f);

//    [Header("Color Transition")]
//    [Tooltip("How fast the flask color lerps per second.")]
//    public float colorLerpSpeed = 1.5f;

//    [Header("Linked Controller")]
//    public IodineTitrationController titrationController;

//    // ── Internal ───────────────────────────────────────────────────────────
//    private Material _liquidMat;
//    private Color _currentColor;
//    private Color _targetColor;
//    private int _dropsReceived = 0;
//    private float _targetScaleY;
//    private bool _starchAdded = false;
//    private bool _initialized = false;

//    void Start()
//    {
//        Initialize();
//    }

//    void Update()
//    {
//        // Smooth color transition
//        _currentColor = Color.Lerp(_currentColor, _targetColor, Time.deltaTime * colorLerpSpeed);
//        ApplyColor(_currentColor);

//        // Smooth level rise
//        if (liquidRenderer != null)
//        {
//            Vector3 s = liquidRenderer.transform.localScale;
//            s.y = Mathf.Lerp(s.y, _targetScaleY, Time.deltaTime * levelLerpSpeed);
//            liquidRenderer.transform.localScale = s;
//        }

//        // Mirror titration progress from controller (overrides drop-based approach when controller is connected)
//        if (titrationController != null && _starchAdded)
//        {
//            if (titrationController.currentPhase == IodineTitrationController.ExperimentPhase.TitrationInProgress ||
//                titrationController.currentPhase == IodineTitrationController.ExperimentPhase.EndpointReached)
//            {
//                float p = titrationController.titrationProgress;
//                _targetColor = Color.Lerp(deepBlueColor, colorlessColor, p);
//            }
//        }
//    }

//    // ── Public API ─────────────────────────────────────────────────────────

//    /// <summary>Initializes the flask to the starting iodine state.</summary>
//    public void Initialize()
//    {
//        if (_initialized) return;
//        _initialized = true;

//        if (liquidRenderer != null)
//        {
//            // Instance the material so we can modify color without affecting shared asset
//            _liquidMat = new Material(liquidRenderer.sharedMaterial);
//            liquidRenderer.material = _liquidMat;

//            _targetScaleY = minScaleY;
//            Vector3 s = liquidRenderer.transform.localScale;
//            s.y = minScaleY;
//            liquidRenderer.transform.localScale = s;
//        }

//        _currentColor = iodineColor;
//        _targetColor  = iodineColor;
//        ApplyColor(iodineColor);
//    }

//    /// <summary>Called by StarchPourDetector — transitions flask to deep blue.</summary>
//    public void SetStarchAdded()
//    {
//        _starchAdded  = true;
//        _targetColor  = deepBlueColor;
//        // Slight level rise to indicate starch volume added
//        _targetScaleY = Mathf.Min(_targetScaleY + 0.008f, maxScaleY);
//    }

//    /// <summary>Called by BuretteDripSpawner / DripDropBehavior on each drop received.</summary>
//    public void OnDropReceived()
//    {
//        _dropsReceived++;
//        float fillFraction = Mathf.Clamp01((float)_dropsReceived / totalDropsForMaxFill);
//        _targetScaleY = Mathf.Lerp(minScaleY, maxScaleY, fillFraction);
//    }

//    /// <summary>Resets flask back to iodine state for experiment restart.</summary>
//    public void Reset()
//    {
//        _dropsReceived = 0;
//        _starchAdded   = false;
//        _currentColor  = iodineColor;
//        _targetColor   = iodineColor;
//        _targetScaleY  = minScaleY;

//        if (liquidRenderer != null)
//        {
//            Vector3 s = liquidRenderer.transform.localScale;
//            s.y = minScaleY;
//            liquidRenderer.transform.localScale = s;
//        }

//        ApplyColor(iodineColor);
//    }

//    // ── Internal ───────────────────────────────────────────────────────────

//    private void ApplyColor(Color color)
//    {
//        if (_liquidMat == null) return;
//        _liquidMat.color = color;

//        if (_liquidMat.HasProperty("_BaseColor"))
//            _liquidMat.SetColor("_BaseColor", color);
//    }
//}


using System.Collections;
using UnityEngine;

/// <summary>
/// Manages the liquid level and color inside the conical flask.
/// FIXES:
///  - Material transparency / render mode is forced to Transparent at runtime.
///  - _Color and _BaseColor are both set (URP + legacy).
///  - Level rise is clamped so it can never exceed maxScaleY.
///  - Initialize() is idempotent and also called from OnEnable so scene reloads work.
///  - Null-guards on liquidRenderer throughout.
///  - Reset() re-instances the material so a hard reset always starts fresh.
/// </summary>
public class ConicalFlaskLiquid : MonoBehaviour
{
    [Header("Flask Liquid Renderer")]
    [Tooltip("The cylinder MeshRenderer representing liquid inside the flask.")]
    public MeshRenderer liquidRenderer;

    [Header("Flask Liquid Level")]
    public float minScaleY = 0.055f;
    public float maxScaleY = 0.07f;
    public int totalDropsForMaxFill = 60;
    public float levelLerpSpeed = 4f;

    [Header("Flask Colors")]
    public Color iodineColor = new Color(0.87f, 0.58f, 0.09f, 0.60f);
    public Color deepBlueColor = new Color(0.04f, 0.04f, 0.55f, 0.92f);
    public Color colorlessColor = new Color(0.88f, 0.94f, 1.00f, 0.12f);

    [Header("Color Transition")]
    public float colorLerpSpeed = 1.5f;

    [Header("Behavior")]
    [Tooltip("Disable scale animation — required when the liquid cylinder is rotated or IodineTitrationController owns the renderer.")]
    public bool disableScaleAnimation = true;
    [Tooltip("Disable color control — set true when IodineTitrationController directly manages the same MeshRenderer to avoid competing material instances.")]
    public bool disableColorControl = true;

    [Header("Linked Controller")]
    public IodineTitrationController titrationController;

    // ── Internal ───────────────────────────────────────────────────────────
    private Material _liquidMat;
    private Color _currentColor;
    private Color _targetColor;
    private int _dropsReceived;
    private float _targetScaleY;
    private float _liquidBottomLocalY;  // local Y of the cylinder's bottom edge — stays fixed
    private bool _starchAdded;
    private bool _initialized;

    // ── Unity lifecycle ────────────────────────────────────────────────────

    void Start() => Initialize();
    void OnEnable() => Initialize();   // FIX: also init on re-enable (scene reload / experiment restart)

    void Update()
    {
        // Color control — skipped when IodineTitrationController owns the renderer
        if (!disableColorControl)
        {
            _currentColor = Color.Lerp(_currentColor, _targetColor, Time.deltaTime * colorLerpSpeed);
            ApplyColor(_currentColor);

            if (titrationController != null)
            {
                var phase = titrationController.currentPhase;

                if (!_starchAdded &&
                    (phase == IodineTitrationController.ExperimentPhase.StarchAdded      ||
                     phase == IodineTitrationController.ExperimentPhase.TitrationInProgress ||
                     phase == IodineTitrationController.ExperimentPhase.EndpointReached))
                {
                    _starchAdded = true;
                    _targetColor = deepBlueColor;
                }

                if (_starchAdded &&
                    (phase == IodineTitrationController.ExperimentPhase.TitrationInProgress ||
                     phase == IodineTitrationController.ExperimentPhase.EndpointReached))
                {
                    float p = titrationController.titrationProgress;
                    _targetColor = Color.Lerp(deepBlueColor, colorlessColor, p);
                }
            }
        }

        // Scale animation — skipped when the cylinder is rotated (scaling Y would push
        // it sideways through flask walls) or when an external system manages the renderer
        if (!disableScaleAnimation && liquidRenderer != null)
        {
            Vector3 s = liquidRenderer.transform.localScale;
            s.y = Mathf.Lerp(s.y, _targetScaleY, Time.deltaTime * levelLerpSpeed);
            liquidRenderer.transform.localScale = s;

            Vector3 pos = liquidRenderer.transform.localPosition;
            pos.y = _liquidBottomLocalY + s.y;
            liquidRenderer.transform.localPosition = pos;
        }
    }

    // ── Public API ─────────────────────────────────────────────────────────

    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        if (liquidRenderer != null)
        {
            // Only create a material instance when this script controls color.
            // When disableColorControl is true, IodineTitrationController owns the material.
            if (!disableColorControl)
            {
                _liquidMat = new Material(liquidRenderer.sharedMaterial);
                ForceTransparentMode(_liquidMat);
                liquidRenderer.material = _liquidMat;
            }

            if (!disableScaleAnimation)
            {
                _targetScaleY = minScaleY;
                Vector3 s = liquidRenderer.transform.localScale;
                s.y = minScaleY;
                liquidRenderer.transform.localScale = s;
                _liquidBottomLocalY = liquidRenderer.transform.localPosition.y - minScaleY;
            }
        }
        else
        {
            Debug.LogWarning("[ConicalFlaskLiquid] liquidRenderer is not assigned.", this);
        }

        _currentColor = iodineColor;
        _targetColor = iodineColor;
        if (!disableColorControl) ApplyColor(iodineColor);
    }

    /// <summary>Called by StarchPourDetector — transitions flask to deep blue.</summary>
    public void SetStarchAdded()
    {
        _starchAdded = true;
        _targetColor = deepBlueColor;
        // FIX: use Mathf.Min to guarantee we never exceed maxScaleY
        _targetScaleY = Mathf.Min(_targetScaleY + 0.008f, maxScaleY);
    }

    /// <summary>Called by DripDropBehavior / FlaskLiquidTrigger on each drop received.</summary>
    public void OnDropReceived()
    {
        _dropsReceived++;
        float fillFraction = Mathf.Clamp01((float)_dropsReceived / totalDropsForMaxFill);
        // FIX: lerp between min and max, then clamp to be safe
        _targetScaleY = Mathf.Clamp(
            Mathf.Lerp(minScaleY, maxScaleY, fillFraction),
            minScaleY, maxScaleY);
    }

    /// <summary>Resets flask back to iodine state for experiment restart.</summary>
    public void Reset()
    {
        _dropsReceived = 0;
        _starchAdded = false;
        _initialized = false;   // FIX: allow re-initialization so material is freshly instanced

        _currentColor = iodineColor;
        _targetColor = iodineColor;
        _targetScaleY = minScaleY;

        if (liquidRenderer != null)
        {
            Vector3 s = liquidRenderer.transform.localScale;
            s.y = minScaleY;
            liquidRenderer.transform.localScale = s;

            // Restore position to initial fill level
            Vector3 pos = liquidRenderer.transform.localPosition;
            pos.y = _liquidBottomLocalY + minScaleY;
            liquidRenderer.transform.localPosition = pos;
        }

        // Re-instance material cleanly (only when this script controls color)
        if (!disableColorControl && liquidRenderer != null && liquidRenderer.sharedMaterial != null)
        {
            _liquidMat = new Material(liquidRenderer.sharedMaterial);
            ForceTransparentMode(_liquidMat);
            liquidRenderer.material = _liquidMat;
        }

        if (!disableColorControl) ApplyColor(iodineColor);
        _initialized = true;
    }

    // ── Internal ───────────────────────────────────────────────────────────

    private void ApplyColor(Color color)
    {
        if (_liquidMat == null) return;

        // FIX: set both property names — covers URP (_BaseColor) and Legacy/HDRP (_Color)
        _liquidMat.color = color;
        if (_liquidMat.HasProperty("_BaseColor"))
            _liquidMat.SetColor("_BaseColor", color);
        if (_liquidMat.HasProperty("_Color"))
            _liquidMat.SetColor("_Color", color);
    }

    private static void ForceTransparentMode(Material mat)
    {
        // URP Lit
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f);           // 1 = Transparent
            mat.SetFloat("_Blend", 0f);           // Alpha blend
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        // Legacy Standard shader — Mode 3 = Transparent (premultiplied alpha)
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
}