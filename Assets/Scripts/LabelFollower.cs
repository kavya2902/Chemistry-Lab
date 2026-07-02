using UnityEngine;

/// <summary>
/// Positions this label directly above a target object in world space every frame.
/// Works correctly regardless of the target's local scale, rotation, or parent hierarchy.
/// Also billboards the label to always face the main camera.
/// </summary>
public class LabelFollower : MonoBehaviour
{
    [Tooltip("The object this label tracks. Assign the root grabbable GameObject.")]
    public Transform target;

    [Tooltip("How far above the target's world position the label floats (in meters).")]
    public float heightOffset = 0.25f;

    private Transform _cameraTransform;

    private void Start()
    {
        if (Camera.main != null)
            _cameraTransform = Camera.main.transform;

        // Snap to position immediately on start to avoid one-frame pop
        if (target != null)
            transform.position = target.position + Vector3.up * heightOffset;
    }

    private void LateUpdate()
    {
        // Re-acquire camera if it was not ready on Start (common in VR with late-initialized rigs)
        if (_cameraTransform == null && Camera.main != null)
            _cameraTransform = Camera.main.transform;

        if (target == null)
            return;

        // Always sit above target in world space — unaffected by target's scale or rotation
        transform.position = target.position + Vector3.up * heightOffset;

        // Billboard: face the camera
        if (_cameraTransform != null)
        {
            Vector3 direction = transform.position - _cameraTransform.position;
            if (direction != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(direction);
        }
    }
}
