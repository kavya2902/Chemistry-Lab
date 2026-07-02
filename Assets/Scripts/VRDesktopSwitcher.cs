using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Automatically enables the VR camera rig when a head-mounted display is
/// detected, and otherwise falls back to the flat-screen desktop preview
/// camera. This removes the need to manually toggle the two setups.
/// </summary>
[DefaultExecutionOrder(ExecutionOrder)]
public class VRDesktopSwitcher : MonoBehaviour
{
    /// <summary>Runs well before default scripts so exactly one camera is active on the first frame.</summary>
    private const int ExecutionOrder = -1000;

    [Tooltip("Root GameObject of the OVR Camera Rig used for VR playback.")]
    [SerializeField] private GameObject vrRig;

    [Tooltip("Desktop preview camera used when no headset is present.")]
    [SerializeField] private GameObject desktopCamera;

    [Tooltip("Seconds to keep polling for a headset before committing to desktop mode.")]
    [SerializeField] private float detectionTimeout = 2f;

    private readonly List<InputDevice> _headDevices = new List<InputDevice>();

    private void Awake()
    {
        // Commit an initial decision immediately so we never start with both active.
        ApplyMode(IsHeadsetPresent());
    }

    private void Start()
    {
        // XR subsystems can take a moment to report the headset, so re-check briefly.
        if (!IsHeadsetPresent())
        {
            StartCoroutine(PollForHeadset());
        }
    }

    /// <summary>Polls for a headset for a short window, then settles on the final mode.</summary>
    private IEnumerator PollForHeadset()
    {
        float elapsed = 0f;
        while (elapsed < detectionTimeout)
        {
            if (IsHeadsetPresent())
            {
                ApplyMode(true);
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        ApplyMode(false);
    }

    /// <summary>Returns true when an active head-mounted display is connected.</summary>
    private bool IsHeadsetPresent()
    {
        if (XRSettings.isDeviceActive)
        {
            return true;
        }

        _headDevices.Clear();
        InputDevices.GetDevicesAtXRNode(XRNode.Head, _headDevices);
        for (int i = 0; i < _headDevices.Count; i++)
        {
            if (_headDevices[i].isValid)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Enables the VR rig or the desktop camera based on headset availability.</summary>
    /// <param name="useVr">True to activate the VR rig, false to activate the desktop camera.</param>
    private void ApplyMode(bool useVr)
    {
        if (vrRig != null && vrRig.activeSelf != useVr)
        {
            vrRig.SetActive(useVr);
        }

        if (desktopCamera != null && desktopCamera.activeSelf == useVr)
        {
            desktopCamera.SetActive(!useVr);
        }
    }
}
