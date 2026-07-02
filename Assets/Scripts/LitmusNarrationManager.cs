using System.Collections;
using UnityEngine;

/// Centralized audio narration controller for the Litmus Paper Test experiment.
///
/// SETUP
///   1. Add an empty GameObject inside the litmusTestSetup group → name it "LitmusAudioManager".
///   2. Attach this script — AudioSource is added automatically.
///   3. Assign all 10 AudioClip slots in the Inspector.
///      IMPORTANT: hover over each slot to see the exact filename that belongs there.
///   4. Drag this GameObject into LitmusPaperController.narrationManager on BOTH papers.
///   5. (Optional) Drag it into LitmusResetButton.narrationManager.
///
/// NARRATION SEQUENCE
///   Scene activates   →  welcome  →  pick_blue
///   First blue dip    →  blue_<result>  →  pick_pink
///   Pink+Base reacts  →  pink_base  →  completed   (only after BOTH papers colour-changed)
///   Blue+Acid reacts  →  blue_acid  →  completed   (if pink already reacted)
///   Any re-dip        →  reaction clip only, no follow-up
[RequireComponent(typeof(AudioSource))]
public class LitmusNarrationManager : MonoBehaviour
{
    // ── Inspector fields ──────────────────────────────────────────────────────

    [Header("Sequence Clips")]
    [Tooltip("← assign: welcome.mp3")]
    public AudioClip welcome;
    [Tooltip("← assign: pick_blue.mp3  (plays after welcome)")]
    public AudioClip pickBlue;
    [Tooltip("← assign: pick_pink.mp3  (plays after first blue-paper dip)")]
    public AudioClip pickPink;
    [Tooltip("← assign: completed.mp3  (plays only after BOTH papers have colour-changed)")]
    public AudioClip completed;

    [Header("Blue Paper Reaction Clips")]
    [Tooltip("← assign: blue_acid.mp3    Blue paper + Acid  → colour changes to Red  → 'Acid Detected'")]
    public AudioClip blueAcid;
    [Tooltip("← assign: blue_base.mp3    Blue paper + Base  → no colour change        → 'No Change'")]
    public AudioClip blueBase;
    [Tooltip("← assign: blue_neutral.mp3 Blue paper + Neutral → no colour change      → 'Neutral Solution'")]
    public AudioClip blueNeutral;

    [Header("Pink Paper Reaction Clips")]
    // NOTE: audio files are alphabetically  pink_acid → pink_base → pink_neutral
    // Assign them in that same order to avoid accidentally swapping Base/Acid.
    [Tooltip("← assign: pink_acid.mp3    Pink paper + Acid  → no colour change         → 'No Change'")]
    public AudioClip pinkAcid;
    [Tooltip("← assign: pink_base.mp3    Pink paper + Base  → colour changes to Blue   → 'Base Detected'")]
    public AudioClip pinkBase;
    [Tooltip("← assign: pink_neutral.mp3 Pink paper + Neutral → no colour change       → 'Neutral Solution'")]
    public AudioClip pinkNeutral;

    [Header("Timing")]
    [Tooltip("Silent gap between two chained clips (seconds).")]
    [Range(0f, 1f)]
    public float gapBetweenClips = 0.05f;

    // ── Runtime state ─────────────────────────────────────────────────────────

    private AudioSource _source;
    private Coroutine   _sequenceRoutine;

    // Tracks first blue dip (any type) → gates pick_pink
    private bool _blueDipped;

    // Tracks whether EACH paper has had its reactive colour-change dip.
    // completed only fires when BOTH are true.
    private bool _blueReacted;   // Blue + Acid dip occurred
    private bool _pinkReacted;   // Pink + Base dip occurred

    // Cooldown prevents audio overlap from any lingering double-fire edge case.
    private float _lastDipTime = -10f;
    private const float DIP_COOLDOWN = 0.25f;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _source              = GetComponent<AudioSource>();
        _source.spatialBlend = 0f;   // 2D — flat audio, no positional falloff
        _source.playOnAwake  = false;
        _source.loop         = false;
        _source.volume       = 1f;

