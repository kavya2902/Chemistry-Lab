using System;
using UnityEngine;

/// Runtime diagnostic + auto-fix for litmus paper grab setup.
///
/// SETUP:
///   1. Attach this script to BlueLitmusPaper AND PinkLitmusPaper.
///   2. Enter Play mode once — Console will show exactly what each paper is missing.
///   3. This script auto-adds a non-trigger BoxCollider if one is missing.
///   4. After fixing all errors, you may remove this script.
///
/// GRAB COMPONENT QUICK GUIDE (Oculus / Meta XR SDK):
///   Each litmus paper root needs ALL four of these:
///     a) Rigidbody            (auto-added by LitmusPaperController)
///     b) BoxCollider          isTrigger = FALSE  (this script adds it if missing)
///     c) Grabbable            Oculus.Interaction.Grabbable
///     d) HandGrabInteractable Oculus.Interaction.HandGrabInteractable
///        → Set RigidbodyRef to point to the Rigidbody on this same GameObject.
///        → Add at least one HandGrabPose child or set "Support All Grab Types" = true.
///
/// QUICK DUPLICATE METHOD (fastest):
///   1. Find any working grabbable object in the scene (e.g. the titration beaker).
///   2. Copy (Ctrl+C) its Grabbable + HandGrabInteractable components.
///   3. Paste them onto BlueLitmusPaper and PinkLitmusPaper.
///   4. Re-link the RigidbodyRef field to THIS object's Rigidbody.
[RequireComponent(typeof(LitmusPaperController))]
public class LitmusPaperGrabSetup : MonoBehaviour
{
    [Header("Expected grab collider size (metres)")]
    public Vector3 grabColliderSize   = new Vector3(0.03f, 0.12f, 0.006f);
    public Vector3 grabColliderCenter = new Vector3(0f,    0f,    0f);

    private void Awake()
    {
        bool ok = true;

        // ── Rigidbody ────────────────────────────────────────────────────────
        var rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError($"[GrabSetup] '{name}': Rigidbody missing — " +
                           "LitmusPaperController should have added it. Re-add manually.");
            ok = false;
        }

        // ── Physical (non-trigger) BoxCollider on the root ────────────────────
        bool hasPhysicsCollider = false;
        foreach (var col in GetComponents<Collider>())
            if (!col.isTrigger) { hasPhysicsCollider = true; break; }

        if (!hasPhysicsCollider)
        {
            // Auto-add one — otherwise the grab ray can't hit the object.
            var box = gameObject.AddComponent<BoxCollider>();
            box.size   = grabColliderSize;
            box.center = grabColliderCenter;
            Debug.LogWarning($"[GrabSetup] '{name}': No non-trigger collider found on root. " +
                             $"Auto-added BoxCollider (size {grabColliderSize}). " +
                             "Resize it in the Inspector to fit the paper mesh.");
            ok = false;
        }

        // ── Grab component ────────────────────────────────────────────────────
        bool hasGrabbable           = HasComponent("Grabbable");
        bool hasHandGrabInteractable = HasComponent("HandGrabInteractable")
                                     || HasComponent("GrabInteractable")
                                     || HasComponent("XRGrabInteractable")
                                     || HasComponent("OVRGrabbable");

        if (!hasGrabbable)
        {
            Debug.LogError($"[GrabSetup] '{name}': No grab component found!\n" +
                           "Add one of:\n" +
                           "  Oculus SDK → Oculus.Interaction.Grabbable\n" +
                           "  XR Toolkit → XRGrabInteractable\n" +
                           "  Legacy OVR → OVRGrabbable\n" +
                           "See this script's header comment for full setup steps.");
            ok = false;
        }

        if (hasGrabbable && !hasHandGrabInteractable)
        {
            Debug.LogError($"[GrabSetup] '{name}': Has Grabbable but no Interactable!\n" +
                           "Add: HandGrabInteractable (or GrabInteractable for controller grab).\n" +
                           "Set its RigidbodyRef to the Rigidbody on this same GameObject.\n" +
                           "Enable 'Support All Hand Grab Types' to allow any grip.");
            ok = false;
        }

        if (ok)
        {
            Debug.Log($"[GrabSetup] '{name}': All grab components present. " +
                      "You may remove the LitmusPaperGrabSetup script.");
        }
    }

    private bool HasComponent(string typeName)
    {
        foreach (var c in GetComponents<MonoBehaviour>())
            if (c != null && c.GetType().Name == typeName) return true;
        return false;
    }
}
