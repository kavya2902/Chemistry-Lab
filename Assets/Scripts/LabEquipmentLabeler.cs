using UnityEngine;

/// <summary>
/// Manages all floating chemical labels in the titration scene.
///
/// CLEANUP (Awake):
///   Destroys every GameObject that carries a ChemicalLabel, LabelBillboard,
///   or LabelFollower component, plus the four known old label GameObjects
///   (Label_Beaker, Label_Burette, Label_ConicalFlask, Label_Pipette).
///
/// SPAWN (Start):
///   Creates three correct, colour-coded label cards:
///     Beaker        — Starch Solution     (C₆H₁₀O₅)  [INDICATOR]  blue-purple
///     Conical Flask — Iodine Solution     (I₂)        [ANALYTE]    amber
///     Burette       — Sodium Thiosulfate  (Na₂S₂O₃)  [TITRANT]    teal
///
/// AUTO-DETECTION (runs if a field is left unassigned in the Inspector):
///   Beaker        → BeakerLiquid component's parent transform
///   Conical Flask → ConicalFlask GameObject by name
///   Burette       → Burette GameObject by name
///
/// To override, drag the correct root Transforms into the Inspector fields.
/// </summary>
public class LabEquipmentLabeler : MonoBehaviour
{
    [Header("Equipment Transforms  (leave empty to auto-detect)")]
    [Tooltip("Root Transform of the Beaker (contains Starch Solution). Auto-detected via BeakerLiquid component if empty.")]
    public Transform beaker;

    [Tooltip("Root Transform of the Conical Flask (contains Iodine Solution). Auto-detected by name 'ConicalFlask' if empty.")]
    public Transform conicalFlask;

    [Tooltip("Root Transform of the Burette (contains Sodium Thiosulfate). Auto-detected by name 'Burette' if empty.")]
    public Transform burette;

    [Header("Label Height Offsets (metres above object pivot)")]
    public float beakerHeight  = 0.30f;
    public float flaskHeight   = 0.38f;
    public float buretteHeight = 0.60f;

    // ── Accent colours per chemical ───────────────────────────────────────────
    // Blue-purple: starch solution turns blue in presence of iodine
    private static readonly Color AccentBeaker  = new Color(0.47f, 0.35f, 0.98f, 1f);
    // Amber/brown: iodine's natural colour
    private static readonly Color AccentFlask   = new Color(0.93f, 0.60f, 0.12f, 1f);
    // Teal: analytical titrant
    private static readonly Color AccentBurette = new Color(0.15f, 0.80f, 0.72f, 1f);

    // ── Solution indicator dot colours ────────────────────────────────────────
    // Starch solution is near-colourless but turns deep blue; show as soft blue
    private static readonly Color DotBeaker  = new Color(0.45f, 0.55f, 1.00f, 1f);
    // Iodine solution is dark amber-brown
    private static readonly Color DotFlask   = new Color(0.80f, 0.52f, 0.04f, 1f);
    // Sodium thiosulfate is colourless — show as pale cyan
    private static readonly Color DotBurette = new Color(0.72f, 0.94f, 1.00f, 1f);

    // ── Known old-label names to destroy by name (belt-and-suspenders) ────────
    private static readonly string[] OldLabelNames =
    {
        "Label_Beaker", "Label_Burette", "Label_ConicalFlask",
        "Label_Pipette", "Label_Beaker(Clone)", "Label_Burette(Clone)",
        "Label_ConicalFlask(Clone)", "Label_Pipette(Clone)"
    };

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        RemoveOldLabels();
        AutoDetectEquipment();
    }

    private void Start()
    {
        // Labels disabled — Awake() already removed any existing ones.
    }

    // ── Auto-detection ────────────────────────────────────────────────────────

    private void AutoDetectEquipment()
    {
        if (beaker == null)
        {
            var bl = FindFirstObjectByType<BeakerLiquid>();
            if (bl != null)
            {
                // Use the parent of Beaker_liquid — that is the grabbable beaker mesh root
                beaker = bl.transform.parent != null ? bl.transform.parent : bl.transform;
                Debug.Log($"[LabEquipmentLabeler] Auto-detected Beaker as '{beaker.name}'");
            }
            else
            {
                Debug.LogWarning("[LabEquipmentLabeler] Could not auto-detect Beaker — assign it in the Inspector.");
            }
        }

        if (conicalFlask == null)
        {
            var go = GameObject.Find("ConicalFlask");
            if (go != null)
            {
                conicalFlask = go.transform;
                Debug.Log("[LabEquipmentLabeler] Auto-detected ConicalFlask.");
            }
            else
            {
                var liq = FindFirstObjectByType<ConicalFlaskLiquid>();
                if (liq != null) conicalFlask = liq.transform;
                else Debug.LogWarning("[LabEquipmentLabeler] Could not auto-detect ConicalFlask — assign it in the Inspector.");
            }
        }

        if (burette == null)
        {
            var go = GameObject.Find("Burette");
            if (go != null)
            {
                burette = go.transform;
                Debug.Log("[LabEquipmentLabeler] Auto-detected Burette.");
            }
            else
            {
                var nc = FindFirstObjectByType<BuretteNozzleController>();
                if (nc != null) burette = nc.transform;
                else Debug.LogWarning("[LabEquipmentLabeler] Could not auto-detect Burette — assign it in the Inspector.");
            }
        }
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    private static void RemoveOldLabels()
    {
        // Destroy by component type (catches runtime-spawned labels)
        foreach (var c in FindObjectsByType<ChemicalLabel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c != null) Destroy(c.gameObject);
        foreach (var c in FindObjectsByType<LabelBillboard>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c != null) Destroy(c.gameObject);
        foreach (var c in FindObjectsByType<LabelFollower>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c != null) Destroy(c.gameObject);

        // Also destroy by known name (catches scene-placed label GameObjects)
        foreach (var name in OldLabelNames)
        {
            var go = GameObject.Find(name);
            if (go != null) Destroy(go);
        }
    }

    // ── Spawn ─────────────────────────────────────────────────────────────────

    private static void Spawn(Transform target, string objName, string chemical,
        string formula, string role, Color accent, Color dotColor, float height)
    {
        if (target == null)
        {
            Debug.LogWarning($"[LabEquipmentLabeler] '{objName}' target not found — label skipped.");
            return;
        }

        GameObject go = new GameObject($"Label_{objName.Replace(" ", "")}");
        go.transform.position = target.position + Vector3.up * height;

        ChemicalLabel label = go.AddComponent<ChemicalLabel>();
        label.target        = target;
        label.heightOffset  = height;
        label.objectName    = objName;
        label.containsText  = chemical;
        label.formulaText   = formula;
        label.roleTag       = role;
        label.accentColor   = accent;
        label.solutionColor = dotColor;
    }
}
