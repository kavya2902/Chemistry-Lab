using System.Collections;
using UnityEngine;

/// <summary>
/// Attach to NozzleEnd (child of BuretteModel).
///
/// Controls the liquid particle stream and physical drops from the nozzle tip.
/// Does NOT self-detect pokes. Wire the PointableUnityEventWrapper on this
/// GameObject: WhenSelect → BuretteNozzleTipFlow.OnPoked()
///
/// MINIMUM SETUP:
///  • Assign nozzleTip: empty child Transform at the exact glass nozzle opening.
///  • Assign titrationController and flaskLiquid.
///  • PointableUnityEventWrapper → WhenSelect → OnPoked()
/// </summary>
[DisallowMultipleComponent]
public class BuretteNozzleTipFlow : MonoBehaviour
{
    [Header("Nozzle Controller (Preferred)")]
    [Tooltip("Assign the BuretteNozzleController on the BuretteNozzle object.\n\n" +
             "When set: poke delegates entirely to the controller which:\n" +
             "  • Sets isOpen so BuretteDripSpawner spawns physics drops\n" +
             "  • Rotates the handle visual\n" +
             "  • Notifies IodineTitrationController (with auto-starch)\n" +
             "The built-in particle system is NOT built (BuretteDripSpawner handles drops).\n\n" +
             "When null: falls back to the old particle-system path.")]
    public BuretteNozzleController nozzleController;

    [Header("Nozzle Tip")]
    [Tooltip("Empty child Transform at the exact glass nozzle opening. Leave empty to use this Transform.")]
    public Transform nozzleTip;

    [Header("Scene References")]
    public IodineTitrationController titrationController;
    public ConicalFlaskLiquid flaskLiquid;

    [Header("Flow Settings")]
    [Range(0.05f, 0.5f)]
    public float dropInterval = 0.14f;

    [Range(0.1f, 2f)]
    public float pokeCooldown = 0.5f;

    [Header("Liquid Appearance")]
    public Color liquidColor = new Color(0.62f, 0.88f, 1f, 0.80f);

    [Range(0.004f, 0.030f)]
    public float dropDiameter = 0.012f;

    [Header("Drop Physics")]
    public float dropInitialSpeed = 1.8f;
    public float dropDrag = 0.25f;
    public float dropLifetime = 6f;

    // ── State ──────────────────────────────────────────────────────────────

    public bool isFlowing { get; private set; }

    private float         _lastPokeTime = -999f;
    private ParticleSystem _liquidPS;
    private Material      _dropMat;
    private Coroutine     _spawnCoroutine;

    // ── Unity lifecycle ────────────────────────────────────────────────────

    void Start()
    {
        if (nozzleTip == null) nozzleTip = transform;
        _dropMat = BuildDropMaterial();

        // Only build the particle system when no nozzle controller is wired.
        // When nozzleController is set, BuretteDripSpawner handles physical drops
        // and no redundant particle stream is needed.
        if (nozzleController == null)
            BuildParticleSystem();

        ValidateReferences();
    }

    void OnDestroy()
    {
        if (_dropMat != null) Destroy(_dropMat);
    }

    // ── Public API — wire these in the Inspector ───────────────────────────

    /// <summary>
    /// Wire to PointableUnityEventWrapper → WhenSelect in the Inspector.
    /// Each call toggles liquid flow on / off (debounced by pokeCooldown).
    /// </summary>
    public void OnPoked()
    {
        if (Time.time - _lastPokeTime < pokeCooldown) return;
        _lastPokeTime = Time.time;
        SetFlowing(!isFlowing);
    }

    public void StartFlow() => SetFlowing(true);
    public void StopFlow()  => SetFlowing(false);

