using UnityEngine;

/// <summary>
/// Monitors a lab object (beaker, flask, pipette, etc.) and respawns it
/// back to its original position/rotation if it falls below a Y threshold
/// or travels too far from its spawn point.
///
/// Attach to any grabbable lab object. The spawn pose is captured at Start().
/// After respawn, the Rigidbody velocity is zeroed and the object is briefly
/// set to kinematic to prevent immediate re-falling.
/// </summary>
public class LabObjectRespawner : MonoBehaviour
{
    [Header("Respawn Settings")]
    [Tooltip("If the object's Y position drops below this value, it respawns.")]
    public float fallThresholdY = 0.3f;

    [Tooltip("If the object moves further than this distance from spawn, it respawns.")]
    public float maxDistanceFromSpawn = 3f;

    [Tooltip("Seconds the object stays kinematic after respawn to settle.")]
    public float kinematicCooldown = 0.5f;

    // ── Internal ───────────────────────────────────────────────────────────
    private Vector3 _spawnPosition;
    private Quaternion _spawnRotation;
    private Rigidbody _rb;
    private float _kinematicTimer;
    private bool _isInCooldown;

    void Start()
    {
        _spawnPosition = transform.position;
        _spawnRotation = transform.rotation;
        _rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // Handle cooldown after respawn
        if (_isInCooldown)
        {
            _kinematicTimer -= Time.deltaTime;
            if (_kinematicTimer <= 0f)
            {
                _isInCooldown = false;
                // Restore non-kinematic only if the object was non-kinematic before
                // (beaker has useGravity=true, isKinematic=false)
                if (_rb != null)
                {
                    _rb.isKinematic = false;
                    _rb.linearVelocity = Vector3.zero;
                    _rb.angularVelocity = Vector3.zero;
                }
            }
            return;
        }

        // Check if object has fallen or drifted too far
        bool hasFallen = transform.position.y < fallThresholdY;
        bool hasDrifted = Vector3.Distance(transform.position, _spawnPosition) > maxDistanceFromSpawn;

        if (hasFallen || hasDrifted)
        {
            Respawn();
        }
    }

    /// <summary>Teleports the object back to its spawn position and rotation.</summary>
    public void Respawn()
    {
        Debug.Log($"[LabObjectRespawner] Respawning '{gameObject.name}' to spawn position.");

        if (_rb != null)
        {
            _rb.isKinematic = true;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        transform.position = _spawnPosition;
        transform.rotation = _spawnRotation;

        _isInCooldown = true;
        _kinematicTimer = kinematicCooldown;

        // Notify BeakerLiquid / ConicalFlaskLiquid to reset if needed
        var beakerLiquid = GetComponent<BeakerLiquid>();
        if (beakerLiquid != null)
            beakerLiquid.ResetBeaker();

        var pourDetector = GetComponent<StarchPourDetector>();
        if (pourDetector != null)
            pourDetector.ResetDetector();
    }

    /// <summary>Updates the spawn point (e.g., after the experiment repositions objects).</summary>
    public void SetSpawnPose(Vector3 position, Quaternion rotation)
    {
        _spawnPosition = position;
        _spawnRotation = rotation;
    }
}
