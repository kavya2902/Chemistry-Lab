using UnityEngine;

/// <summary>
/// Prevents a lab object (beaker, flask, etc.) from falling on scene load.
/// Keeps the Rigidbody kinematic until grabbed, then releases physics so the
/// user can freely move it. When released, optionally snaps back to origin.
///
/// SETUP:
///   1. Attach to any lab object that has a Rigidbody + Meta SDK Grabbable.
///   2. On the Grabbable component, wire:
///        WhenPointerEventRaised → LabObjectAnchor.OnGrabBegin  (Select event)
///        WhenPointerEventRaised → LabObjectAnchor.OnGrabEnd    (Unselect event)
///      OR use the Grabbable's OnGrab / OnRelease Unity Events if available.
///   3. Set snapBackOnRelease = true if the object should return to its table
///      position when released (recommended for beakers/flasks).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class LabObjectAnchor : MonoBehaviour
{
    [Tooltip("When true: object snaps back to its start position when released.\n" +
             "When false: object stays wherever the user drops it.")]
    public bool snapBackOnRelease = true;

    private Rigidbody _rb;
    private Vector3   _anchorPos;
    private Quaternion _anchorRot;
    private bool _isGrabbed;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _anchorPos = transform.position;
        _anchorRot = transform.rotation;

        // Lock in place — stops the beaker falling through the table on scene load.
        // Meta SDK Grabbable moves kinematic Rigidbodies directly, so grab still works.
        _rb.isKinematic = true;
        _rb.useGravity  = false;
    }

    /// <summary>
    /// Call when the user grabs this object.
    /// Wire to: Grabbable → WhenPointerEventRaised (filter: Select)
    ///      OR  Grabbable → OnGrab Unity Event.
    /// </summary>
    public void OnGrabBegin()
    {
        if (_isGrabbed) return;
        _isGrabbed = true;
        _rb.isKinematic = false;
        _rb.useGravity  = true;
    }

    /// <summary>
    /// Call when the user releases this object.
    /// Wire to: Grabbable → WhenPointerEventRaised (filter: Unselect)
    ///      OR  Grabbable → OnRelease Unity Event.
    /// </summary>
    public void OnGrabEnd()
    {
        if (!_isGrabbed) return;
        _isGrabbed = false;

        if (snapBackOnRelease)
        {
            _rb.isKinematic = true;
            _rb.useGravity  = false;
            _rb.linearVelocity  = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(_anchorPos, _anchorRot);
        }
        // If not snapping back, leave physics enabled so it lands naturally.
    }

    /// <summary>Resets the object back to its anchor position (called on experiment reset).</summary>
    public void ResetToAnchor()
    {
        _isGrabbed = false;
        _rb.isKinematic = true;
        _rb.useGravity  = false;
        _rb.linearVelocity  = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(_anchorPos, _anchorRot);
    }
}
