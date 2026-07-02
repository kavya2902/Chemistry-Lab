//using UnityEngine;

///// <summary>
///// Detects when the grabbed beaker is tilted over the conical flask and triggers
///// the starch pouring action on the IodineTitrationController.
/////
///// Tilt is measured relative to the beaker's resting orientation captured at Start().
///// Distance is measured between pourPoint (beaker mouth) and receivePoint (flask opening).
/////
///// FIX SUMMARY (this revision):
/////  - tiltThreshold lowered to 30° (was 40°) — matches BeakerLiquid.pourTiltThreshold.
/////  - maxPourDistance increased to 0.35 m (was 0.2 m) — matches BeakerLiquid.maxPourDistance.
/////  - pourHoldDuration lowered to 0.3 s (was 0.4 s) — less delay before commit.
/////  - Grab detection now tries SelectingPointsCount, SelectingInteractorsCount, AND
/////    IsGrabbed for multi-SDK-version compatibility.
/////  - skipGrabCheck bool added so you can test without VR grab in the Editor.
/////  - Debug logs throttled to debugLogInterval frames (default 20) and include all
/////    four values: tilt, distance, grabbed, hold time.
/////  - Gizmos draw pourPoint and receivePoint spheres with the acceptance radius ring.
///// </summary>
//public class StarchPourDetector : MonoBehaviour
//{
//    [Header("References")]
//    public IodineTitrationController titrationController;
//    public Transform flaskTransform;
//    public ParticleSystem pourParticleSystem;

//    [Header("Pour Point References")]
//    [Tooltip("Transform at the beaker mouth/rim. If null, falls back to this.transform.")]
//    public Transform pourPoint;
//    [Tooltip("Transform at the flask opening. If null, falls back to flaskTransform.")]
//    public Transform receivePoint;

//    [Header("Pour Detection Settings")]
//    [Tooltip("Degrees of CHANGE from resting orientation that triggers a pour. " +
//             "Should match BeakerLiquid.pourTiltThreshold (30°).")]
//    public float tiltThreshold = 30f;

//    [Tooltip("Max mouth-to-mouth distance. Should match BeakerLiquid.maxPourDistance (0.35 m).")]
//    public float maxPourDistance = 0.35f;

//    [Tooltip("Seconds the beaker must stay tilted AND near the flask before the transfer commits.")]
//    public float pourHoldDuration = 0.3f;

//    [Tooltip("When checked, grab detection is skipped — useful for testing in Editor without VR.")]
//    public bool skipGrabCheck = false;

//    [Header("Debug")]
//    [Tooltip("Log tilt / distance / grab every N frames. 0 = disabled.")]
//    public int debugLogInterval = 20;

//    // ── State ──────────────────────────────────────────────────────────────
//    private bool _hasPouredStarch;
//    public bool hasPouredStarch => _hasPouredStarch;

//    private bool _isPouring;
//    private float _pouringTimeAccum;
//    private Vector3 _restingUp;
//    private int _debugLogThrottle;

//    // Oculus Interaction SDK Grabbable (resolved via reflection)
//    private Component _grabbable;
//    private System.Reflection.PropertyInfo _selectingCountProp;
//    private bool _grabbableResolved;

//    // ── Unity lifecycle ────────────────────────────────────────────────────

//    void Start()
//    {
//        _restingUp = transform.up;
//        ResolveGrabbable();

//        if (titrationController == null)
//            Debug.LogError("[StarchPourDetector] titrationController is not assigned.", this);
//        if (flaskTransform == null)
//            Debug.LogError("[StarchPourDetector] flaskTransform is not assigned.", this);

//        Debug.Log($"[StarchPourDetector] Initialised — restingUp={_restingUp}, " +
//                  $"grabbable={((_grabbable != null) ? "found" : "NULL")}, " +
//                  $"selectingProp={((_selectingCountProp != null) ? _selectingCountProp.Name : "NULL")}, " +
//                  $"skipGrabCheck={skipGrabCheck}");
//    }

//    void Update()
//    {
//        if (_hasPouredStarch) return;
//        if (titrationController == null) return;

//        var phase = titrationController.currentPhase;
//        if (phase != IodineTitrationController.ExperimentPhase.SetupVisible)
//        {
//            if (_isPouring) CancelPour();
//            return;
//        }

//        bool isGrabbed = skipGrabCheck || IsBeingGrabbed();
//        float tiltAngle = Vector3.Angle(transform.up, _restingUp);