    public void SetFlowing(bool flowing)
    {
        if (isFlowing == flowing) return;
        isFlowing = flowing;

        if (nozzleController != null)
        {
            // Delegate to BuretteNozzleController which:
            //   • sets isOpen → BuretteDripSpawner starts spawning physics drops
            //   • rotates the handle visual
            //   • auto-advances starch phase if needed
            //   • notifies IodineTitrationController
            nozzleController.SetNozzleOpen(flowing);
        }
        else
        {
            // Fallback: old particle-system path (no BuretteDripSpawner)
            NotifyTitrationController(flowing);
            ControlParticleStream(flowing);
        }

        Debug.Log($"[BuretteNozzleTipFlow] Nozzle {(flowing ? "OPEN — liquid flowing" : "CLOSED")}");
    }

    // ── Internal: titration state ──────────────────────────────────────────

    private void NotifyTitrationController(bool flowing)
    {
        if (titrationController == null) return;

        if (flowing)
        {
            if (titrationController.currentPhase ==
                IodineTitrationController.ExperimentPhase.SetupVisible)
            {
                titrationController.OnStarchPoured();
            }

            if (flaskLiquid != null)
                flaskLiquid.SetStarchAdded();

            var phase = titrationController.currentPhase;
            if (phase != IodineTitrationController.ExperimentPhase.NotStarted &&
                phase != IodineTitrationController.ExperimentPhase.EndpointReached)
            {
                titrationController.SetBuretteFlowing(true);
            }
        }
        else
        {
            titrationController.SetBuretteFlowing(false);
        }
    }

    // ── Internal: particle stream ──────────────────────────────────────────

