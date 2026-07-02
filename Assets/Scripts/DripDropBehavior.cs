//using System.Collections;
//using UnityEngine;

///// <summary>
///// Attached to each individual drip-drop mesh object spawned by BuretteDripSpawner.
///// Handles lifetime, flask collision detection (trigger), and the smooth
///// scale-shrink "merge" animation when absorbed into the flask liquid.
///// </summary>
//[RequireComponent(typeof(Rigidbody))]
//public class DripDropBehavior : MonoBehaviour
//{
//    private float _lifetime;
//    private float _absorbDuration;
//    private ConicalFlaskLiquid _flaskLiquid;
//    private BuretteDripSpawner _spawner;
//    private bool _absorbed = false;

//    private static readonly int ColorPropID = Shader.PropertyToID("_Color");

//    /// <summary>Called by BuretteDripSpawner immediately after Instantiate.</summary>
//    public void Initialize(float lifetime, float absorbDuration, ConicalFlaskLiquid flaskLiquid, BuretteDripSpawner spawner)
//    {
//        _lifetime      = lifetime;
//        _absorbDuration = absorbDuration;
//        _flaskLiquid   = flaskLiquid;
//        _spawner       = spawner;

//        StartCoroutine(AutoDestroyAfter(lifetime));
//    }

//    void OnTriggerEnter(Collider other)
//    {
//        if (_absorbed) return;

//        // Check if we hit the flask liquid volume or flask body
//        if (other.CompareTag("FlaskLiquid") || other.name.Contains("FlaskTrigger"))
//        {
//            AbsorbIntoFlask();
//        }
//    }

//    void OnCollisionEnter(Collision collision)
//    {
//        if (_absorbed) return;

//        // When drop lands on the flask liquid surface mesh it will collide
//        if (collision.gameObject.CompareTag("FlaskLiquid") ||
//            collision.gameObject.name.Contains("liquid") ||
//            collision.gameObject.name.Contains("FlaskTrigger"))
//        {
//            AbsorbIntoFlask();
//        }
//    }

//    private void AbsorbIntoFlask()
//    {
//        if (_absorbed) return;
//        _absorbed = true;

//        // Notify flask that a drop was added
//        if (_flaskLiquid != null)
//            _flaskLiquid.OnDropReceived();

//        StopAllCoroutines();
//        StartCoroutine(ShrinkAndDestroy());
//    }

//    private IEnumerator ShrinkAndDestroy()
//    {
//        Vector3 startScale = transform.localScale;
//        float elapsed = 0f;

//        while (elapsed < _absorbDuration)
//        {
//            elapsed += Time.deltaTime;
//            float t = elapsed / _absorbDuration;
//            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
//            yield return null;
//        }

//        FinishDrop();
//    }

//    private IEnumerator AutoDestroyAfter(float seconds)
//    {
//        yield return new WaitForSeconds(seconds);
//        if (!_absorbed) FinishDrop();
//    }

//    private void FinishDrop()
//    {
//        if (_spawner != null)
//            _spawner.OnDropFinished(gameObject);
//        Destroy(gameObject);
//    }
//}



using System.Collections;
using UnityEngine;

