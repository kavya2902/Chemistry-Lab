using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// World-space Canvas panel that shows litmus test results ("Acid is Detected" etc.).
/// Assign the same instance to BOTH LitmusPaperController.resultDisplayUI fields
/// so both papers write to one shared display.
///
/// SETUP:
///   1. Create an empty GameObject → name it "LitmusResultPanel".
///   2. Position it near the lab bench (e.g. mounted on a wall or shelf at eye level).
///   3. Attach this script — the Canvas is built automatically at runtime.
///   4. Drag it into LitmusPaperController.resultDisplayUI on BlueLitmusPaper
///      AND on PinkLitmusPaper.
public class LitmusResultDisplayUI : MonoBehaviour
{
    [Header("Panel Size (metres)")]
    public Vector2 panelSizeMetre = new Vector2(0.55f, 0.20f);

    [Header("Text")]
    [Range(8f, 48f)]
    public float fontSize = 26f;

    [Header("Billboard — auto-face player camera")]
    public bool faceCamera = true;

    private CanvasGroup     _group;
    private TextMeshProUGUI _resultText;
    private Coroutine       _routine;
    private bool            _built;

    // ── Build ─────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        if (!_built) { BuildUI(); _built = true; }
    }

    private void BuildUI()
    {
        // Child GO holds the Canvas so the root transform stays a clean anchor point.
        var cvGO = new GameObject("ResultCanvas");
        cvGO.transform.SetParent(transform, false);
        cvGO.transform.localScale = Vector3.one * 0.001f; // 1 px = 1 mm in world space

        var canvas = cvGO.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        cvGO.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;

        _group                = cvGO.AddComponent<CanvasGroup>();
        _group.alpha          = 0f;   // starts invisible
        _group.interactable   = false;
        _group.blocksRaycasts = false;

        var rt = cvGO.GetComponent<RectTransform>();
        rt.sizeDelta = panelSizeMetre * 1000f; // metres → pixels (at 0.001 scale = metres)

        // Dark glass background
        MakeImage(cvGO.transform,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0.05f, 0.07f, 0.18f, 0.94f));

        // Blue accent bar on the left
        MakeImage(cvGO.transform,
            new Vector2(0f, 0f), new Vector2(0f, 1f),
            Vector2.zero, new Vector2(8f, 0f),
            new Color(0.28f, 0.73f, 1.00f, 1f));

        // Result text
        var txtGO = new GameObject("ResultText");
        txtGO.transform.SetParent(cvGO.transform, false);
        _resultText = txtGO.AddComponent<TextMeshProUGUI>();
        _resultText.fontSize           = fontSize;
        _resultText.fontStyle          = FontStyles.Bold;
        _resultText.alignment          = TextAlignmentOptions.Center;
        _resultText.enableWordWrapping = false;
        _resultText.color              = Color.white;
        var txtRt = txtGO.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = new Vector2(14f, 0f); // indent past the accent bar
        txtRt.offsetMax = Vector2.zero;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void ShowResult(string message, Color textColor, float displayDuration)
    {
        if (_resultText == null) return;
        _resultText.text  = message;
        _resultText.color = textColor;
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(ShowAndFade(displayDuration));
    }

    // ── Fade coroutine ────────────────────────────────────────────────────────

    private IEnumerator ShowAndFade(float duration)
    {
        _group.alpha = 1f;

        float holdTime = duration * 0.65f;
        float fadeTime = duration - holdTime;
        yield return new WaitForSeconds(holdTime);

        float elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed     += Time.deltaTime;
            _group.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeTime);
            yield return null;
        }
        _group.alpha = 0f;
    }

    // ── Billboard ─────────────────────────────────────────────────────────────

    private void LateUpdate()
    {
        if (!faceCamera) return;
        var cam = Camera.main;
        if (cam == null) return;
        transform.LookAt(cam.transform.position);
        transform.Rotate(0f, 180f, 0f);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static void MakeImage(Transform parent,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        var go = new GameObject("BG");
        go.transform.SetParent(parent, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = anchorMin; r.anchorMax = anchorMax;
        r.offsetMin = offsetMin; r.offsetMax = offsetMax;
        go.AddComponent<Image>().color = color;
    }
}
