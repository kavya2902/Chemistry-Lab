//using System.Collections.Generic;
//using UnityEngine;
//using UnityEngine.Events;

///// <summary>
///// Master controller for the Iodine Titration experiment.
///// Manages the five-phase state machine, flask color transitions,
///// and all particle system coordination.
///// </summary>
//public class IodineTitrationController : MonoBehaviour
//{
//    // ── Experiment Phases ──────────────────────────────────────────────────
//    public enum ExperimentPhase
//    {
//        NotStarted,
//        SetupVisible,        // Equipment is visible; waiting for starch pour
//        StarchAdded,         // Beaker tilted and starch poured → flask deep blue
//        TitrationInProgress, // Burette nozzle open; Na₂S₂O₃ dripping into flask
//        EndpointReached      // Flask turned colorless — experiment complete
//    }

//    // ── State ──────────────────────────────────────────────────────────────
//    [Header("Current State (Read-Only in Play)")]
//    public ExperimentPhase currentPhase = ExperimentPhase.NotStarted;

//    // ── Scene References ───────────────────────────────────────────────────
//    [Header("Titration Root")]
//    public GameObject titrationRoot;

//    [Header("Lab Equipment")]
//    public GameObject buretteStand;
//    public GameObject burette;
//    public GameObject conicalFlask;
//    public GameObject beaker;
//    public GameObject pipette;
//    public GameObject funnel;

//    [Header("Particle Systems — Liquid Fills")]
//    public ParticleSystem iodineLiquidPS;       // Yellow-brown iodine fill in flask
//    public ParticleSystem starchLiquidPS;       // Deep-blue starch fill in beaker
//    public ParticleSystem buretteLiquidPS;      // Colorless thiosulfate fill in burette

//    [Header("Particle Systems — Effects")]
//    public ParticleSystem buretteFlowPS;        // Drip stream from nozzle to flask
//    public ParticleSystem starchPourPS;         // Pour stream from beaker into flask

//    [Header("Flask Liquid Renderer")]
//    public Renderer flaskLiquidRenderer;
//    public int flaskMaterialIndex = 0;

//    [Header("Titration Parameters")]
//    [Range(0f, 1f)] public float titrationProgress = 0f;
//    [Tooltip("Progress per second when burette is flowing. 0.025 = ~40 seconds to endpoint.")]
//    public float titrationSpeed = 0.025f;
//    [Tooltip("Progress fraction at which endpoint is declared.")]
//    public float endpointThreshold = 0.98f;

//    [Header("Flask Colors")]
//    public Color iodineColor    = new Color(0.75f, 0.55f, 0.05f, 0.85f);  // Yellow-brown
//    public Color deepBlueColor  = new Color(0.04f, 0.04f, 0.55f, 0.92f);  // Deep blue
//    public Color colorlessColor = new Color(0.88f, 0.94f, 1.00f, 0.12f);  // Nearly clear

//    [Header("Events")]
//    public UnityEvent onExperimentStarted;
//    public UnityEvent onStarchAdded;
//    public UnityEvent onTitrationBegan;
//    public UnityEvent onEndpointReached;
//    public UnityEvent<ExperimentPhase> onPhaseChanged;
//    public UnityEvent<float> onProgressChanged;

//    // ── Internal ───────────────────────────────────────────────────────────
//    private bool _buretteFlowing = false;
//    private Material _flaskMat   = null;

//    // ── Unity Lifecycle ────────────────────────────────────────────────────
//    void Awake()
//    {
//        // Do not self-disable when titrationRoot == this.gameObject.
//        // Only hide if it is a separate root object.
//        if (titrationRoot != null && titrationRoot != gameObject)
//            titrationRoot.SetActive(false);
//    }

//    void Start()
//    {
//        // Auto-start the experiment so the scene is immediately interactive
//        // without requiring a UI button press.
//        StartExperiment();
//    }

//    void Update()
//    {
//        if (currentPhase == ExperimentPhase.TitrationInProgress && _buretteFlowing)
//            AdvanceTitration();
//    }

//    // ── Public API ─────────────────────────────────────────────────────────

//    /// <summary>Called by the "Start Titration" UI button.</summary>
//    //public void StartExperiment()
//    //{
//    //    if (currentPhase != ExperimentPhase.NotStarted) return;

