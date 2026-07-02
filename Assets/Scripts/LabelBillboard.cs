using UnityEngine;

/// <summary>
/// Makes a label always face the main camera (billboard effect).
/// Rotates only on the Y-axis to keep the text upright, unless fullBillboard is enabled.
/// </summary>
public class LabelBillboard : MonoBehaviour
{
    [Tooltip("When true, the label fully faces the camera (all axes). When false, only Y-axis rotation is applied.")]
    public bool fullBillboard = true;

    private Transform _cameraTransform;

    private void Start()
    {
        if (Camera.main != null)
            _cameraTransform = Camera.main.transform;
    }

    private void LateUpdate()
    {
        if (_cameraTransform == null)
        {
            if (Camera.main != null)
                _cameraTransform = Camera.main.transform;
            return;
        }

        if (fullBillboard)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - _cameraTransform.position);
        }
        else
        {
            Vector3 direction = transform.position - _cameraTransform.position;
            direction.y = 0f;
            if (direction != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(direction);
        }
    }
}
