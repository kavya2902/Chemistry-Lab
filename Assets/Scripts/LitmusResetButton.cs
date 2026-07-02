using UnityEngine;

/// Attach to any GameObject with a Collider (Is Trigger = TRUE).
/// When the player's hand/controller touches it, all litmus papers in the
/// scene are reset to their original colour and can be used again.
///
/// SETUP:
///   1. Create a Button mesh in the scene (e.g. a small cube).
///   2. Add a Box Collider → set Is Trigger = TRUE.
///   3. Attach this script.
///   4. Optionally assign specific papers to 'papersToReset'; leave empty to
///      auto-find every LitmusPaperController in the scene.
public class LitmusResetButton : MonoBehaviour
{
    [Header("Papers to Reset (leave empty = auto-find all)")]
    public LitmusPaperController[] papersToReset;

    [Header("Narration (optional)")]
    [Tooltip("Assign the scene's LitmusNarrationManager to restart narration on reset.")]
    public LitmusNarrationManager narrationManager;

    [Header("Cooldown between presses (seconds)")]
    [Range(0.5f, 5f)]
    public float pressCooldown = 1.5f;

    [Header("Visual Feedback (optional)")]
    [Tooltip("Renderer whose colour flashes when button is pressed.")]
    public Renderer buttonRenderer;
    public Color normalColor  = new Color(0.2f, 0.6f, 1f);
    public Color pressedColor = new Color(0.1f, 1f, 0.3f);
    public float flashDuration = 0.3f;

    private float _lastPressTime = -999f;

    private void Start()
    {
        if (buttonRenderer != null)
            buttonRenderer.material.color = normalColor;

        if (papersToReset == null || papersToReset.Length == 0)
            papersToReset = FindObjectsOfType<LitmusPaperController>();
    }

    // Triggered when a hand/controller (or any collider) enters the button zone
    private void OnTriggerEnter(Collider other)
    {
        if (Time.time - _lastPressTime < pressCooldown) return;
        _lastPressTime = Time.time;

        ResetAllPapers();

        if (buttonRenderer != null)
            StartCoroutine(FlashButton());
    }

    private void ResetAllPapers()
    {
        // Re-collect in case papers were added at runtime
        if (papersToReset == null || papersToReset.Length == 0)
            papersToReset = FindObjectsOfType<LitmusPaperController>();

        foreach (var paper in papersToReset)
        {
            if (paper != null)
                paper.ResetPaper();
        }

        // Restart narration from welcome so the user hears the full sequence again.
        narrationManager?.ResetAndBegin();

        Debug.Log("[LitmusResetButton] Reset " + papersToReset.Length + " litmus paper(s).");
    }

    private System.Collections.IEnumerator FlashButton()
    {
        buttonRenderer.material.color = pressedColor;
        yield return new WaitForSeconds(flashDuration);
        buttonRenderer.material.color = normalColor;
    }
}