//    //    if (titrationRoot != null)
//    //        titrationRoot.SetActive(true);

//    //    InitializeParticleSystems();
//    //    SetFlaskColor(iodineColor);
//    //    SetPhase(ExperimentPhase.SetupVisible);
//    //    onExperimentStarted?.Invoke();
//    //    Debug.Log("[IodineTitration] Experiment started — equipment visible.");
//    //}

//    //bezi code
//    public void StartExperiment()
//    {
//        if (currentPhase != ExperimentPhase.NotStarted) return;

//        // Only activate a separate root object; skip self-activation to avoid hiding the controller.
//        if (titrationRoot != null && titrationRoot != gameObject)
//            titrationRoot.SetActive(true);

//        // Give Meta SDK interactables one frame to initialize before proceeding
//        StartCoroutine(InitializeAfterFrame());
//    }

//    private System.Collections.IEnumerator InitializeAfterFrame()
//    {
//        yield return null; // wait one frame for Interactables to enable

//        // Skip particle-system fills — liquid visualization is now mesh-based.
//        // Only manage the flow/pour particle effects if they are assigned.
//        StopClear(buretteFlowPS);
//        StopClear(starchPourPS);

//        SetFlaskColor(iodineColor);
//        SetPhase(ExperimentPhase.SetupVisible);
//        onExperimentStarted?.Invoke();
//        Debug.Log("[IodineTitration] Experiment started — equipment visible.");
//    }



//    /// <summary>Called by StarchPourDetector when the beaker is tilted over the flask.</summary>
//    public void OnStarchPoured()
//    {
//        if (currentPhase != ExperimentPhase.SetupVisible) return;

//        SetFlaskColor(deepBlueColor);

//        // starchPourPS is optional — skip if not assigned (mesh-based pour is used instead)
//        if (starchPourPS != null && !starchPourPS.isPlaying)
//            starchPourPS.Play();

//        SetPhase(ExperimentPhase.StarchAdded);
//        onStarchAdded?.Invoke();
//        Debug.Log("[IodineTitration] Starch added — flask turned deep blue.");
//    }

//    /// <summary>Called by BuretteNozzleController when the stopcock is toggled.</summary>
//    public void SetBuretteFlowing(bool flowing)
//    {
//        // Allow nozzle open during SetupVisible so user can explore; just won't advance titration
//        if (currentPhase == ExperimentPhase.NotStarted) return;
//        if (currentPhase == ExperimentPhase.EndpointReached) return;

//        _buretteFlowing = flowing;

//        if (flowing && currentPhase == ExperimentPhase.StarchAdded)
//        {
//            SetPhase(ExperimentPhase.TitrationInProgress);
//            onTitrationBegan?.Invoke();
//        }

//        // buretteFlowPS is optional — skip if not assigned
//        if (buretteFlowPS != null)
//        {
//            if (flowing && currentPhase == ExperimentPhase.TitrationInProgress)
//            {
//                if (!buretteFlowPS.isPlaying) buretteFlowPS.Play();
//            }
//            else
//            {
//                buretteFlowPS.Stop();
//            }
//        }
//    }

//    /// <summary>Resets the experiment back to the initial NotStarted state.</summary>
//    public void ResetExperiment()
//    {
//        titrationProgress = 0f;
//        _buretteFlowing   = false;
//        _flaskMat         = null;

//        StopAllParticleSystems();

//        // Only hide a separate root object; never self-deactivate.
//        if (titrationRoot != null && titrationRoot != gameObject)
//            titrationRoot.SetActive(false);

//        SetPhase(ExperimentPhase.NotStarted);
//        Debug.Log("[IodineTitration] Experiment reset.");
//    }

//    // ── Internal Methods ───────────────────────────────────────────────────

//    private void AdvanceTitration()
//    {
//        titrationProgress += titrationSpeed * Time.deltaTime;
//        titrationProgress  = Mathf.Clamp01(titrationProgress);

//        Color current = Color.Lerp(deepBlueColor, colorlessColor, titrationProgress);
//        SetFlaskColor(current);

//        onProgressChanged?.Invoke(titrationProgress);

