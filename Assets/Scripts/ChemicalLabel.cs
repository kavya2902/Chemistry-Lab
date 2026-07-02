using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Builds an animated floating label card above a target chemistry object.
/// All visuals are created at runtime — no prefab or scene setup required.
/// Follows the target every frame (grab-safe) and billboards toward the main camera.
/// </summary>
public class ChemicalLabel : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The object this label floats above.")]
    public Transform target;
    [Tooltip("Height in metres above the target's world position.")]
    public float heightOffset = 0.35f;

    [Header("Label Content")]
    public string objectName   = "Beaker";
    public string containsText = "Starch Solution";
    public string formulaText  = "";         // e.g. "Na₂S₂O₃"
    public string roleTag      = "";         // e.g. "INDICATOR" | "ANALYTE" | "TITRANT"

    [Header("Colors")]
    [Tooltip("Accent bar and badge color — set per chemical.")]
    public Color accentColor   = new Color(0.47f, 0.35f, 0.98f, 1f);
    [Tooltip("Solution indicator dot color — matches the solution's visual color.")]
    public Color solutionColor = new Color(0.38f, 0.50f, 0.98f, 1f);

    [Header("Animation")]
    public float animDuration  = 0.65f;

    // ── Card constants ──────────────────────────────────────────────────────
    private const float CanvasScale    = 0.001f;   // 1 px = 1 mm in world space
    private const float CardWidth      = 340f;
    private const float CardHeight     = 128f;
    private const float AccentBarW     = 7f;
    private const float Pad            = 14f;
    private const float LineWidth      = 0.0028f;

    // ── Runtime refs ────────────────────────────────────────────────────────
    private CanvasGroup  _canvasGroup;
    private Canvas       _canvas;
    private LineRenderer _line;
    private Transform    _cam;
    private bool         _ready;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Start()
    {
        BuildVisuals();
        StartCoroutine(AnimateIn());
        if (Camera.main != null) _cam = Camera.main.transform;
    }

    private void LateUpdate()
    {
        if (_cam == null && Camera.main != null) _cam = Camera.main.transform;
        if (target == null) return;

        transform.position = target.position + Vector3.up * heightOffset;

        if (_cam != null)
        {
            Vector3 dir = transform.position - _cam.position;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir);
        }

        if (_ready && _line != null)
        {
            _line.SetPosition(0, transform.position - transform.up * (CardHeight * CanvasScale * 0.5f));
            _line.SetPosition(1, target.position);
        }
    }

    // ── Build ───────────────────────────────────────────────────────────────

    private void BuildVisuals()
    {
        // ── World-space canvas ──────────────────────────────────────────────
        GameObject cvGO = new GameObject("LabelCanvas");
        cvGO.transform.SetParent(transform, false);
        cvGO.transform.localScale = Vector3.one * CanvasScale;

        _canvas            = cvGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        cvGO.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;

        _canvasGroup = cvGO.AddComponent<CanvasGroup>();
        _canvasGroup.alpha          = 0f;
        _canvasGroup.interactable   = false;
        _canvasGroup.blocksRaycasts = false;

        cvGO.GetComponent<RectTransform>().sizeDelta = new Vector2(CardWidth, CardHeight);

        Transform cv = cvGO.transform;

        // ── Dark glass card ─────────────────────────────────────────────────
        Img(cv, "CardBG",
            Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero,
            new Color(0.04f, 0.06f, 0.13f, 0.93f));

        // Subtle inner glow border
        Img(cv, "CardGlow",
            Vector2.zero, Vector2.one,
            new Vector2(1f, 1f), new Vector2(-1f, -1f),
            new Color(accentColor.r, accentColor.g, accentColor.b, 0.20f));

        // Faint horizontal gradient strip at top (brightens the card header)
        Img(cv, "TopShine",
            new Vector2(0f, 0.72f), new Vector2(1f, 1f),
            new Vector2(AccentBarW, 0f), Vector2.zero,
            new Color(1f, 1f, 1f, 0.04f));

        // ── Left accent bar ─────────────────────────────────────────────────
        Img(cv, "AccentBar",
            Vector2.zero, new Vector2(0f, 1f),
            Vector2.zero, new Vector2(AccentBarW, 0f),
            accentColor);

        // Accent bar top highlight (makes the bar look 3D)
        Img(cv, "AccentHighlight",
            new Vector2(0f, 0.75f), new Vector2(0f, 1f),
            Vector2.zero, new Vector2(AccentBarW, 0f),
            new Color(1f, 1f, 1f, 0.35f));

        // ── Solution color indicator dot ────────────────────────────────────
        float dotCx = AccentBarW + Pad;
        BuildDot(cv, dotCx, -12f, 13f);

        // ── Object name (top half, bold large) ─────────────────────────────
        TMP(cv, "NameText",
            new Vector2(0f, 0.46f), Vector2.one,
            new Vector2(AccentBarW + Pad, 0f), new Vector2(-Pad, -10f),
            objectName, 28f, FontStyles.Bold,
            Color.white, TextAlignmentOptions.BottomLeft);

        // ── Divider ─────────────────────────────────────────────────────────
        Img(cv, "Divider",
            new Vector2(0f, 0.455f), new Vector2(1f, 0.455f),
            new Vector2(AccentBarW + 4f, -0.5f), new Vector2(-Pad, 0.5f),
            new Color(accentColor.r, accentColor.g, accentColor.b, 0.28f));

        // ── Contains text (bottom half) ─────────────────────────────────────
        TMP(cv, "ContainsText",
            Vector2.zero, new Vector2(1f, 0.455f),
            new Vector2(AccentBarW + Pad, 8f), new Vector2(-Pad, 0f),
            BuildContainsLine(), 18f, FontStyles.Italic,
            new Color(accentColor.r + 0.15f, accentColor.g + 0.15f, accentColor.b + 0.15f, 0.95f),
            TextAlignmentOptions.TopLeft);

        // ── Role badge pill (bottom-right) ──────────────────────────────────
        if (!string.IsNullOrEmpty(roleTag))
            BuildRoleBadge(cv);

        // ── Connector line ──────────────────────────────────────────────────
        BuildConnectorLine();

        _ready = true;
    }

    private void BuildDot(Transform parent, float x, float y, float size)
    {
        GameObject go = new GameObject("SolutionDot");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(0f, 1f);
        rt.pivot            = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta        = new Vector2(size, size);
        Image img = go.AddComponent<Image>();
        img.color = solutionColor;

        // Inner highlight dot (makes it look like a glowing bead)
        GameObject shine = new GameObject("DotShine");
        shine.transform.SetParent(go.transform, false);
        RectTransform srt = shine.AddComponent<RectTransform>();
        srt.anchorMin        = new Vector2(0.55f, 0.55f);
        srt.anchorMax        = new Vector2(0.95f, 0.95f);
        srt.offsetMin        = Vector2.zero;
        srt.offsetMax        = Vector2.zero;
        shine.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.55f);
    }

    private void BuildRoleBadge(Transform parent)
    {
        float badgeW = roleTag.Length * 8.8f + 18f;

        // Badge background pill
        GameObject bg = new GameObject("RoleBadgeBG");
        bg.transform.SetParent(parent, false);
        RectTransform bgRT = bg.AddComponent<RectTransform>();
        bgRT.anchorMin        = new Vector2(1f, 0f);
        bgRT.anchorMax        = new Vector2(1f, 0f);
        bgRT.pivot            = new Vector2(1f, 0f);
        bgRT.anchoredPosition = new Vector2(-Pad, 9f);
        bgRT.sizeDelta        = new Vector2(badgeW, 20f);
        bg.AddComponent<Image>().color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.30f);

        // Badge border
        GameObject border = new GameObject("RoleBadgeBorder");
        border.transform.SetParent(bg.transform, false);
        RectTransform bRT = border.AddComponent<RectTransform>();
        bRT.anchorMin = Vector2.zero;
        bRT.anchorMax = Vector2.one;
        bRT.offsetMin = new Vector2(1f, 1f);
        bRT.offsetMax = new Vector2(-1f, -1f);
        border.AddComponent<Image>().color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.55f);

        // Badge text
        GameObject txt = new GameObject("RoleBadgeText");
        txt.transform.SetParent(bg.transform, false);
        RectTransform tRT = txt.AddComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero;
        tRT.anchorMax = Vector2.one;
        tRT.offsetMin = new Vector2(6f, 2f);
        tRT.offsetMax = new Vector2(-6f, -2f);
        TextMeshProUGUI label = txt.AddComponent<TextMeshProUGUI>();
        label.text      = roleTag;
        label.fontSize  = 11f;
        label.fontStyle = FontStyles.Bold;
        label.color     = new Color(accentColor.r + 0.2f, accentColor.g + 0.2f, accentColor.b + 0.2f, 1f);
        label.alignment = TextAlignmentOptions.Center;
        label.richText  = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.enableWordWrapping = false;
    }

    private string BuildContainsLine()
    {
        if (!string.IsNullOrEmpty(formulaText))
            return $"Contains: {containsText}\n<color=#FFE880><b>{formulaText}</b></color>";
        return $"Contains: {containsText}";
    }

    private void BuildConnectorLine()
    {
        _line = gameObject.AddComponent<LineRenderer>();
        _line.positionCount     = 2;
        _line.useWorldSpace     = true;
        _line.startWidth        = LineWidth;
        _line.endWidth          = LineWidth * 0.15f;
        _line.numCapVertices    = 4;
        _line.numCornerVertices = 4;

        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3000;
        _line.material = mat;

        _line.startColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.75f);
        _line.endColor   = new Color(accentColor.r, accentColor.g, accentColor.b, 0f);
        _line.SetPosition(0, Vector3.zero);
        _line.SetPosition(1, Vector3.zero);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void Img(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        go.AddComponent<Image>().color = color;
    }

    private static void TMP(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax,
        string text, float fontSize, FontStyles style,
        Color color, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text                 = text;
        tmp.fontSize             = fontSize;
        tmp.fontStyle            = style;
        tmp.color                = color;
        tmp.alignment            = align;
        tmp.richText             = true;
        tmp.overflowMode         = TextOverflowModes.Overflow;
        tmp.enableWordWrapping   = false;
    }

    // ── Entry animation ──────────────────────────────────────────────────────

    private IEnumerator AnimateIn()
    {
        float elapsed = 0f;
        Transform cvT  = _canvas.transform;
        Vector3 fullSc = Vector3.one * CanvasScale;

        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            float t = EaseOutBack(Mathf.Clamp01(elapsed / animDuration));
            _canvasGroup.alpha  = Mathf.Clamp01(elapsed / (animDuration * 0.5f));
            cvT.localScale      = fullSc * Mathf.Lerp(0.15f, 1f, t);
            yield return null;
        }

        _canvasGroup.alpha = 1f;
        cvT.localScale     = fullSc;
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
