using UnityEngine;

/// Positions both litmus papers on the lab bench near the beakers at startup.
/// Papers float in place (no gravity) until the player grabs them.
///
/// SETUP:
///   1. Attach to any empty manager GameObject (e.g. the LitmusTestSetup root).
///   2. Drag BlueLitmusPaper and PinkLitmusPaper into the inspector fields.
///   3. Drag any one of the three beaker Transforms into "benchReference".
///   4. Adjust offsets in the Inspector — defaults put papers to the RIGHT of the bench
///      at a comfortable standing-height grab position (~0.05 m above bench surface).
///   5. Enter Play — papers snap to their positions automatically.
public class LitmusPaperPositioner : MonoBehaviour
{
    [Header("Paper GameObjects")]
    public GameObject blueLitmusPaper;
    public GameObject pinkLitmusPaper;

    [Header("Bench Reference (any beaker Transform)")]
    [Tooltip("Papers are placed relative to this Transform.")]
    public Transform benchReference;

    [Header("Offset from Bench Reference (local to benchReference)")]
    [Tooltip("Blue paper offset from benchReference.")]
    public Vector3 blueOffset  = new Vector3( 0.18f, 0.05f, 0f);
    [Tooltip("Pink paper offset from benchReference.")]
    public Vector3 pinkOffset  = new Vector3(-0.18f, 0.05f, 0f);

    [Header("Paper Rotation (Euler — Z up = paper stands vertically)")]
    public Vector3 paperEuler = new Vector3(0f, 0f, 0f);

    private void Start()
    {
        PlacePaper(blueLitmusPaper, blueOffset);
        PlacePaper(pinkLitmusPaper, pinkOffset);
    }

    private void PlacePaper(GameObject paper, Vector3 localOffset)
    {
        if (paper == null)
        {
            Debug.LogWarning("[LitmusPaperPositioner] Paper reference not assigned — skipping.");
            return;
        }

        if (benchReference == null)
        {
            Debug.LogWarning("[LitmusPaperPositioner] benchReference not assigned — papers not repositioned.");
            return;
        }

        // Convert local offset to world space using the bench reference's orientation.
        Vector3 worldPos = benchReference.TransformPoint(localOffset);
        paper.transform.position    = worldPos;
        paper.transform.eulerAngles = paperEuler;

        // Ensure Rigidbody doesn't make it fall after repositioning.
        var rb = paper.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Debug.Log($"[LitmusPaperPositioner] Placed '{paper.name}' at {worldPos}.");
    }
}