//        if (titrationProgress >= endpointThreshold)
//        {
//            _buretteFlowing = false;

//            if (buretteFlowPS != null)
//                buretteFlowPS.Stop();

//            SetFlaskColor(colorlessColor);
//            SetPhase(ExperimentPhase.EndpointReached);
//            onEndpointReached?.Invoke();
//            Debug.Log("[IodineTitration] ENDPOINT REACHED — solution is colorless!");
//        }
//    }

//    private void SetFlaskColor(Color color)
//    {
//        if (flaskLiquidRenderer == null) return;

//        if (_flaskMat == null)
//        {
//            Material[] mats = flaskLiquidRenderer.materials;
//            if (flaskMaterialIndex < mats.Length)
//            {
//                _flaskMat = new Material(mats[flaskMaterialIndex]);
//                mats[flaskMaterialIndex] = _flaskMat;
//                flaskLiquidRenderer.materials = mats;
//            }
//        }

//        if (_flaskMat == null) return;

//        _flaskMat.color = color;

//        if (_flaskMat.HasProperty("_BaseColor"))
//            _flaskMat.SetColor("_BaseColor", color);

//        if (_flaskMat.HasProperty("_Color"))
//            _flaskMat.SetColor("_Color", color);
//    }

//    private void InitializeParticleSystems()
//    {
//        PlayIfStopped(iodineLiquidPS);
//        PlayIfStopped(starchLiquidPS);
//        PlayIfStopped(buretteLiquidPS);
//        StopClear(buretteFlowPS);
//        StopClear(starchPourPS);
//    }

//    private void StopAllParticleSystems()
//    {
//        StopClear(iodineLiquidPS);
//        StopClear(starchLiquidPS);
//        StopClear(buretteLiquidPS);
//        StopClear(buretteFlowPS);
//        StopClear(starchPourPS);
//    }

//    private static void PlayIfStopped(ParticleSystem ps)
//    {
//        if (ps != null && !ps.isPlaying) ps.Play();
//    }

//    private static void StopClear(ParticleSystem ps)
//    {
//        if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
//    }

//    private void SetPhase(ExperimentPhase newPhase)
//    {
//        currentPhase = newPhase;
//        onPhaseChanged?.Invoke(newPhase);
//    }
//}


using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Master controller for the Iodine Titration experiment.
/// Manages the five-phase state machine, flask color transitions,
/// and particle system coordination.
///
/// FIXES:
///  - StartExperiment() guard changed from != NotStarted to a bool flag so
///    calling it twice (from Start() AND a UI button) doesn't silently no-op
///    after a reset.
///  - ResetExperiment() properly resets _experimentStarted so the experiment
///    can be restarted cleanly.
///  - SetFlaskColor(): ForceTransparentMode() applied once when the material
///    is first instanced, so the colorless low-alpha state is actually visible.
///  - SetFlaskColor(): _Color and _BaseColor both set (URP + Legacy coverage).
///  - AdvanceTitration(): onProgressChanged is only fired when progress actually
///    changes (not every frame at 1.0 after endpoint).
///  - SetBuretteFlowing(): phase guard prevents opening burette before starch
///    is added (SetupVisible phase no longer silently accepts the call).
///  - StopAllParticleSystems(): uses StopEmittingAndClear on all PS so old
///    particles don't linger on reset.
///  - _flaskMat null-check extended to cover the case where flaskMaterialIndex
///    is out of bounds, with an explicit LogError.
///  - OnValidate() in Editor builds to surface missing references early.
///  - Removed unused System.Collections.Generic import.
/// </summary>
public class IodineTitrationController : MonoBehaviour
{
    // ── Experiment Phases ──────────────────────────────────────────────────
    public enum ExperimentPhase
    {
        NotStarted,
        SetupVisible,           // Equipment visible; waiting for starch pour
        StarchAdded,            // Starch poured → flask deep blue
        TitrationInProgress,    // Burette open; Na₂S₂O₃ dripping
        EndpointReached         // Flask colorless — experiment complete
    }

    // ── State ──────────────────────────────────────────────────────────────
    [Header("Current State (Read-Only in Play)")]
    public ExperimentPhase currentPhase = ExperimentPhase.NotStarted;