//        Vector3 pourPos = pourPoint   != null ? pourPoint.position   : transform.position;
//        Vector3 recvPos = receivePoint!= null ? receivePoint.position
//                        : flaskTransform != null ? flaskTransform.position : transform.position;
//        float dist = Vector3.Distance(pourPos, recvPos);

//        bool isTilted    = tiltAngle > tiltThreshold;
//        bool isNearFlask = dist < maxPourDistance;

//        // Debug visualization line
//        if (pourPoint != null && receivePoint != null)
//            Debug.DrawLine(pourPos, recvPos,
//                (isTilted && isNearFlask && isGrabbed) ? Color.green : Color.red);

//        // Throttled log
//        if (debugLogInterval > 0 && ++_debugLogThrottle >= debugLogInterval)
//        {
//            _debugLogThrottle = 0;
//            Debug.Log($"[StarchPour] Tilt:{tiltAngle:F1}° OK:{isTilted} | " +
//                      $"Dist:{dist:F3}m OK:{isNearFlask} | Grab:{isGrabbed} | " +
//                      $"Phase:{phase} | Pouring:{_isPouring} Hold:{_pouringTimeAccum:F2}s");
//        }

//        if (!isGrabbed)
//        {
//            if (_isPouring) CancelPour();
//            return;
//        }

//        if (flaskTransform == null)
//        {
//            if (_isPouring) CancelPour();
//            return;
//        }

//        if (isTilted && isNearFlask)
//        {
//            if (!_isPouring)
//            {
//                _isPouring = true;
//                _pouringTimeAccum = 0f;
//                PlayPourEffect();
//                Debug.Log($"[StarchPourDetector] Pour STARTED — tilt:{tiltAngle:F1}° dist:{dist:F3}m");
//            }

//            _pouringTimeAccum += Time.deltaTime;

//            if (_pouringTimeAccum >= pourHoldDuration)
//            {
//                _hasPouredStarch = true;
//                Debug.Log($"[StarchPourDetector] Pour COMMITTED — " +
//                          $"tilt:{tiltAngle:F1}° dist:{dist:F3}m hold:{_pouringTimeAccum:F2}s");
//                titrationController.OnStarchPoured();
//                Invoke(nameof(StopPourEffect), 1.5f);
//            }
//        }
//        else
//        {
//            if (_isPouring)
//            {
//                Debug.Log($"[StarchPourDetector] Pour cancelled — tilt:{tiltAngle:F1}° dist:{dist:F3}m");
//                CancelPour();
//            }
//        }
//    }

//    // ── Public API ─────────────────────────────────────────────────────────

//    public void ResetDetector()
//    {
//        CancelInvoke(nameof(StopPourEffect));
//        _hasPouredStarch  = false;
//        _isPouring        = false;
//        _pouringTimeAccum = 0f;
//        _restingUp        = transform.up;
//        StopPourEffect();
//    }

//    // ── Internal ───────────────────────────────────────────────────────────

//    private void ResolveGrabbable()
//    {
//        if (_grabbableResolved) return;
//        _grabbableResolved = true;

//        var grabbableType = System.Type.GetType(
//            "Oculus.Interaction.Grabbable, Oculus.Interaction.Runtime");
//        if (grabbableType == null)
//        {
//            Debug.LogWarning("[StarchPourDetector] Grabbable type not found. " +
//                             "Enable skipGrabCheck to bypass.", this);
//            return;
//        }

//        _grabbable = GetComponent(grabbableType) ?? GetComponentInChildren(grabbableType);
//        if (_grabbable == null)
//        {
//            Debug.LogWarning("[StarchPourDetector] No Grabbable on this object. " +
//                             "Enable skipGrabCheck to bypass.", this);
//            return;
//        }

//        string[] candidateNames =
//        {
//            "SelectingPointsCount",
//            "SelectingInteractorsCount",
//            "IsGrabbed"
//        };

//        var type = _grabbable.GetType();
//        while (type != null && _selectingCountProp == null)
//        {
//            foreach (var name in candidateNames)
//            {
//                _selectingCountProp = type.GetProperty(name,
//                    System.Reflection.BindingFlags.Public |
//                    System.Reflection.BindingFlags.Instance);
//                if (_selectingCountProp != null) break;
//            }
//            type = type.BaseType;
//        }

//        if (_selectingCountProp != null)
//            Debug.Log($"[StarchPourDetector] Resolved grab property: " +
//                      $"{_selectingCountProp.DeclaringType?.Name}.{_selectingCountProp.Name}");
//        else
//            Debug.LogError("[StarchPourDetector] Could not resolve grab property. " +
//                           "Enable skipGrabCheck to bypass.", this);
//    }

