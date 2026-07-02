//using UnityEngine;
//using UnityEngine.UI;
//using TMPro;
//using System.Globalization;

///// <summary>
///// Manages the visual dashboard UI for the Iodine Titration experiment.
///// Displays title, chemical equation, live step cards with colored indicators,
///// animated status bar, and a gradient progress bar.
///// </summary>
//public class TitrationDashboardUI : MonoBehaviour
//{
//    // ── Controller ─────────────────────────────────────────────────────────
//    [Header("Controller")]
//    public IodineTitrationController titrationController;

//    // ── Header ─────────────────────────────────────────────────────────────
//    [Header("Header")]
//    public TextMeshProUGUI titleText;
//    public TextMeshProUGUI equationText;

//    // ── Step Cards (5 total) ───────────────────────────────────────────────
//    [Header("Step Cards — assign 5 card root GameObjects")]
//    public GameObject[] stepCards;

//    [Header("Step Dot Indicators — one Image per card")]
//    public Image[] stepDots;

//    [Header("Step Number Labels — one TMP per card")]
//    public TextMeshProUGUI[] stepNumberLabels;

//    [Header("Step Description Labels — one TMP per card")]
//    public TextMeshProUGUI[] stepDescLabels;

//    // ── Status & Progress ──────────────────────────────────────────────────
//    [Header("Status & Progress")]
//    public Image statusPanel;
//    public TextMeshProUGUI statusText;
//    public TextMeshProUGUI progressText;
//    public Image progressBarFill;
//    public Image progressBarBg;

//    // ── Step content ───────────────────────────────────────────────────────
//    private static readonly string[] STEP_NUMBERS = { "01", "02", "03", "04", "05" };

//    private static readonly string[] STEP_DESCS =
//    {
//        "Flask filled with\n<color=#D4A017><b>iodine solution</b></color>\n(yellow-brown)",
//        "Tilt <color=#4FC3F7><b>beaker</b></color> over flask\nto pour <color=#5C6BC0><b>starch indicator</b></color>\nFlask turns deep blue",
//        "Rotate <color=#80DEEA><b>nozzle handle</b></color>\nto open burette and\nrelease Na\u2082S\u2082O\u2083 dropwise",
//        "Na\u2082S\u2082O\u2083 reacts with I\u2082\n<color=#5C6BC0><b>Deep blue</b></color> fades\nto <color=#E0F7FA><b>colourless</b></color>",
//        "<color=#69F0AE><b>Endpoint Reached!</b></color>\nSolution is colourless\nRecord burette reading"
//    };

//    // ── Phase colors ───────────────────────────────────────────────────────
//    private static readonly Color COL_PENDING  = new Color(0.25f, 0.28f, 0.38f, 1f);
//    private static readonly Color COL_ACTIVE   = new Color(1.00f, 0.82f, 0.10f, 1f);
//    private static readonly Color COL_DONE     = new Color(0.27f, 0.85f, 0.47f, 1f);

//    private static readonly Color CARD_PENDING  = new Color(0.08f, 0.11f, 0.20f, 0.85f);
//    private static readonly Color CARD_ACTIVE   = new Color(0.14f, 0.18f, 0.34f, 0.95f);
//    private static readonly Color CARD_DONE     = new Color(0.06f, 0.22f, 0.13f, 0.85f);

//    private static readonly Color STATUS_IDLE    = new Color(0.10f, 0.10f, 0.18f, 0.92f);
//    private static readonly Color STATUS_WARN    = new Color(0.28f, 0.18f, 0.04f, 0.95f);
//    private static readonly Color STATUS_BLUE    = new Color(0.08f, 0.10f, 0.28f, 0.95f);
//    private static readonly Color STATUS_FLOW    = new Color(0.04f, 0.16f, 0.28f, 0.95f);
//    private static readonly Color STATUS_SUCCESS = new Color(0.06f, 0.22f, 0.13f, 0.95f);

//    // ── Lifecycle ──────────────────────────────────────────────────────────
//    void Start()
//    {
//        SetStaticContent();
//        Refresh(IodineTitrationController.ExperimentPhase.NotStarted);
//        SetProgress(0f);
//    }

//    void OnEnable()
//    {
//        if (titrationController == null) return;
//        titrationController.onPhaseChanged.AddListener(Refresh);
//        titrationController.onProgressChanged.AddListener(SetProgress);
//    }