/// <summary>
/// Attached to each drip-drop mesh spawned by BuretteDripSpawner.
/// Handles lifetime, collision with the flask, and smooth scale-shrink absorption.
///
/// FIXES:
///  - _absorbed flag is set before the coroutine branch so concurrent trigger/collision
///    events on the same frame cannot both fire AbsorbIntoFlask().
///  - OnDropReceived() is NOT called here anymore — FlaskLiquidTrigger is the single
///    authoritative counter, preventing double-counting when both trigger and collision
///    events fire for the same drop.
///  - ShrinkAndDestroy now gracefully handles the case where the object is already
///    being destroyed (coroutine null-check).
///  - Rigidbody is disabled (isKinematic=true) the moment absorption begins so the
///    drop doesn't keep bouncing inside the flask mesh while shrinking.
///  - AutoDestroyAfter uses a cached WaitForSeconds to reduce GC allocation.
///  - Null-guard on _spawner.OnDropFinished.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class DripDropBehavior : MonoBehaviour
{
    private float _lifetime;
    private float _absorbDuration;
    private ConicalFlaskLiquid _flaskLiquid;
    private IodineTitrationController _titrationController;
    private BuretteDripSpawner _spawner;
    private bool _absorbed;
    private Rigidbody _rb;
    private float _spawnY;           // world Y at spawn — used to filter early collisions

    [Tooltip("Drop must fall at least this far (metres) before a collision counts as absorption. " +
             "Prevents the nozzle tip itself from triggering absorption.")]
    private const float MinFallDistance = 0.04f;

    private WaitForSeconds _autoDestroyWait;

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>Called by BuretteDripSpawner immediately after Instantiate.</summary>
    public void Initialize(float lifetime, float absorbDuration,
                           ConicalFlaskLiquid flaskLiquid, BuretteDripSpawner spawner,
                           float growDuration = 0f, float releaseSpeed = 1.5f, float fullScale = 0f,
                           IodineTitrationController titrationController = null)
    {
        _lifetime              = lifetime;
        _absorbDuration        = absorbDuration;
        _flaskLiquid           = flaskLiquid;
        _titrationController   = titrationController;
        _spawner               = spawner;
        _absorbed              = false;
        _spawnY                = transform.position.y;

        _rb = GetComponent<Rigidbody>();
        _autoDestroyWait = new WaitForSeconds(lifetime);

        if (growDuration > 0f && fullScale > 0f)
            StartCoroutine(GrowThenFall(growDuration, fullScale, releaseSpeed));

        StartCoroutine(AutoDestroyAfter());
    }

    // ── Collision / Trigger ────────────────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        if (_absorbed) return;
        // Named/tagged check for the explicit FlaskLiquid trigger zone
        if (other.CompareTag("FlaskLiquid") || other.name.Contains("FlaskTrigger"))
        {
            AbsorbIntoFlask();
            return;
        }
        // Fallback: absorb on any trigger entry after minimum fall (catches unlabelled flask triggers)
        if (HasFallenFarEnough())
            AbsorbIntoFlask();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (_absorbed) return;
        // Absorb on any solid-surface hit after minimum fall — covers any flask mesh name/tag
        if (HasFallenFarEnough())
            AbsorbIntoFlask();
    }

    private bool HasFallenFarEnough()
        => (_spawnY - transform.position.y) >= MinFallDistance;

    // ── Internal ───────────────────────────────────────────────────────────

    private IEnumerator GrowThenFall(float growDuration, float fullScale, float releaseSpeed)
    {
        Vector3 startScale = transform.localScale;
        Vector3 endScale   = Vector3.one * fullScale;
        float   elapsed    = 0f;

        while (elapsed < growDuration)
        {
            if (this == null || gameObject == null) yield break;
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(startScale, endScale,
                                                Mathf.Clamp01(elapsed / growDuration));
            yield return null;
        }

        if (this == null || gameObject == null) yield break;
        transform.localScale = endScale;

        // Release the drop straight down
        if (_rb != null)
        {
            _rb.isKinematic           = false;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            _rb.linearVelocity        = Vector3.down * releaseSpeed;
        }
    }

    private void AbsorbIntoFlask()
    {
        // FIX: set flag FIRST before any branch so concurrent calls on the same frame
        //      are blocked immediately.
        if (_absorbed) return;
        _absorbed = true;

        // Stop physics so the drop doesn't jitter inside the flask while shrinking
        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
        }

        // Notify the titration system directly — reliable regardless of FlaskLiquidTrigger setup
        _flaskLiquid?.OnDropReceived();
        _titrationController?.AdvanceByOneDrop();

        StopAllCoroutines();
        StartCoroutine(ShrinkAndDestroy());
    }

    private IEnumerator ShrinkAndDestroy()
    {
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < _absorbDuration)
        {
            // FIX: guard against the object being destroyed mid-coroutine
            if (this == null || gameObject == null) yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _absorbDuration);
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            yield return null;
        }

        FinishDrop();
    }

    private IEnumerator AutoDestroyAfter()
    {
        yield return _autoDestroyWait;   // FIX: cached, no GC allocation
        if (!_absorbed) FinishDrop();
    }

    private void FinishDrop()
    {
        // FIX: null-guard on spawner
        if (_spawner != null)
            _spawner.OnDropFinished(gameObject);

        if (gameObject != null)
            Destroy(gameObject);
    }
}