//    private bool IsBeingGrabbed()
//    {
//        if (_grabbable == null || _selectingCountProp == null) return false;
//        try
//        {
//            var val = _selectingCountProp.GetValue(_grabbable);
//            if (val is int  count) return count > 0;
//            if (val is bool flag)  return flag;
//            return false;
//        }
//        catch { return false; }
//    }

//    private void CancelPour()
//    {
//        _isPouring        = false;
//        _pouringTimeAccum = 0f;
//        StopPourEffect();
//    }

//    private void PlayPourEffect()
//    {
//        if (pourParticleSystem != null && !pourParticleSystem.isPlaying)
//            pourParticleSystem.Play();
//    }

//    private void StopPourEffect()
//    {
//        if (pourParticleSystem != null)
//            pourParticleSystem.Stop(false, ParticleSystemStopBehavior.StopEmitting);
//    }

//    // ── Editor gizmos ──────────────────────────────────────────────────────

//#if UNITY_EDITOR
//    void OnDrawGizmosSelected()
//    {
//        if (pourPoint != null)
//        {
//            Gizmos.color = Color.cyan;
//            Gizmos.DrawWireSphere(pourPoint.position, 0.015f);
//            UnityEditor.Handles.Label(pourPoint.position + Vector3.up * 0.03f, "pourPoint");
//        }
//        if (receivePoint != null)
//        {
//            Gizmos.color = Color.yellow;
//            Gizmos.DrawWireSphere(receivePoint.position, 0.02f);
//            UnityEditor.Handles.Label(receivePoint.position + Vector3.up * 0.03f, "receivePoint");
//            Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
//            Gizmos.DrawWireSphere(receivePoint.position, maxPourDistance);
//        }
//    }

//    void OnValidate()
//    {
//        if (titrationController == null)
//            Debug.LogWarning("[StarchPourDetector] titrationController is not assigned.", this);
//        if (flaskTransform == null)
//            Debug.LogWarning("[StarchPourDetector] flaskTransform is not assigned.", this);
//        if (pourPoint == null)
//            Debug.LogWarning("[StarchPourDetector] pourPoint not assigned — " +
//                             "create an empty child Transform at the beaker lip.", this);
//        if (receivePoint == null)
//            Debug.LogWarning("[StarchPourDetector] receivePoint not assigned — " +
//                             "create an empty child Transform at the flask mouth.", this);
//    }
//#endif
//}


using UnityEngine;

/// <summary>
/// Detects when the grabbed beaker is tilted over the conical flask and triggers
/// the starch pouring action on the IodineTitrationController.
/// </summary>
public class StarchPourDetector : MonoBehaviour
{
    [Header("References")]
    public IodineTitrationController titrationController;
    public Transform flaskTransform;
    public ParticleSystem pourParticleSystem;

    [Header("Pour Point References")]
    [Tooltip("Beaker mouth position")]
    public Transform pourPoint;

    [Tooltip("Flask opening position")]
    public Transform receivePoint;

    [Header("Pour Detection Settings")]
    public float tiltThreshold = 30f;
    public float maxPourDistance = 0.35f;
    [Tooltip("Seconds the beaker must stay tilted before the flask turns blue. 20 s gives a natural 'pouring contact' feel.")]
    public float pourHoldDuration = 20f;
    [Tooltip("TRUE = pour triggers from tilt+distance alone (no grip needed).\n" +
             "Required for Meta Hand Tracking — hand grip state cannot be detected " +
             "via Grabbable.SelectingPointsCount with hand-tracking interactors.")]
    public bool skipGrabCheck = true;

    [Header("Debug")]

    [Tooltip("Enable / Disable Debug Logs")]
    public bool enableDebugLogs = false;

    [Tooltip("Logs every N frames. 0 = Off")]
    public int debugLogInterval = 0;

    // Internal State
    private bool _hasPouredStarch;
    public bool hasPouredStarch => _hasPouredStarch;

    private bool _isPouring;
    private float _pouringTimeAccum;
    private Vector3 _restingUp;
    private int _debugCounter;

    // Oculus grab detection
    private Component _grabbable;
    private System.Reflection.PropertyInfo _grabProp;
    private bool _resolved;

    void Start()
    {
        _restingUp = transform.up;
        ResolveGrabbable();

        if (enableDebugLogs)
            Debug.Log("[StarchPourDetector] Started");
    }

