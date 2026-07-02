using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

/// <summary>
/// Loads the 3D lab room (walls, lights, furniture) additively on top of SampleScene.
/// After loading, automatically disables any Camera, AudioListener, and EventSystem
/// from the lab scene so they don't conflict with SampleScene's OVRCameraRig.
/// </summary>
public class LabEnvironmentLoader : MonoBehaviour
{
    void Awake()
    {
        if (!SceneManager.GetSceneByName("Laboratory Scene").isLoaded)
        {
            var op = SceneManager.LoadSceneAsync("Laboratory Scene", LoadSceneMode.Additive);
            op.completed += OnLabSceneLoaded;
        }
    }

    void OnLabSceneLoaded(AsyncOperation op)
    {
        Scene labScene = SceneManager.GetSceneByName("Laboratory Scene");
        if (!labScene.IsValid()) return;

        foreach (GameObject root in labScene.GetRootGameObjects())
        {
            // Disable all cameras — OVRCameraRig in SampleScene handles rendering
            foreach (var cam in root.GetComponentsInChildren<Camera>(true))
            {
                cam.gameObject.SetActive(false);
                Debug.Log($"[LabLoader] Disabled camera: {cam.name}");
            }

            // Disable extra AudioListeners — SampleScene already has one
            foreach (var al in root.GetComponentsInChildren<AudioListener>(true))
            {
                al.enabled = false;
                Debug.Log($"[LabLoader] Disabled AudioListener: {al.name}");
            }

            // Disable extra EventSystems — SampleScene already has one
            foreach (var es in root.GetComponentsInChildren<EventSystem>(true))
            {
                es.gameObject.SetActive(false);
                Debug.Log($"[LabLoader] Disabled EventSystem: {es.name}");
            }
        }

        Debug.Log("[LabLoader] Laboratory Scene loaded and cleaned up successfully.");
    }
}
