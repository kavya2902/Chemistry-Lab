using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space Canvas instruction board for the Litmus Paper Test.
/// Mirrors the visual style of TitrationDashboardUI — large, readable,
/// with step cards, title, and a colour-coded summary row.
///
/// SETUP:
///   1. Create an empty GameObject named "Dashboard" next to the beakers.
///   2. Attach this script.
///   3. Press Play — the Canvas is built automatically and faces the camera.
///   4. Adjust boardWidthMetre / boardHeightMetre in the Inspector if needed.
/// </summary>
public class LitmusDashboardUI : MonoBehaviour
{
    [Header("Canvas Size (metres in world space)")]
    public float boardWidthMetre  = 0.70f;
    public float boardHeightMetre = 1.00f;   // increased from 0.90 to fit back button

    [Header("Position / Rotation Offset from this GameObject")]
    public Vector3 positionOffset = Vector3.zero;
    [Tooltip("Set Y = 180 if the board faces away from the player.")]
    public Vector3 rotationOffset = Vector3.zero;

    [Header("Back Button")]
    [Tooltip("ExperimentManager that owns ShowMainMenu(). Auto-found if left empty.")]
    public ExperimentManager experimentManager;

    // 1 Canvas unit = 1 mm  →  scale 0.001 makes it 1 mm per pixel
    private const float CANVAS_SCALE = 0.001f;

    // Pixel dimensions of the Canvas (metres / scale = pixels)
    private float CanvasW => boardWidthMetre  / CANVAS_SCALE;
    private float CanvasH => boardHeightMetre / CANVAS_SCALE;

    private const float PADDING_PX  = 28f;
    private const float CARD_GAP_PX = 10f;

    // Font sizes in pixels (at 0.001 world scale → good VR readability at 0.5–1.5 m)
    private const float FONT_TITLE   = 40f;
    private const float FONT_STEP    = 26f;
    private const float FONT_BODY    = 22f;
    private const float FONT_SUMMARY = 20f;

    // Colours matching the titration dashboard palette
    private static readonly Color COL_BG         = new Color(0.05f, 0.07f, 0.18f, 0.97f);
    private static readonly Color COL_HEADER_BAR = new Color(0.28f, 0.73f, 1.00f, 1.00f);
    private static readonly Color COL_CARD_BG    = new Color(0.09f, 0.12f, 0.23f, 0.92f);
    private static readonly Color COL_CARD_PINK  = new Color(1.00f, 0.56f, 0.75f, 0.18f);
    private static readonly Color COL_CARD_BLUE  = new Color(0.38f, 0.65f, 1.00f, 0.18f);
    private static readonly Color COL_ACCENT     = new Color(1.00f, 0.82f, 0.10f, 1.00f);
    private static readonly Color COL_TEXT_DIM   = new Color(0.55f, 0.65f, 0.80f, 1.00f);

