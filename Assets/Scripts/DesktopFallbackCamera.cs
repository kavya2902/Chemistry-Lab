using UnityEngine;
using UnityEngine.XR;

/// Activates a plain desktop camera when no XR headset is connected.
/// Attach to any always-active GameObject in the scene.
public class DesktopFallbackCamera : MonoBehaviour
{
    [Tooltip("Position of the fallback camera (eye height in front of the lab).")]
    public Vector3 cameraPosition = new Vector3(0f, 1.6f, 0f);
    public Vector3 cameraRotation = new Vector3(0f, 0f, 0f);

    void Start()
    {
        // XRSettings.isDeviceActive is false when no headset is initialised.
        if (!XRSettings.isDeviceActive)
        {
            var go = new GameObject("DesktopCamera");
            go.tag = "MainCamera";
            go.transform.position    = cameraPosition;
            go.transform.eulerAngles = cameraRotation;

            var cam = go.AddComponent<Camera>();
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane  = 500f;
            cam.fieldOfView   = 70f;

            // Simple WASD + mouse-look so the user can look around
            go.AddComponent<DesktopCameraController>();

            Debug.Log("[DesktopFallbackCamera] No XR device found — desktop camera activated.");
        }
    }
}
