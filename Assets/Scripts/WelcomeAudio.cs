using UnityEngine;

public class WelcomeAudio : MonoBehaviour
{
    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();

        // Small delay to ensure XR fully loads
        Invoke("PlayWelcome", 2f);
    }

    void PlayWelcome()
    {
        if (audioSource != null) audioSource.Play();
    }
}