//    void OnDisable()
//    {
//        if (titrationController == null) return;
//        titrationController.onPhaseChanged.RemoveListener(Refresh);
//        titrationController.onProgressChanged.RemoveListener(SetProgress);
//    }

//    // ── Public API ─────────────────────────────────────────────────────────

//    /// <summary>Refreshes all step cards and status panel for the given phase.</summary>
//    public void Refresh(IodineTitrationController.ExperimentPhase phase)
//    {
//        RefreshStepCards(phase);
//        RefreshStatusPanel(phase);
//    }

//    /// <summary>Updates progress bar fill and percentage label.</summary>
//    public void SetProgress(float t)
//    {
//        if (progressBarFill != null)
//        {
//            progressBarFill.fillAmount = t;
//            // Gradient: deep blue (0) → colorless sky (1)
//            progressBarFill.color = Color.Lerp(
//                new Color(0.22f, 0.34f, 0.90f, 1f),
//                new Color(0.56f, 0.95f, 1.00f, 1f),
//                t);
//        }

//        if (progressText != null)
//            progressText.text = $"Titration  <b>{(t * 100f):F0}%</b> complete";
//    }

//    // ── Private helpers ────────────────────────────────────────────────────

//    private void SetStaticContent()
//    {
//        if (titleText != null)
//            titleText.text =
//                "<b>IODINE TITRATION</b>\n" +
//                "<size=70%><color=#9AB4CC>Iodometry Experiment</color></size>";

//        if (equationText != null)
//            equationText.text =
//                "<color=#FFD700><b>I\u2082</b></color>  +  " +
//                "<color=#80DEEA><b>2 Na\u2082S\u2082O\u2083</b></color>  " +
//                "<color=#FFFFFF>\u2192</color>  " +
//                "<color=#A5D6A7><b>2 NaI</b></color>  +  " +
//                "<color=#CE93D8><b>Na\u2082S\u2084O\u2086</b></color>";
//    }

//    private void RefreshStepCards(IodineTitrationController.ExperimentPhase phase)
//    {
//        int activeIndex = (int)phase;

//        for (int i = 0; i < 5; i++)
//        {
//            bool isDone   = i < activeIndex;
//            bool isActive = i == activeIndex;

//            // Dot color
//            if (stepDots != null && i < stepDots.Length && stepDots[i] != null)
//                stepDots[i].color = isDone ? COL_DONE : isActive ? COL_ACTIVE : COL_PENDING;

//            // Card background
//            if (stepCards != null && i < stepCards.Length && stepCards[i] != null)
//            {
//                var img = stepCards[i].GetComponent<Image>();
//                if (img != null)
//                    img.color = isDone ? CARD_DONE : isActive ? CARD_ACTIVE : CARD_PENDING;
//            }

//            // Step number
//            if (stepNumberLabels != null && i < stepNumberLabels.Length && stepNumberLabels[i] != null)
//            {
//                stepNumberLabels[i].color = isDone ? COL_DONE : isActive ? COL_ACTIVE : new Color(0.4f, 0.4f, 0.55f, 1f);
//                stepNumberLabels[i].text  = isDone ? "<b>\u2713</b>" : $"<b>{STEP_NUMBERS[i]}</b>";
//            }

//            // Description
//            if (stepDescLabels != null && i < stepDescLabels.Length && stepDescLabels[i] != null)
//            {
//                string prefix = isActive ? "" : "";
//                string wrap   = isDone ? "<color=#5DBF79>" : isActive ? "<color=#FFFFFF>" : "<color=#4A5070>";
//                stepDescLabels[i].text = $"{wrap}{prefix}{STEP_DESCS[i]}</color>";
//            }
//        }
//    }

//    private void RefreshStatusPanel(IodineTitrationController.ExperimentPhase phase)
//    {
//        if (statusPanel != null)
//        {
//            statusPanel.color = phase switch
//            {
//                IodineTitrationController.ExperimentPhase.NotStarted        => STATUS_IDLE,
//                IodineTitrationController.ExperimentPhase.SetupVisible      => STATUS_WARN,
//                IodineTitrationController.ExperimentPhase.StarchAdded       => STATUS_BLUE,
//                IodineTitrationController.ExperimentPhase.TitrationInProgress => STATUS_FLOW,
//                IodineTitrationController.ExperimentPhase.EndpointReached   => STATUS_SUCCESS,
//                _ => STATUS_IDLE
//            };
//        }