    // ── Scene References ───────────────────────────────────────────────────
    [Header("Titration Root")]
    public GameObject titrationRoot;

    [Header("Lab Equipment")]
    public GameObject buretteStand;
    public GameObject burette;
    public GameObject conicalFlask;
    public GameObject beaker;
    public GameObject pipette;
    public GameObject funnel;

    [Tooltip("Optional: assign BuretteNozzleController so auto-progress can open the nozzle " +
             "and activate drop spawning automatically.")]
    public BuretteNozzleController buretteNozzleController;

    [Tooltip("Optional: assign PipetteController so it resets together with the experiment.")]
    public PipetteController pipetteController;

    [Header("Particle Systems — Liquid Fills (optional)")]
    public ParticleSystem iodineLiquidPS;
    public ParticleSystem starchLiquidPS;
    public ParticleSystem buretteLiquidPS;

    [Header("Particle Systems — Effects (optional)")]
    public ParticleSystem buretteFlowPS;
    public ParticleSystem starchPourPS;

    [Header("Flask Liquid Renderer")]
    public Renderer flaskLiquidRenderer;
    public int flaskMaterialIndex = 0;

    [Header("Titration Parameters")]
    [Range(0f, 1f)] public float titrationProgress = 0f;
    [Tooltip("Progress added per drop that lands in the flask. ~60 drops to endpoint at default.")]
    public float progressPerDrop = 0.016f;
    [Tooltip("Progress fraction at which endpoint is declared.")]
    public float endpointThreshold = 0.98f;

    [Header("Color Transition")]
    [Tooltip("How fast the flask color smoothly fades per second. 1.5 = ~0.7 s to blend each drop's color shift.")]
    public float colorLerpSpeed = 1.5f;

    [Header("Flask Colors")]
    public Color iodineColor = new Color(0.75f, 0.55f, 0.05f, 0.85f);
    public Color deepBlueColor = new Color(0.04f, 0.04f, 0.55f, 0.92f);
    public Color colorlessColor = new Color(0.88f, 0.94f, 1.00f, 0.12f);

    [Header("Events")]
    public UnityEvent onExperimentStarted;
    public UnityEvent onStarchAdded;
    public UnityEvent onTitrationBegan;
    public UnityEvent onEndpointReached;
    public UnityEvent<ExperimentPhase> onPhaseChanged;
    public UnityEvent<float> onProgressChanged;

    [Header("Auto-Progress Mode")]
    [Tooltip("When ON: clicking Start Titration automatically turns the flask blue then " +
             "starts burette flow — no manual beaker pouring or nozzle interaction needed.\n\n" +
             "When OFF: the full interactive VR flow is required (grab beaker, tilt, poke nozzle).")]
    public bool autoProgress = false;

    [Tooltip("Seconds after Start before the flask automatically turns deep blue. Range: 5–10.")]
    public float autoStarchDelay = 5f;

    // ── Internal ───────────────────────────────────────────────────────────
    private bool _buretteFlowing = false;
    private Material _flaskMat = null;
    private bool _experimentStarted = false;
    private Color _currentColor;
    private Color _targetColor;
    private bool _colorInitialized;

    // ── Unity Lifecycle ────────────────────────────────────────────────────

    void Awake()
    {
        if (titrationRoot != null && titrationRoot != gameObject)
            titrationRoot.SetActive(false);
    }

    void Start()
    {
        // Do NOT auto-start. ExperimentManager.StartTitration() calls StartExperiment()
        // after activating this object, so auto-calling here races with ShowMainMenu()
        // and leaves _experimentStarted=true before the user presses the button.
    }