        ValidateClips();
    }

    private void OnEnable()
    {
        // Fires every time ExperimentManager.StartLitmusTest() re-activates this group.
        ResetAndBegin();
    }

    private void OnDisable()
    {
        if (_sequenceRoutine != null) StopCoroutine(_sequenceRoutine);
        _source.Stop();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by LitmusPaperController on every valid dip event.
    /// Stops any in-progress sequence, plays the correct reaction clip,
    /// then chains pick_pink or completed when the conditions are met.
    /// </summary>
    public void OnPaperDipped(LitmusPaperController.LitmusType paperType,
                               BeakerSolutionType.SolutionType solutionType)
    {
        // Short cooldown prevents audio overlap if two trigger paths fire
        // within the same physics/update frame for the same dip event.
        if (Time.time - _lastDipTime < DIP_COOLDOWN) return;
        _lastDipTime = Time.time;

        if (_sequenceRoutine != null) StopCoroutine(_sequenceRoutine);

        AudioClip dipClip    = ResolveDipClip(paperType, solutionType);
        AudioClip followClip = ResolveFollowClip(paperType, solutionType);

        _sequenceRoutine = StartCoroutine(PlaySequence(dipClip, followClip));
    }

    /// <summary>
    /// Resets all session flags and replays welcome → pick_blue.
    /// Called automatically on OnEnable and by LitmusResetButton.
    /// </summary>
    public void ResetAndBegin()
    {
        if (_sequenceRoutine != null) StopCoroutine(_sequenceRoutine);
        _source.Stop();

        _blueDipped  = false;
        _blueReacted = false;
        _pinkReacted = false;
        _lastDipTime = -10f;

        _sequenceRoutine = StartCoroutine(PlaySequence(welcome, pickBlue));
    }

    // ── Clip resolution ───────────────────────────────────────────────────────

    private AudioClip ResolveDipClip(LitmusPaperController.LitmusType paper,
                                      BeakerSolutionType.SolutionType solution)
    {
        if (paper == LitmusPaperController.LitmusType.Blue)
        {
            switch (solution)
            {
                case BeakerSolutionType.SolutionType.Acid:    return blueAcid;
                case BeakerSolutionType.SolutionType.Base:    return blueBase;
                default:                                       return blueNeutral;
            }
        }
        else // Pink
        {
            switch (solution)
            {
                case BeakerSolutionType.SolutionType.Base:    return pinkBase;
                case BeakerSolutionType.SolutionType.Acid:    return pinkAcid;
                default:                                       return pinkNeutral;
            }
        }
    }

    /// <summary>
    /// Returns the clip to chain AFTER the dip clip, and advances state flags.
    ///
    /// pick_pink  → plays after the very first blue dip (any beaker)
    /// completed  → plays only after Blue+Acid AND Pink+Base have BOTH occurred
    ///              (i.e. both papers have colour-changed at least once)
    /// </summary>
    private AudioClip ResolveFollowClip(LitmusPaperController.LitmusType paperType,
                                         BeakerSolutionType.SolutionType solutionType)
    {
        bool isReactive = IsReactiveDip(paperType, solutionType);

        if (paperType == LitmusPaperController.LitmusType.Blue)
        {
            // Track whether blue has had its reactive (colour-change) dip.
            if (isReactive && !_blueReacted)
            {
                _blueReacted = true;
                // If pink already colour-changed → both done → play completed.
                if (_pinkReacted) return completed;
            }

            // First dip of any kind → guide user to pick up the pink paper.
            if (!_blueDipped)
            {
                _blueDipped = true;
                return pickPink;
            }
        }
        else // Pink
        {
            // Track whether pink has had its reactive (colour-change) dip.
            if (isReactive && !_pinkReacted)
            {
                _pinkReacted = true;
                // If blue already colour-changed → both done → play completed.
                if (_blueReacted) return completed;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns true only for the two paper+solution combos that cause a colour change:
    ///   Blue  + Acid  →  paper turns Red   (reactive)
    ///   Pink  + Base  →  paper turns Blue  (reactive)
    /// All other combos leave the paper unchanged (non-reactive).
    /// </summary>
    private static bool IsReactiveDip(LitmusPaperController.LitmusType paper,
                                       BeakerSolutionType.SolutionType solution)
    {
        return (paper == LitmusPaperController.LitmusType.Blue &&
                solution == BeakerSolutionType.SolutionType.Acid)
            || (paper == LitmusPaperController.LitmusType.Pink &&
                solution == BeakerSolutionType.SolutionType.Base);
    }

    // ── Coroutine sequencing ──────────────────────────────────────────────────

    private IEnumerator PlaySequence(AudioClip first, AudioClip second)
    {
        if (first != null)
        {
            PlayClip(first);
            yield return new WaitForSeconds(first.length + gapBetweenClips);
        }

        if (second != null)
            PlayClip(second);
    }

    // ── Playback ──────────────────────────────────────────────────────────────

    private void PlayClip(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("[LitmusNarration] Null clip — check Inspector assignments on LitmusAudioManager.");
            return;
        }
        _source.Stop();
        _source.clip = clip;
        _source.Play();
        Debug.Log($"[LitmusNarration] ▶ {clip.name}");
    }

    // ── Startup validation ────────────────────────────────────────────────────

    /// <summary>
    /// Checks each Inspector slot against the expected filename.
    /// Logs a warning for any mismatch so swapped assignments are caught immediately.
    /// Common mistake: pink_acid and pink_base are alphabetically adjacent and easy to swap.
    /// </summary>
    private void ValidateClips()
    {
        Validate(welcome,     "welcome");
        Validate(pickBlue,    "pick_blue");
        Validate(pickPink,    "pick_pink");
        Validate(completed,   "completed");
        Validate(blueAcid,    "blue_acid");
        Validate(blueBase,    "blue_base");
        Validate(blueNeutral, "blue_neutral");
        Validate(pinkAcid,    "pink_acid");
        Validate(pinkBase,    "pink_base");
        Validate(pinkNeutral, "pink_neutral");
    }

    private void Validate(AudioClip clip, string expectedName)
    {
        if (clip == null)
        {
            Debug.LogWarning($"[LitmusNarration] Slot '{expectedName}' is empty — assign {expectedName}.mp3");
            return;
        }
        if (!clip.name.ToLower().Replace("-", "_").Contains(expectedName))
            Debug.LogWarning($"[LitmusNarration] Slot '{expectedName}' has '{clip.name}' assigned. " +
                             $"Expected a clip whose name contains '{expectedName}'. " +
                             $"Check for swapped assignments in the Inspector.");
    }
}