//        if (statusText == null) return;

//        statusText.text = phase switch
//        {
//            IodineTitrationController.ExperimentPhase.NotStarted =>
//                "<color=#6B7A99>Press  <b>Start Titration</b>  to begin the experiment.</color>",

//            IodineTitrationController.ExperimentPhase.SetupVisible =>
//                "<color=#FFD54F>Grab the <b>blue beaker</b> and tilt it over the\nconical flask to add the starch indicator.</color>",

//            IodineTitrationController.ExperimentPhase.StarchAdded =>
//                "<color=#9FA8DA>Starch added — flask is now <b>deep blue</b>.\nRotate the <b>nozzle handle</b> to open the burette.</color>",

//            IodineTitrationController.ExperimentPhase.TitrationInProgress =>
//                "<color=#4FC3F7>Na\u2082S\u2082O\u2083 dripping into flask.\nWatch the <b>colour change</b> towards colourless!</color>",

//            IodineTitrationController.ExperimentPhase.EndpointReached =>
//                "<color=#69F0AE><b>Endpoint reached!</b>\nSolution is colourless — titration complete.</color>",

//            _ => string.Empty
//        };
//    }
//}



using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the visual dashboard UI for the Iodine Titration experiment.
///
/// FIXES:
///  - Array length is validated at Start() with a clear error for each array that
///    doesn't have exactly 5 elements — prevents silent card skips.
///  - All four parallel arrays (stepCards, stepDots, stepNumberLabels, stepDescLabels)
///    are bounds-checked against STEP_COUNT (5) before indexing.
///  - titrationController null-guard in both OnEnable and OnDisable.
///  - SetProgress() clamps t to [0,1] so an out-of-range value can't corrupt the bar.
///  - Removed the unused System.Globalization import.
///  - progressBarFill null-guard before setting color (was already present but
///    consolidated into a single null-check path).
///  - RefreshStatusPanel and RefreshStepCards are both null-guarded on every
///    UI element before access.
/// </summary>
public class TitrationDashboardUI : MonoBehaviour
{
    private const int STEP_COUNT = 5;

    // ── Controller ─────────────────────────────────────────────────────────
    [Header("Controller")]
    public IodineTitrationController titrationController;

    [Header("Back Button")]
    [Tooltip("ExperimentManager to call ShowMainMenuAndReset() on. Auto-found if not assigned.")]
    public ExperimentManager experimentManager;
    [Tooltip("Assign an existing Back button in your Canvas, OR leave empty to auto-create one.")]
    public Button backButton;