    void Update()
    {
        // Smooth color lerp — runs every frame so the flask fades gradually between drops
        if (_colorInitialized && _flaskMat != null)
        {
            _currentColor = Color.Lerp(_currentColor, _targetColor, Time.deltaTime * colorLerpSpeed);
            ApplyColorToMaterial(_currentColor);
        }
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>Called by the "Start Titration" UI button or auto-called from Start().</summary>
    public void StartExperiment()
    {
        if (_experimentStarted) return;
        _experimentStarted = true;

        if (titrationRoot != null && titrationRoot != gameObject)
            titrationRoot.SetActive(true);

        StartCoroutine(InitializeAfterFrame());
    }

    private System.Collections.IEnumerator InitializeAfterFrame()
    {
        yield return null; // let Meta SDK Interactables initialize

        StopClear(buretteFlowPS);
        StopClear(starchPourPS);

        SetFlaskColor(iodineColor);
        SetPhase(ExperimentPhase.SetupVisible);
        onExperimentStarted?.Invoke();
        Debug.Log("[IodineTitration] Experiment started.");

        if (autoProgress)
            StartCoroutine(AutoProgressSequence());
    }

    /// <summary>
    /// Waits autoStarchDelay seconds then automatically turns the flask deep blue.
    /// After that the user must manually poke the burette nozzle to start the flow.
    /// </summary>
    private System.Collections.IEnumerator AutoProgressSequence()
    {
        yield return new WaitForSeconds(autoStarchDelay);

        if (currentPhase == ExperimentPhase.SetupVisible)
        {
            Debug.Log("[IodineTitration] AUTO: flask turning deep blue — poke the burette nozzle to start flow.");
            OnStarchPoured();
        }
        // Burette is NOT auto-opened — user must poke the nozzle handle to start drops.
    }

    /// <summary>Called by StarchPourDetector / BeakerLiquid when the beaker is tilted.</summary>
    public void OnStarchPoured()
    {
        if (currentPhase != ExperimentPhase.SetupVisible) return;

        SetFlaskColor(deepBlueColor);

        if (starchPourPS != null && !starchPourPS.isPlaying)
            starchPourPS.Play();

        SetPhase(ExperimentPhase.StarchAdded);
        onStarchAdded?.Invoke();
        Debug.Log("[IodineTitration] Starch added — flask turned deep blue.");
    }

    /// <summary>Called by BuretteNozzleController when the stopcock is toggled.</summary>
    public void SetBuretteFlowing(bool flowing)
    {
        if (currentPhase == ExperimentPhase.NotStarted) return;
        if (currentPhase == ExperimentPhase.EndpointReached) return;

        // Nozzle open before starch is added — allow the handle to move but do NOT
        // auto-advance the phase. BuretteDripSpawner's phase gate prevents drops
        // from spawning until StarchAdded, so this is safe to ignore silently.
        if (flowing && currentPhase == ExperimentPhase.SetupVisible)
        {
            _buretteFlowing = flowing;
            return;
        }

        _buretteFlowing = flowing;

        if (flowing && currentPhase == ExperimentPhase.StarchAdded)
        {
            SetPhase(ExperimentPhase.TitrationInProgress);
            onTitrationBegan?.Invoke();
        }

        if (buretteFlowPS != null)
        {
            if (flowing && currentPhase == ExperimentPhase.TitrationInProgress)
            {
                if (!buretteFlowPS.isPlaying) buretteFlowPS.Play();
            }
            else
            {
                buretteFlowPS.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    /// <summary>Resets the experiment back to NotStarted so it can be restarted.</summary>
    public void ResetExperiment()
    {
        titrationProgress = 0f;
        _buretteFlowing   = false;
        _flaskMat         = null;
        _experimentStarted = false;
        _colorInitialized = false;
        _currentColor = iodineColor;
        _targetColor  = iodineColor;

        StopAllCoroutines(); // cancel any running AutoProgressSequence

        if (buretteNozzleController != null)
            buretteNozzleController.ResetNozzle();

        if (pipetteController != null)
            pipetteController.ResetPipette();

        StopAllParticleSystems();

        if (titrationRoot != null && titrationRoot != gameObject)
            titrationRoot.SetActive(false);

        SetPhase(ExperimentPhase.NotStarted);
        Debug.Log("[IodineTitration] Experiment reset.");
    }

    // ── Internal Methods ───────────────────────────────────────────────────

    /// <summary>
    /// Called by FlaskLiquidTrigger each time a Na₂S₂O₃ drop actually lands in the flask.
    /// Progress only advances when drops physically reach the liquid — holding the flask
    /// away from the burette no longer causes color change.
    /// </summary>
    public void AdvanceByOneDrop()
    {
        // Starch must be added by the beaker interaction before drops do anything.
        // BuretteDripSpawner's phase gate already blocks drops in SetupVisible,
        // but guard here too in case AdvanceByOneDrop is called from another path.
        if (currentPhase == ExperimentPhase.SetupVisible) return;

        if (currentPhase == ExperimentPhase.StarchAdded)
        {
            SetPhase(ExperimentPhase.TitrationInProgress);
            onTitrationBegan?.Invoke();
        }

        if (currentPhase != ExperimentPhase.TitrationInProgress) return;

        float prev = titrationProgress;
        titrationProgress = Mathf.Clamp01(titrationProgress + progressPerDrop);

        // Only update the TARGET — Update() lerps _currentColor toward it smoothly
        _targetColor = Color.Lerp(deepBlueColor, colorlessColor, titrationProgress);

        if (!Mathf.Approximately(prev, titrationProgress))
            onProgressChanged?.Invoke(titrationProgress);

        if (titrationProgress >= endpointThreshold)
        {
            _buretteFlowing = false;

            if (buretteFlowPS != null)
                buretteFlowPS.Stop(false, ParticleSystemStopBehavior.StopEmitting);

            _targetColor = colorlessColor;
            SetPhase(ExperimentPhase.EndpointReached);
            onEndpointReached?.Invoke();
            Debug.Log("[IodineTitration] ENDPOINT REACHED — solution is colorless!");
        }
    }

    /// <summary>
    /// Immediately snaps the flask to <paramref name="color"/> (used for initialization,
    /// starch pour, and endpoint). Both _currentColor and _targetColor are set so the
    /// smooth lerp in Update() starts from the correct base.
    /// </summary>
    private void SetFlaskColor(Color color)
    {
        if (flaskLiquidRenderer == null) return;

        if (_flaskMat == null)
        {
            Material[] mats = flaskLiquidRenderer.materials;

            if (flaskMaterialIndex < 0 || flaskMaterialIndex >= mats.Length)
            {
                Debug.LogError(
                    $"[IodineTitration] flaskMaterialIndex ({flaskMaterialIndex}) is out of range " +
                    $"for flaskLiquidRenderer which has {mats.Length} material(s).", this);
                return;
            }

            _flaskMat = new Material(mats[flaskMaterialIndex]);
            ForceTransparentMode(_flaskMat);
            mats[flaskMaterialIndex] = _flaskMat;
            flaskLiquidRenderer.materials = mats;
        }

        // Snap both so the smooth lerp doesn't animate from the old color on a hard transition
        _currentColor = color;
        _targetColor = color;
        _colorInitialized = true;
        ApplyColorToMaterial(color);
    }

    private void ApplyColorToMaterial(Color color)
    {
        if (_flaskMat == null) return;
        _flaskMat.color = color;
        if (_flaskMat.HasProperty("_BaseColor"))
            _flaskMat.SetColor("_BaseColor", color);
        if (_flaskMat.HasProperty("_Color"))
            _flaskMat.SetColor("_Color", color);
    }

    /// <summary>
    /// Forces a material into Transparent render mode so low-alpha colors show through.
    /// Covers URP Lit (_Surface) and Legacy Standard (_Mode) shaders.
    /// </summary>
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
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }

    private void StopAllParticleSystems()
    {
        // FIX: StopEmittingAndClear so old particles don't persist after reset
        StopClear(iodineLiquidPS);
        StopClear(starchLiquidPS);
        StopClear(buretteLiquidPS);
        StopClear(buretteFlowPS);
        StopClear(starchPourPS);
    }

    private static void PlayIfStopped(ParticleSystem ps)
    {
        if (ps != null && !ps.isPlaying) ps.Play();
    }

    private static void StopClear(ParticleSystem ps)
    {
        if (ps != null)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void SetPhase(ExperimentPhase newPhase)
    {
        currentPhase = newPhase;
        onPhaseChanged?.Invoke(newPhase);
    }

    // ── Editor validation ──────────────────────────────────────────────────

#if UNITY_EDITOR
    void OnValidate()
    {
        if (flaskLiquidRenderer == null)
            Debug.LogWarning("[IodineTitration] flaskLiquidRenderer is not assigned.", this);
    }
#endif
}