    private bool      _built;
    private Transform _boardRoot;   // kept so LateUpdate can billboard it every frame

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        if (!_built) { BuildBoard(); _built = true; }
    }

    // Keep board facing the player every frame — fixes the VR camera-alignment issue
    // where FaceCamera() fires before headset tracking is active.
    private void LateUpdate()
    {
        if (_boardRoot != null) FaceCamera(_boardRoot);
    }

    // ── Build ─────────────────────────────────────────────────────────────────

    private void BuildBoard()
    {
        var root = new GameObject("LitmusBoard_Root");
        root.transform.SetParent(transform, false);
        root.transform.localPosition    = positionOffset;
        root.transform.localEulerAngles = rotationOffset;

        _boardRoot = root.transform;   // stored so LateUpdate can re-face every frame

        // Initial face — LateUpdate keeps it correct once VR tracking is active.
        FaceCamera(root.transform);

        // ── World-space Canvas ───────────────────────────────────────────────
        var cvGO = new GameObject("LitmusCanvas");
        cvGO.transform.SetParent(root.transform, false);
        cvGO.transform.localScale = Vector3.one * CANVAS_SCALE;

        var canvas = cvGO.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main != null ? Camera.main
                           : Object.FindFirstObjectByType<Camera>();

        var scaler = cvGO.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        var cvRt = cvGO.GetComponent<RectTransform>();
        cvRt.sizeDelta = new Vector2(CanvasW, CanvasH);

        // ── Full background ──────────────────────────────────────────────────
        MakeImage(cvGO.transform, "BG",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            COL_BG);

        // ── Header bar ───────────────────────────────────────────────────────
        float headerH = 70f;
        MakeImageAnchored(cvGO.transform, "HeaderBar",
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -headerH), new Vector2(0f, 0f),
            COL_HEADER_BAR);

        // Left accent bar
        MakeImageAnchored(cvGO.transform, "LeftBar",
            new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(0f, 0f), new Vector2(8f, 0f),
            COL_HEADER_BAR);

        // ── Title text ───────────────────────────────────────────────────────
        var titleGO  = MakeTextGO(cvGO.transform, "TitleText",
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(PADDING_PX, -headerH),
            new Vector2(-PADDING_PX, 0f),
            FONT_TITLE, TextAlignmentOptions.Left);
        titleGO.text =
            "<b>LITMUS PAPER TEST</b>\n" +
            $"<size={FONT_BODY}><color=#9AB4CC>pH Indicator Experiment</color></size>";

        // ── Step cards area ──────────────────────────────────────────────────
        // 6 step cards stacked vertically below the header
        string[] stepNums  = { "01", "02", "03", "04", "05", "06" };
        Color[]  cardTints =
        {
            COL_CARD_BG,       // Step 1: beakers intro
            COL_CARD_PINK,     // Step 2: pink paper
            COL_CARD_PINK,     // Step 3: dip pink
            COL_CARD_BLUE,     // Step 4: blue paper
            COL_CARD_BLUE,     // Step 5: dip blue
            COL_CARD_BG        // Step 6: record
        };
        string[] stepTitles =
        {
            "① Three Beakers on the Bench",
            "② Pick Up Pink Litmus Paper",
            "③ Dip Pink Paper into Each Beaker",
            "④ Pick Up Blue Litmus Paper",
            "⑤ Dip Blue Paper into Each Beaker",
            "⑥ Record Your Observations"
        };
        string[] stepBodies =
        {
            "Each beaker holds an <b>unknown solution</b>.\n" +
                "Identify which is  <color=#FF7070><b>Acid</b></color>  ·  " +
                "<color=#6EA6FF><b>Base</b></color>  ·  " +
                "<color=#69F0AE><b>Neutral</b></color>.",

            "Grab the <color=#FF90C0><b>Pink (Red)</b></color> litmus paper.\n" +
                "Hold firmly by the <b>dry top end</b>.",

            "Lower the <b>bottom half</b> and wait 1–2 s:\n" +
                "Turns <color=#6EA6FF><b>BLUE</b></color> → <b>BASE DETECTED</b>   |   " +
                "No change → Acid or Neutral",

            "Return the pink paper, then grab\n" +
                "the <color=#90B8FF><b>Blue</b></color> litmus paper from the holder.",

            "Lower the <b>bottom half</b> and wait 1–2 s:\n" +
                "Turns <color=#FF6E6E><b>RED</b></color>  → <b>ACID DETECTED</b>    |   " +
                "No change → Base or Neutral",

            "<color=#FF90C0>Pink</color> + Base → <color=#6EA6FF><b>Blue</b></color>     " +
                "<color=#90B8FF>Blue</color> + Acid → <color=#FF6E6E><b>Red</b></color>     " +
                "<color=#69F0AE>Any + Neutral → No change</color>"
        };

        // Reserve space at the bottom for: back button + gap + footer hint + bottom padding
        const float BACK_BTN_H  = 65f;
        const float BACK_BTN_GAP = 10f;
        const float FOOTER_H_PX  = FONT_SUMMARY + 14f;
        float bottomReserved = PADDING_PX + BACK_BTN_H + BACK_BTN_GAP + FOOTER_H_PX + PADDING_PX;

        float usableH       = CanvasH - headerH - PADDING_PX - bottomReserved;
        float cardH         = (usableH - CARD_GAP_PX * 5f) / 6f;
        float cardTopOffset = headerH + PADDING_PX;

        for (int i = 0; i < 6; i++)
        {
            float yFromTop  = cardTopOffset + i * (cardH + CARD_GAP_PX);
            var cardAncMin  = new Vector2(0f, 1f);
            var cardAncMax  = new Vector2(1f, 1f);
            var cardOffMin  = new Vector2(PADDING_PX, -(yFromTop + cardH));
            var cardOffMax  = new Vector2(-PADDING_PX, -yFromTop);

            var cardGO = MakeImageAnchored(cvGO.transform, $"Card_{i + 1}",
                cardAncMin, cardAncMax, cardOffMin, cardOffMax,
                cardTints[i]);

            // Dot indicator on the left
            MakeImageAnchored(cardGO.transform, "Dot",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(12f, -10f), new Vector2(26f, 10f),
                COL_ACCENT);

            // Step number
            var numText = MakeTextGO(cardGO.transform, "StepNum",
                new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(30f, 0f), new Vector2(80f, 0f),
                FONT_STEP * 0.85f, TextAlignmentOptions.Center);
            numText.text      = $"<b>{stepNums[i]}</b>";
            numText.color     = COL_ACCENT;

            // Step title
            var titleTxt = MakeTextGO(cardGO.transform, "StepTitle",
                new Vector2(0f, 0.5f), new Vector2(1f, 1f),
                new Vector2(84f, 0f), new Vector2(-8f, -4f),
                FONT_STEP, TextAlignmentOptions.BottomLeft);
            titleTxt.text  = $"<b><color=#FFD166>{stepTitles[i]}</color></b>";

            // Step body
            var bodyTxt = MakeTextGO(cardGO.transform, "StepBody",
                new Vector2(0f, 0f), new Vector2(1f, 0.5f),
                new Vector2(84f, 4f), new Vector2(-8f, 0f),
                FONT_BODY, TextAlignmentOptions.TopLeft);
            bodyTxt.text  = $"<color=#C8D8F0>{stepBodies[i]}</color>";
        }

        // ── Back to Main Menu button — at the very bottom of the canvas ─────────
        // Positions are in pixels from the canvas bottom edge (anchor = bottom-stretch).
        float backBtnY0 = PADDING_PX;                       // bottom of button
        float backBtnY1 = backBtnY0 + BACK_BTN_H;          // top of button

        // Dark-blue background panel
        var backBtnGO = MakeImageAnchored(cvGO.transform, "BackButton",
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(PADDING_PX,  backBtnY0),
            new Vector2(-PADDING_PX, backBtnY1),
            new Color(0.10f, 0.20f, 0.55f, 0.95f));

        // Left accent stripe on the button
        MakeImageAnchored(backBtnGO.transform, "BtnAccent",
            new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(0f, 0f), new Vector2(6f, 0f),
            COL_HEADER_BAR);

        // Button label
        var backBtnTxt = MakeTextGO(backBtnGO.transform, "BackBtnLabel",
            Vector2.zero, Vector2.one,
            new Vector2(12f, 4f), new Vector2(-8f, -4f),
            FONT_STEP, TextAlignmentOptions.Center);
        backBtnTxt.text  = "<b>←  Back to Main Menu</b>";
        backBtnTxt.color = Color.white;

        // Wire Button.onClick so pointer-based VR systems (raycaster) can use it too
        if (experimentManager == null)
            experimentManager = Object.FindFirstObjectByType<ExperimentManager>();
        if (experimentManager != null)
        {
            var btn = backBtnGO.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = backBtnGO.GetComponent<Image>();
            var cols = btn.colors;
            cols.normalColor      = new Color(0.10f, 0.20f, 0.55f, 0.95f);
            cols.highlightedColor = new Color(0.22f, 0.38f, 0.78f, 1.00f);
            cols.pressedColor     = new Color(0.06f, 0.12f, 0.38f, 1.00f);
            btn.colors = cols;
            btn.onClick.AddListener(() => experimentManager.ShowMainMenu());
        }

        // ── Footer hint — sits just above the back button ─────────────────────
        float footerY0 = backBtnY1 + BACK_BTN_GAP;
        float footerY1 = footerY0 + FOOTER_H_PX;

        var footerTxt = MakeTextGO(cvGO.transform, "Footer",
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(PADDING_PX, footerY0), new Vector2(-PADDING_PX, footerY1),
            FONT_SUMMARY, TextAlignmentOptions.Center);
        footerTxt.text =
            "<color=#69F0AE>✓ Papers reset after each dip — repeat the test any time!</color>";

        Debug.Log($"[LitmusDashboardUI] Board built ({boardWidthMetre}m × {boardHeightMetre}m) with back button.");
    }

    // ── Camera alignment ──────────────────────────────────────────────────────

    private static void FaceCamera(Transform root)
    {
        Camera cam = Camera.main ?? Object.FindFirstObjectByType<Camera>();
        if (cam == null) return;

        Vector3 toCamera = cam.transform.position - root.position;
        toCamera.y = 0f;
        if (toCamera.sqrMagnitude < 0.01f) return;

        // Point +Z away from the camera so the Canvas front (local +Z) faces the player.
        root.rotation = Quaternion.LookRotation(-toCamera.normalized);
    }

    // ── UI Helpers ────────────────────────────────────────────────────────────

    /// <summary>Creates a stretched Image using anchor-based offsets (from top-left).</summary>
    private static GameObject MakeImage(Transform parent, string goName,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        go.AddComponent<Image>().color = color;
        return go;
    }

    /// <summary>Creates a stretched Image using from-top offsets for vertical layout.</summary>
    private static GameObject MakeImageAnchored(Transform parent, string goName,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        return MakeImage(parent, goName, anchorMin, anchorMax, offsetMin, offsetMax, color);
    }

    /// <summary>Creates a TextMeshProUGUI element with stretched anchors.</summary>
    private static TextMeshProUGUI MakeTextGO(Transform parent, string goName,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax,
        float fontSize, TextAlignmentOptions align)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize           = fontSize;
        tmp.alignment          = align;
        tmp.richText           = true;
        tmp.enableWordWrapping = true;
        tmp.overflowMode       = TextOverflowModes.Truncate;
        tmp.color              = new Color(0.88f, 0.93f, 1.00f, 1f);
        return tmp;
    }
}