    // ── Header ─────────────────────────────────────────────────────────────
    [Header("Header")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI equationText;

    // ── Step Cards ─────────────────────────────────────────────────────────
    [Header("Step Cards — assign exactly 5 card root GameObjects")]
    public GameObject[] stepCards;

    [Header("Step Dot Indicators — one Image per card")]
    public Image[] stepDots;

    [Header("Step Number Labels — one TMP per card")]
    public TextMeshProUGUI[] stepNumberLabels;

    [Header("Step Description Labels — one TMP per card")]
    public TextMeshProUGUI[] stepDescLabels;

    // ── Status & Progress ──────────────────────────────────────────────────
    [Header("Status & Progress")]
    public Image statusPanel;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI progressText;
    public Image progressBarFill;
    public Image progressBarBg;

    // ── Step content (static) ──────────────────────────────────────────────
    private static readonly string[] STEP_NUMBERS = { "01", "02", "03", "04", "05" };

    private static readonly string[] STEP_DESCS =
    {
        "Flask filled with\n<color=#D4A017><b>iodine solution</b></color>\n(yellow-brown)",
        "Tilt <color=#4FC3F7><b>beaker</b></color> over flask\nto pour <color=#5C6BC0><b>starch indicator</b></color>\nFlask turns deep blue",
        "Rotate <color=#80DEEA><b>nozzle handle</b></color>\nto open burette and\nrelease Na\u2082S\u2082O\u2083 dropwise",
        "Na\u2082S\u2082O\u2083 reacts with I\u2082\n<color=#5C6BC0><b>Deep blue</b></color> fades\nto <color=#E0F7FA><b>colourless</b></color>",
        "<color=#69F0AE><b>Endpoint Reached!</b></color>\nSolution is colourless\nRecord burette reading"
    };

    // ── Phase colors ───────────────────────────────────────────────────────
    private static readonly Color COL_PENDING = new Color(0.25f, 0.28f, 0.38f, 1f);
    private static readonly Color COL_ACTIVE = new Color(1.00f, 0.82f, 0.10f, 1f);
    private static readonly Color COL_DONE = new Color(0.27f, 0.85f, 0.47f, 1f);

    private static readonly Color CARD_PENDING = new Color(0.08f, 0.11f, 0.20f, 0.85f);
    private static readonly Color CARD_ACTIVE = new Color(0.14f, 0.18f, 0.34f, 0.95f);
    private static readonly Color CARD_DONE = new Color(0.06f, 0.22f, 0.13f, 0.85f);

    private float _lastPolledProgress = -1f; // tracks last value pushed to the UI

    private static readonly Color STATUS_IDLE = new Color(0.10f, 0.10f, 0.18f, 0.92f);
    private static readonly Color STATUS_WARN = new Color(0.28f, 0.18f, 0.04f, 0.95f);
    private static readonly Color STATUS_BLUE = new Color(0.08f, 0.10f, 0.28f, 0.95f);
    private static readonly Color STATUS_FLOW = new Color(0.04f, 0.16f, 0.28f, 0.95f);
    private static readonly Color STATUS_SUCCESS = new Color(0.06f, 0.22f, 0.13f, 0.95f);

    // ── Lifecycle ──────────────────────────────────────────────────────────

    void Start()
    {
        if (experimentManager == null)
            experimentManager = FindFirstObjectByType<ExperimentManager>();

        ValidateArrayLengths();
        SetStaticContent();
        Refresh(IodineTitrationController.ExperimentPhase.NotStarted);
        SetProgress(0f);
        SetupBackButton();
    }

    void Update()
    {
        // Polling fallback — only rebuild UI when the value actually changes
        if (titrationController == null) return;
        var phase = titrationController.currentPhase;
        if (phase == IodineTitrationController.ExperimentPhase.TitrationInProgress ||
            phase == IodineTitrationController.ExperimentPhase.EndpointReached)
        {
            float p = titrationController.titrationProgress;
            if (!Mathf.Approximately(p, _lastPolledProgress))
            {
                _lastPolledProgress = p;
                SetProgress(p);
            }
        }
    }

    void OnEnable()
    {
        // FIX: null-guard before subscribing
        if (titrationController == null) return;
        titrationController.onPhaseChanged.AddListener(Refresh);
        titrationController.onProgressChanged.AddListener(SetProgress);
    }

    void OnDisable()
    {
        // FIX: null-guard before unsubscribing
        if (titrationController == null) return;
        titrationController.onPhaseChanged.RemoveListener(Refresh);
        titrationController.onProgressChanged.RemoveListener(SetProgress);
    }

    // ── Public API ─────────────────────────────────────────────────────────

    public void Refresh(IodineTitrationController.ExperimentPhase phase)
    {
        RefreshStepCards(phase);
        RefreshStatusPanel(phase);
    }

    public void SetProgress(float t)
    {
        // FIX: clamp so an out-of-range value can't push fillAmount outside [0,1]
        t = Mathf.Clamp01(t);

        if (progressBarFill != null)
        {
            progressBarFill.fillAmount = t;
            progressBarFill.color = Color.Lerp(
                new Color(0.22f, 0.34f, 0.90f, 1f),
                new Color(0.56f, 0.95f, 1.00f, 1f),
                t);
        }

        if (progressText != null)
            progressText.text = $"Titration  <b>{t * 100f:F0}%</b> complete";
    }

    // ── Private helpers ────────────────────────────────────────────────────

    /// <summary>
    /// If a UI Button is assigned in the Inspector, wires its onClick.
    /// Otherwise creates a 3D physics-trigger cube BELOW the canvas (VR hand-touchable)
    /// plus a matching UI label anchored outside the canvas rect.
    /// </summary>
    private void SetupBackButton()
    {
        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(() => experimentManager?.ShowMainMenuAndReset());
            return;
        }

        Create3DBackButton();
    }