    private void ControlParticleStream(bool flowing)
    {
        if (_liquidPS == null) return;

        if (flowing)
        {
            var em     = _liquidPS.emission;
            em.enabled = true;
            if (!_liquidPS.isPlaying) _liquidPS.Play();
        }
        else
        {
            _liquidPS.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void BuildParticleSystem()
    {
        var temp       = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var sphereMesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Destroy(temp);

        var psGO = new GameObject("_BuretteLiquidDropPS");
        psGO.transform.SetParent(nozzleTip, false);
        psGO.transform.localPosition = Vector3.zero;
        psGO.transform.localRotation = Quaternion.identity;

        _liquidPS = psGO.AddComponent<ParticleSystem>();

        var main             = _liquidPS.main;
        main.loop            = true;
        main.playOnAwake     = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(1.0f, 1.8f);
        main.startSpeed      = 0f;
        main.startSize       = new ParticleSystem.MinMaxCurve(dropDiameter * 0.7f, dropDiameter * 1.1f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
            new Color(liquidColor.r, liquidColor.g, liquidColor.b, 0.45f),
            new Color(liquidColor.r, liquidColor.g, liquidColor.b, liquidColor.a));
        main.gravityModifier  = 1.4f;
        main.simulationSpace  = ParticleSystemSimulationSpace.World;
        main.maxParticles     = 200;

        var emission          = _liquidPS.emission;
        emission.enabled      = false;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)1, (short)2, 0, dropInterval)
        });

        var shape       = _liquidPS.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius    = 0.001f;

        var vel     = _liquidPS.velocityOverLifetime;
        vel.enabled = true;
        vel.space   = ParticleSystemSimulationSpace.World;
        vel.x       = new ParticleSystem.MinMaxCurve(0f);
        vel.y       = new ParticleSystem.MinMaxCurve(-dropInitialSpeed);
        vel.z       = new ParticleSystem.MinMaxCurve(0f);

        var sol     = _liquidPS.sizeOverLifetime;
        sol.enabled = true;
        var curve   = new AnimationCurve();
        curve.AddKey(new Keyframe(0.00f, 0.15f,  0f,  6f));
        curve.AddKey(new Keyframe(0.18f, 1.00f,  0f,  0f));
        curve.AddKey(new Keyframe(0.82f, 1.00f,  0f,  0f));
        curve.AddKey(new Keyframe(1.00f, 0.25f, -4f,  0f));
        sol.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var col     = _liquidPS.colorOverLifetime;
        col.enabled = true;
        var grad    = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(liquidColor.r, liquidColor.g, liquidColor.b), 0f),
                new GradientColorKey(new Color(liquidColor.r, liquidColor.g, liquidColor.b), 1f),
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.0f, 0.00f),
                new GradientAlphaKey(0.9f, 0.15f),
                new GradientAlphaKey(0.9f, 0.80f),
                new GradientAlphaKey(0.0f, 1.00f),
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var rend = psGO.GetComponent<ParticleSystemRenderer>();
        if (sphereMesh != null)
        {
            rend.renderMode = ParticleSystemRenderMode.Mesh;
            rend.mesh       = sphereMesh;
        }
        else
        {
            rend.renderMode = ParticleSystemRenderMode.Billboard;
        }
        rend.material          = BuildParticleMaterial();
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows    = false;

        _liquidPS.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private Material BuildParticleMaterial()
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null || sh.name.Contains("InternalError"))
            sh = Shader.Find("Particles/Standard Unlit");
        if (sh == null || sh.name.Contains("InternalError"))
            sh = Shader.Find("Standard");
        if (sh == null) return null;

        var mat = new Material(sh) { color = liquidColor };
        ConfigureTransparentMaterial(mat, liquidColor);
        return mat;
    }

    // ── Internal: physical drops ───────────────────────────────────────────

    private void ControlDropSpawn(bool flowing)
    {
        if (_spawnCoroutine != null)
        {
            StopCoroutine(_spawnCoroutine);
            _spawnCoroutine = null;
        }
        if (flowing) _spawnCoroutine = StartCoroutine(SpawnDropLoop());
    }

    private IEnumerator SpawnDropLoop()
    {
        yield return new WaitForSeconds(dropInterval * 0.5f);
        while (isFlowing)
        {
            SpawnDrop();
            yield return new WaitForSeconds(dropInterval);
        }
    }

    private void SpawnDrop()
    {
        var go  = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "BuretteDrop";

        go.transform.position   = nozzleTip.position;
        go.transform.localScale = Vector3.one * dropDiameter;

        var mr               = go.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows    = false;
        if (_dropMat != null) mr.material = _dropMat;

        var col        = go.GetComponent<SphereCollider>();
        col.isTrigger  = true;

        var rb                        = go.AddComponent<Rigidbody>();
        rb.useGravity                 = true;
        rb.linearDamping              = dropDrag;
        rb.angularDamping             = 10f;
        rb.mass                       = 0.0001f;
        rb.collisionDetectionMode     = CollisionDetectionMode.ContinuousSpeculative;
        rb.constraints                = RigidbodyConstraints.FreezeRotation;
        rb.linearVelocity             = Vector3.down * dropInitialSpeed;

        var drip = go.AddComponent<DripDropBehavior>();
        drip.Initialize(dropLifetime, 0.18f, flaskLiquid, null);
    }

    private Material BuildDropMaterial()
    {
        Shader sh = Shader.Find("Standard");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) return null;

        var mat = new Material(sh) { color = liquidColor };
        ConfigureTransparentMaterial(mat, liquidColor);
        return mat;
    }

    private static void ConfigureTransparentMaterial(Material mat, Color color)
    {
        if (mat.HasProperty("_Mode"))
        {
            mat.SetFloat("_Mode", 3f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite",   0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        }
        if (mat.HasProperty("_Surface"))   mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend"))     mat.SetFloat("_Blend",   0f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     color);

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private void ValidateReferences()
    {
        if (titrationController == null)
            Debug.LogWarning("[BuretteNozzleTipFlow] titrationController is not assigned.", this);
        if (flaskLiquid == null)
            Debug.LogWarning("[BuretteNozzleTipFlow] flaskLiquid is not assigned.", this);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        var tip = nozzleTip != null ? nozzleTip : transform;

        Gizmos.color = new Color(0f, 0.9f, 1f, 0.85f);
        Gizmos.DrawWireSphere(tip.position, dropDiameter);

        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.7f);
        Gizmos.DrawRay(tip.position, Vector3.down * 0.5f);

        UnityEditor.Handles.Label(
            tip.position + Vector3.right * 0.02f,
            isFlowing ? "FLOWING" : "CLOSED",
            new GUIStyle { normal = { textColor = isFlowing ? Color.cyan : Color.gray },
                           fontSize = 11 });
    }
#endif
}