    void Update()
    {
        if (_hasPouredStarch) return;
        if (titrationController == null) return;

        if (titrationController.currentPhase != IodineTitrationController.ExperimentPhase.SetupVisible)
        {
            if (_isPouring) CancelPour(); // only cancel when actually pouring, not every frame
            return;
        }

        bool isGrabbed = skipGrabCheck || IsBeingGrabbed();

        float tiltAngle = Vector3.Angle(transform.up, _restingUp);

        bool usingFallback = pourPoint == null || receivePoint == null;
        Vector3 pourPos = pourPoint != null ? pourPoint.position : transform.position;
        Vector3 receivePos = receivePoint != null ? receivePoint.position :
                             flaskTransform != null ? flaskTransform.position :
                             transform.position;

        float dist = Vector3.Distance(pourPos, receivePos);
        float effectiveMax = usingFallback ? maxPourDistance + 0.2f : maxPourDistance;

        bool isTilted = tiltAngle > tiltThreshold;
        bool isNear   = dist < effectiveMax;

        // Debug line
        Debug.DrawLine(pourPos, receivePos, (isTilted && isNear) ? Color.green : Color.red);

        // Throttled Logs
        if (enableDebugLogs && debugLogInterval > 0)
        {
            _debugCounter++;

            if (_debugCounter >= debugLogInterval)
            {
                _debugCounter = 0;

                Debug.Log(
                    $"[StarchPour] Tilt:{tiltAngle:F1}° OK:{isTilted} | " +
                    $"Dist:{dist:F2}m (max:{effectiveMax:F2}m{(usingFallback ? " fallback" : "")}) OK:{isNear} | " +
                    $"Grab:{isGrabbed}(skip:{skipGrabCheck}) | Pouring:{_isPouring}"
                );
            }
        }

        if (!isGrabbed)
        {
            CancelPour();
            return;
        }

        if (isTilted && isNear)
        {
            if (!_isPouring)
            {
                _isPouring = true;
                _pouringTimeAccum = 0f;
                PlayPourEffect();

                if (enableDebugLogs)
                    Debug.Log("[StarchPourDetector] Pour Started");
            }

            _pouringTimeAccum += Time.deltaTime;

            if (_pouringTimeAccum >= pourHoldDuration)
            {
                _hasPouredStarch = true;

                if (enableDebugLogs)
                    Debug.Log("[StarchPourDetector] Pour Completed");

                titrationController.OnStarchPoured();

                Invoke(nameof(StopPourEffect), 1.5f);
            }
        }
        else
        {
            CancelPour();
        }
    }

    public void ResetDetector()
    {
        _hasPouredStarch = false;
        _isPouring = false;
        _pouringTimeAccum = 0f;
        _restingUp = transform.up;

        StopPourEffect();
    }

    void ResolveGrabbable()
    {
        if (_resolved) return;
        _resolved = true;

        var type = System.Type.GetType(
            "Oculus.Interaction.Grabbable, Oculus.Interaction.Runtime"
        );

        if (type == null) return;

        _grabbable = GetComponent(type);

        if (_grabbable == null)
            _grabbable = GetComponentInChildren(type);

        if (_grabbable == null) return;

        string[] names =
        {
            "SelectingPointsCount",
            "SelectingInteractorsCount",
            "IsGrabbed"
        };

        var current = _grabbable.GetType();

        while (current != null && _grabProp == null)
        {
            foreach (string n in names)
            {
                _grabProp = current.GetProperty(n);

                if (_grabProp != null)
                    break;
            }

            current = current.BaseType;
        }
    }

    bool IsBeingGrabbed()
    {
        if (_grabbable == null || _grabProp == null)
            return false;

        try
        {
            object val = _grabProp.GetValue(_grabbable);

            if (val is int i)
                return i > 0;

            if (val is bool b)
                return b;

            return false;
        }
        catch
        {
            return false;
        }
    }

    void CancelPour()
    {
        _isPouring = false;
        _pouringTimeAccum = 0f;
        StopPourEffect();
    }

    void PlayPourEffect()
    {
        if (pourParticleSystem != null && !pourParticleSystem.isPlaying)
            pourParticleSystem.Play();
    }

    void StopPourEffect()
    {
        if (pourParticleSystem != null)
            pourParticleSystem.Stop(false, ParticleSystemStopBehavior.StopEmitting);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (pourPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(pourPoint.position, 0.015f);
        }

        if (receivePoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(receivePoint.position, 0.02f);

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(receivePoint.position, maxPourDistance);
        }
    }
#endif
}