    private void Create3DBackButton()
    {
        // World-space height of this canvas so we can place the button below it
        RectTransform canvasRT = GetComponent<RectTransform>();
        float worldHalfH = canvasRT != null
            ? canvasRT.rect.height * 0.5f * Mathf.Abs(transform.lossyScale.y)
            : 0.30f;
        if (worldHalfH <= 0f) worldHalfH = 0.30f;

        // Button centre: 5 cm below the canvas bottom edge, same facing direction
        Vector3 btnCenter = transform.position - transform.up * (worldHalfH + 0.05f);
        Quaternion btnRot = transform.rotation;

        // ── Flat cube body (22 cm × 6 cm × 2.5 cm) ─────────────────────────────
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "BackButton3D";
        cube.transform.SetPositionAndRotation(btnCenter, btnRot);
        cube.transform.localScale = new Vector3(0.22f, 0.06f, 0.025f);

        // Stay hidden/shown together with the rest of TitrationSetup
        if (transform.parent != null)
            cube.transform.SetParent(transform.parent, true);

        cube.GetComponent<Collider>().isTrigger = true;

        var rb = cube.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity  = false;

        var rend = cube.GetComponent<Renderer>();
        rend.material.color = new Color(0.15f, 0.30f, 0.80f, 1f);

        // Physics-trigger back button (OnTriggerEnter fires when hand touches cube)
        var physBtn = cube.AddComponent<BackToMenuButton>();
        physBtn.experimentManager = experimentManager;
        physBtn.buttonRenderer    = rend;
        physBtn.normalColor       = new Color(0.15f, 0.30f, 0.80f, 1f);
        physBtn.pressedColor      = new Color(0.10f, 0.80f, 0.35f, 1f);

        // Let ExperimentManager.StartTitration / ShowMainMenu control visibility
        if (experimentManager != null && experimentManager.titrationBackButton == null)
            experimentManager.titrationBackButton = cube;

        // Text printed directly on the cube face
        AddTextToCube(cube);

        Debug.Log($"[TitrationDashboardUI] 3D back button created at {btnCenter}");
    }

    // Places a world-space canvas on the front face of the cube so the
    // "Back to Main Menu" text is printed directly on the button surface.
    private static void AddTextToCube(GameObject cube)
    {
        var labelRoot = new GameObject("BackButtonLabel");
        labelRoot.transform.SetParent(cube.transform, false);

        // Front face of the cube mesh is at local Z = -0.5; -0.51 clears the surface
        labelRoot.transform.localPosition = new Vector3(0f, 0f, -0.51f);
        labelRoot.transform.localRotation = Quaternion.identity;

        // Make 1 canvas-unit = 1 mm in world space, accounting for cube local scale.
        // cube localScale = (0.22, 0.06, 0.025)
        // canvas worldScale = cubeScale * localScale → localScale = 0.001 / cubeScale
        labelRoot.transform.localScale = new Vector3(
            0.001f / 0.22f,   // ≈ 0.00455
            0.001f / 0.06f,   // ≈ 0.01667
            1f);

        var cvs = labelRoot.AddComponent<Canvas>();
        cvs.renderMode = RenderMode.WorldSpace;
        labelRoot.AddComponent<CanvasScaler>();

        var crt = labelRoot.GetComponent<RectTransform>();
        crt.sizeDelta = new Vector2(220f, 60f); // 220 mm × 60 mm in world

        var txtGO = new GameObject("Text");
        txtGO.transform.SetParent(labelRoot.transform, false);
        var trt = txtGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = "Back to Main Menu";
        tmp.fontSize  = 28f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
        tmp.fontStyle = FontStyles.Bold;
    }

    /// <summary>
    /// FIX: Validates that all four step-card arrays have exactly STEP_COUNT elements.
    /// Logs a clear error for each mismatch so a designer can fix the Inspector setup.
    /// </summary>
    private void ValidateArrayLengths()
    {
        CheckArray(stepCards, nameof(stepCards));
        CheckArray(stepDots, nameof(stepDots));
        CheckArray(stepNumberLabels, nameof(stepNumberLabels));
        CheckArray(stepDescLabels, nameof(stepDescLabels));
    }

    private void CheckArray(System.Array arr, string fieldName)
    {
        if (arr == null || arr.Length != STEP_COUNT)
            Debug.LogError(
                $"[TitrationDashboardUI] '{fieldName}' must have exactly {STEP_COUNT} elements " +
                $"(currently {(arr == null ? 0 : arr.Length)}). Some step cards will be skipped.",
                this);
    }

