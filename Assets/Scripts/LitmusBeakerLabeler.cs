using UnityEngine;

/// Manages floating label cards above the three litmus-test beakers.
/// Labels show "Beaker 1 / 2 / 3" — the chemical content is intentionally hidden
/// so students identify acid / base / neutral by testing with litmus paper.
///
/// ATTACH TO: any empty GameObject in the Litmus scene.
/// INSPECTOR:  Drag each beaker root Transform into Beaker A / B / C.
///
/// Awake() removes any old "Acid / Base / Neutral" labels left in the scene
/// before spawning the correct "Beaker 1 / 2 / 3" cards.
public class LitmusBeakerLabeler : MonoBehaviour
{
    [Header("Beaker Transforms (drag root GameObjects here)")]
    [Tooltip("Beaker shown as 'Beaker 1' (actual content hidden from student).")]
    public Transform beakerA;

    [Tooltip("Beaker shown as 'Beaker 2'.")]
    public Transform beakerB;

    [Tooltip("Beaker shown as 'Beaker 3'.")]
    public Transform beakerC;

    [Header("Label Height")]
    public float labelHeight = 0.30f;

    // All three beakers use the same neutral colour — no visual hints given.
    private static readonly Color BeakerColor = new Color(0.72f, 0.80f, 0.90f, 1f);

    // Old label GameObject names to destroy on Awake (catches anything left in the scene).
    private static readonly string[] OldLabelNames =
    {
        "Acid", "Base", "Neutral",
        "Label_Acid", "Label_Base", "Label_Neutral",
        "AcidLabel", "BaseLabel", "NeutralLabel",
        "Acid Label", "Base Label", "Neutral Label",
        "Label_Beaker1", "Label_Beaker2", "Label_Beaker3",
    };

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        RemoveOldLabels();
        AnchorBeaker(beakerA, "Beaker 1");
        AnchorBeaker(beakerB, "Beaker 2");
        AnchorBeaker(beakerC, "Beaker 3");
    }

    private void Start()
    {
        Spawn(beakerA, "Beaker 1");
        Spawn(beakerB, "Beaker 2");
        Spawn(beakerC, "Beaker 3");
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    private static void RemoveOldLabels()
    {
        // Remove by component type (catches runtime-spawned labels).
        foreach (var c in FindObjectsByType<ChemicalLabel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c != null) Destroy(c.gameObject);
        foreach (var c in FindObjectsByType<LabelBillboard>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c != null) Destroy(c.gameObject);
        foreach (var c in FindObjectsByType<LabelFollower>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c != null) Destroy(c.gameObject);

        // Remove by known name (catches manually placed label GameObjects).
        foreach (var n in OldLabelNames)
        {
            var go = GameObject.Find(n);
            if (go != null)
            {
                Debug.Log($"[LitmusBeakerLabeler] Removing old label: '{n}'");
                Destroy(go);
            }
        }
    }

    // ── Beaker Anchor ─────────────────────────────────────────────────────────

    private static void AnchorBeaker(Transform t, string label)
    {
        if (t == null) return;
        var rb = t.GetComponentInChildren<Rigidbody>();
        if (rb == null) return;
        rb.isKinematic = true;
        rb.useGravity  = false;
        rb.constraints = RigidbodyConstraints.FreezeAll;
        Debug.Log($"[LitmusBeakerLabeler] Anchored {label}.");
    }

    // ── Label Spawn ───────────────────────────────────────────────────────────

    private void Spawn(Transform target, string beakerName)
    {
        if (target == null)
        {
            Debug.LogWarning($"[LitmusBeakerLabeler] {beakerName} Transform not assigned — label skipped.");
            return;
        }

        var labelGO = new GameObject("Label_" + beakerName.Replace(" ", ""));
        labelGO.transform.position = target.position + Vector3.up * labelHeight;

        var label          = labelGO.AddComponent<ChemicalLabel>();
        label.target       = target;
        label.heightOffset = labelHeight;
        label.objectName   = beakerName;          // "Beaker 1" / "Beaker 2" / "Beaker 3"
        label.containsText = "Unknown Solution";  // content deliberately hidden
        label.formulaText  = "";
        label.roleTag      = "?";                 // mystery badge — no chemical name shown
        label.accentColor  = BeakerColor;
        label.solutionColor = BeakerColor;
    }
}
