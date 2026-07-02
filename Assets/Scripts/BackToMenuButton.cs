using UnityEngine;

public class BackToMenuButton : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Leave empty — found automatically in the scene.")]
    public ExperimentManager experimentManager;

    [Header("Cooldown")]
    [Range(0.3f, 5f)]
    public float pressCooldown = 1.5f;

    [Header("Visual Feedback (optional)")]
    public Renderer buttonRenderer;
    public Color normalColor  = new Color(0.15f, 0.30f, 0.80f, 1f);
    public Color pressedColor = new Color(0.10f, 0.80f, 0.35f, 1f);
    public float flashDuration = 0.25f;

    private float _lastPressTime = -999f;
    // Grace period after the button becomes active before it accepts any trigger.
    // Prevents OnTriggerEnter from firing the instant the scene activates and
    // overlapping objects enter the collider on the same frame.
    private float _activationTime = -999f;
    private const float ActivationGrace = 2f;

    void Awake()
    {
        if (GetComponent<Rigidbody>() == null)
        {
            Rigidbody rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity  = false;
        }

        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
            Debug.LogWarning("[BackToMenuButton] Collider was not set to Trigger — fixed automatically.", this);
        }
        else if (col == null)
        {
            Debug.LogError("[BackToMenuButton] No Collider found! Add a Box/Sphere Collider with Is Trigger.", this);
        }
    }

    void OnEnable()
    {
        // Reset activation timer every time the button is shown
        _activationTime = Time.time;
        Debug.Log($"[BackToMenuButton] '{gameObject.name}' became VISIBLE at {transform.position}", this);
    }

    void Start()
    {
        if (experimentManager == null)
            experimentManager = FindFirstObjectByType<ExperimentManager>();

        if (experimentManager == null)
            Debug.LogError("[BackToMenuButton] ExperimentManager not found.", this);

        if (buttonRenderer != null)
            buttonRenderer.material.color = normalColor;
    }

    void OnTriggerEnter(Collider other)
    {
        if (Time.time - _activationTime < ActivationGrace) return;
        if (Time.time - _lastPressTime < pressCooldown) return;
        _lastPressTime = Time.time;

        Debug.Log($"[BackToMenuButton] Pressed — returning to menu.");
        experimentManager?.ShowMainMenuAndReset();

        if (buttonRenderer != null)
            StartCoroutine(FlashButton());
    }

    private System.Collections.IEnumerator FlashButton()
    {
        if (buttonRenderer == null) yield break;
        buttonRenderer.material.color = pressedColor;
        yield return new WaitForSeconds(flashDuration);
        if (buttonRenderer != null)
            buttonRenderer.material.color = normalColor;
    }
}