    private void SetStaticContent()
    {
        if (titleText != null)
            titleText.text =
                "<b>IODINE TITRATION</b>\n" +
                "<size=70%><color=#9AB4CC>Iodometry Experiment</color></size>";

        if (equationText != null)
            equationText.text =
                "<color=#FFD700><b>I\u2082</b></color>  +  " +
                "<color=#80DEEA><b>2 Na\u2082S\u2082O\u2083</b></color>  " +
                "<color=#FFFFFF>\u2192</color>  " +
                "<color=#A5D6A7><b>2 NaI</b></color>  +  " +
                "<color=#CE93D8><b>Na\u2082S\u2084O\u2086</b></color>";
    }

    private void RefreshStepCards(IodineTitrationController.ExperimentPhase phase)
    {
        int activeIndex = (int)phase;

        for (int i = 0; i < STEP_COUNT; i++)
        {
            bool isDone = i < activeIndex;
            bool isActive = i == activeIndex;

            // FIX: each array is individually null + bounds checked
            if (stepDots != null && i < stepDots.Length && stepDots[i] != null)
                stepDots[i].color = isDone ? COL_DONE : isActive ? COL_ACTIVE : COL_PENDING;

            if (stepCards != null && i < stepCards.Length && stepCards[i] != null)
            {
                var img = stepCards[i].GetComponent<Image>();
                if (img != null)
                    img.color = isDone ? CARD_DONE : isActive ? CARD_ACTIVE : CARD_PENDING;
            }

            if (stepNumberLabels != null && i < stepNumberLabels.Length && stepNumberLabels[i] != null)
            {
                stepNumberLabels[i].color = isDone ? COL_DONE
                                          : isActive ? COL_ACTIVE
                                          : new Color(0.4f, 0.4f, 0.55f, 1f);
                stepNumberLabels[i].text = isDone ? "<b>\u2713</b>"
                                                   : $"<b>{STEP_NUMBERS[i]}</b>";
            }

            if (stepDescLabels != null && i < stepDescLabels.Length && stepDescLabels[i] != null)
            {
                string wrap = isDone ? "<color=#5DBF79>"
                            : isActive ? "<color=#FFFFFF>"
                                        : "<color=#4A5070>";
                stepDescLabels[i].text = $"{wrap}{STEP_DESCS[i]}</color>";
            }
        }
    }

    private void RefreshStatusPanel(IodineTitrationController.ExperimentPhase phase)
    {
        if (statusPanel != null)
        {
            statusPanel.color = phase switch
            {
                IodineTitrationController.ExperimentPhase.NotStarted => STATUS_IDLE,
                IodineTitrationController.ExperimentPhase.SetupVisible => STATUS_WARN,
                IodineTitrationController.ExperimentPhase.StarchAdded => STATUS_BLUE,
                IodineTitrationController.ExperimentPhase.TitrationInProgress => STATUS_FLOW,
                IodineTitrationController.ExperimentPhase.EndpointReached => STATUS_SUCCESS,
                _ => STATUS_IDLE
            };
        }

        if (statusText == null) return;

        statusText.text = phase switch
        {
            IodineTitrationController.ExperimentPhase.NotStarted =>
                "<color=#6B7A99>Press  <b>Start Titration</b>  to begin the experiment.</color>",

            IodineTitrationController.ExperimentPhase.SetupVisible =>
                "<color=#FFD54F>Grab the <b>blue beaker</b> and tilt it over the\nconical flask to add the starch indicator.</color>",

            IodineTitrationController.ExperimentPhase.StarchAdded =>
                "<color=#9FA8DA>Starch added — flask is now <b>deep blue</b>.\nRotate the <b>nozzle handle</b> to open the burette.</color>",

            IodineTitrationController.ExperimentPhase.TitrationInProgress =>
                "<color=#4FC3F7>Na\u2082S\u2082O\u2083 dripping into flask.\nWatch the <b>colour change</b> towards colourless!</color>",

            IodineTitrationController.ExperimentPhase.EndpointReached =>
                "<color=#69F0AE><b>Endpoint reached!</b>\nSolution is colourless — titration complete.</color>",

            _ => string.Empty
        };
    }

    // ── Editor validation ──────────────────────────────────────────────────

#if UNITY_EDITOR
    void OnValidate()
    {
        if (titrationController == null)
            Debug.LogWarning("[TitrationDashboardUI] titrationController is not assigned.", this);

        CheckArray(stepCards, nameof(stepCards));
        CheckArray(stepDots, nameof(stepDots));
        CheckArray(stepNumberLabels, nameof(stepNumberLabels));
        CheckArray(stepDescLabels, nameof(stepDescLabels));
    }
#endif
}