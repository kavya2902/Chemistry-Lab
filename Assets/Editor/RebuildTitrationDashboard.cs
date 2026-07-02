using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Rebuilds the TitrationDashboard with card-based step UI,
/// configures all 5 particle systems for realistic liquid appearance,
/// and wires all missing scene references.
/// Run via: Tools > Rebuild Titration Dashboard & Particles
/// </summary>
public static class RebuildTitrationDashboard
{
    [MenuItem("Tools/Rebuild Titration Dashboard & Particles")]
    public static void Execute()
    {
        // ── Find root objects ─────────────────────────────────────────────
        GameObject titration = null;
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == "Titration") { titration = root; break; }
        }
        if (titration == null) { Debug.LogError("[Rebuild] /Titration not found."); return; }

        bool wasActive = titration.activeSelf;
        titration.SetActive(true);

        IodineTitrationController ctrl = titration.GetComponent<IodineTitrationController>();
        if (ctrl == null) { Debug.LogError("[Rebuild] IodineTitrationController not found on /Titration."); return; }

        // ── 1. Particle Systems ───────────────────────────────────────────
        ConfigureParticleSystems(titration, ctrl);

        // ── 2. Dashboard ──────────────────────────────────────────────────
        RebuildDashboard(titration, ctrl);

        // ── 3. Wire missing scene references ─────────────────────────────
        WireMissingReferences(titration, ctrl);

        // ── 4. Restore state ──────────────────────────────────────────────
        titration.SetActive(wasActive);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("[Rebuild] Dashboard & Particles rebuilt. Save the scene.");
    }

    // ======================================================================
    // PARTICLE SYSTEMS
    // ======================================================================
    static void ConfigureParticleSystems(GameObject titration, IodineTitrationController ctrl)
    {
        // -- IodineLiquid_PS : flat yellow-brown pool inside flask --------
        if (ctrl.iodineLiquidPS != null)
        {
            var ps = ctrl.iodineLiquidPS;
            var main = ps.main;
            main.loop            = true;
            main.playOnAwake     = false;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(3.0f);
            main.startSpeed      = new ParticleSystem.MinMaxCurve(0f);
            main.startSize       = new ParticleSystem.MinMaxCurve(0.025f, 0.04f);
            main.startColor      = new Color(0.78f, 0.52f, 0.04f, 0.90f);
            main.maxParticles    = 60;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 20f;

            var shape = ps.shape;
            shape.enabled    = true;
            shape.shapeType  = ParticleSystemShapeType.Circle;
            shape.radius     = 0.030f;
            shape.rotation   = new Vector3(90f, 0f, 0f); // emit horizontally

            SetParticleMaterialTransparent(ps, new Color(0.78f, 0.52f, 0.04f, 0.90f));
            EditorUtility.SetDirty(ps);
            Debug.Log("[Rebuild] IodineLiquid_PS configured.");
        }

        // -- StarchLiquid_PS : small deep-blue pool in beaker -------------
        if (ctrl.starchLiquidPS != null)
        {
            var ps = ctrl.starchLiquidPS;
            var main = ps.main;
            main.loop            = true;
            main.playOnAwake     = false;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(2.5f);
            main.startSpeed      = new ParticleSystem.MinMaxCurve(0f);
            main.startSize       = new ParticleSystem.MinMaxCurve(0.018f, 0.028f);
            main.startColor      = new Color(0.04f, 0.04f, 0.62f, 0.92f);
            main.maxParticles    = 30;          // Only a little starch
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 12f;

            var shape = ps.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius    = 0.020f;
            shape.rotation  = new Vector3(90f, 0f, 0f);

            SetParticleMaterialTransparent(ps, new Color(0.04f, 0.04f, 0.62f, 0.92f));
            EditorUtility.SetDirty(ps);
            Debug.Log("[Rebuild] StarchLiquid_PS configured (small fill).");
        }

        // -- BuretteLiquid_PS : colorless thiosulfate column in burette ---
        if (ctrl.buretteLiquidPS != null)
        {
            var ps = ctrl.buretteLiquidPS;
            var main = ps.main;
            main.loop            = true;
            main.playOnAwake     = false;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(2.0f);
            main.startSpeed      = new ParticleSystem.MinMaxCurve(0f);
            main.startSize       = new ParticleSystem.MinMaxCurve(0.006f, 0.010f);
            main.startColor      = new Color(0.84f, 0.92f, 1.00f, 0.60f);
            main.maxParticles    = 80;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 40f;

            var shape = ps.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            // Thin vertical box to fill burette tube
            shape.scale     = new Vector3(0.006f, 0.30f, 0.006f);

            SetParticleMaterialTransparent(ps, new Color(0.84f, 0.92f, 1.00f, 0.65f));
            EditorUtility.SetDirty(ps);
            Debug.Log("[Rebuild] BuretteLiquid_PS configured.");
        }

        // -- BuretteFlow_PS : narrow drip stream from nozzle downward -----
        if (ctrl.buretteFlowPS != null)
        {
            var ps = ctrl.buretteFlowPS;
            var main = ps.main;
            main.loop            = true;
            main.playOnAwake     = false;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(0.55f, 0.70f);
            main.startSpeed      = new ParticleSystem.MinMaxCurve(0.30f, 0.45f);
            main.startSize       = new ParticleSystem.MinMaxCurve(0.004f, 0.007f);
            main.startColor      = new Color(0.85f, 0.93f, 1.00f, 0.80f);
            main.maxParticles    = 60;
            main.gravityModifier = 1.8f;        // Falls under gravity
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 18f;         // Controlled drip rate

            var shape = ps.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle     = 3f;               // Very narrow stream
            shape.radius    = 0.001f;

            // Face downward (-Y)
            ps.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            SetParticleMaterialTransparent(ps, new Color(0.85f, 0.93f, 1.00f, 0.85f));
            EditorUtility.SetDirty(ps);
            Debug.Log("[Rebuild] BuretteFlow_PS configured (realistic drip).");
        }

        // -- StarchPour_PS : curved blue stream from beaker to flask ------
        if (ctrl.starchPourPS != null)
        {
            var ps = ctrl.starchPourPS;
            var main = ps.main;
            main.loop            = true;
            main.playOnAwake     = false;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(0.40f, 0.55f);
            main.startSpeed      = new ParticleSystem.MinMaxCurve(0.35f, 0.50f);
            main.startSize       = new ParticleSystem.MinMaxCurve(0.006f, 0.010f);
            main.startColor      = new Color(0.04f, 0.04f, 0.65f, 0.88f);
            main.maxParticles    = 80;
            main.gravityModifier = 2.5f;        // Arc under gravity
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 40f;

            var shape = ps.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle     = 8f;
            shape.radius    = 0.003f;

            // Slightly angled to arc toward flask
            ps.transform.localRotation = Quaternion.Euler(60f, 180f, 0f);

            SetParticleMaterialTransparent(ps, new Color(0.04f, 0.04f, 0.65f, 0.88f));
            EditorUtility.SetDirty(ps);
            Debug.Log("[Rebuild] StarchPour_PS configured.");
        }
    }

    /// <summary>Switches a particle material to Fade (transparent) blending.</summary>
    static void SetParticleMaterialTransparent(ParticleSystem ps, Color col)
    {
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer == null) return;

        Material mat = renderer.sharedMaterial;
        if (mat == null)
        {
            mat = new Material(Shader.Find("Particles/Standard Unlit"));
            renderer.sharedMaterial = mat;
        }

        // Switch to Fade mode (SrcAlpha / OneMinusSrcAlpha)
        mat.SetFloat("_Mode", 2f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        mat.SetColor("_Color", col);
        mat.SetColor("_TintColor", col);

        renderer.sortingOrder = 1;
        EditorUtility.SetDirty(mat);
    }

    // ======================================================================
    // DASHBOARD REBUILD
    // ======================================================================
    static void RebuildDashboard(GameObject titration, IodineTitrationController ctrl)
    {
        // Remove old dashboard if it exists
        Transform oldDash = titration.transform.Find("TitrationDashboard");
        if (oldDash != null)
        {
            Undo.DestroyObjectImmediate(oldDash.gameObject);
            Debug.Log("[Rebuild] Removed old TitrationDashboard.");
        }

        // Conical flask position for placement reference
        Vector3 flaskPos = ctrl.conicalFlask != null
            ? ctrl.conicalFlask.transform.position
            : new Vector3(-2.50f, 1.10f, 6.85f);

        // ── Canvas ────────────────────────────────────────────────────────
        GameObject canvasGO = new GameObject("TitrationDashboard");
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Dashboard");
        canvasGO.transform.SetParent(titration.transform, false);
        canvasGO.layer = LayerMask.NameToLayer("UI");

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 12f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // Position: to the left of the burette stand, facing the player
        RectTransform canvasRT = canvasGO.GetComponent<RectTransform>();
        canvasRT.sizeDelta     = new Vector2(700, 680);
        canvasRT.localScale    = new Vector3(0.00085f, 0.00085f, 0.00085f);
        canvasRT.position      = new Vector3(flaskPos.x - 0.42f, flaskPos.y + 0.30f, flaskPos.z);
        canvasRT.rotation      = Quaternion.Euler(0f, 0f, 0f);

        // ── Outer background ──────────────────────────────────────────────
        GameObject bg = MakePanel("Background", canvasGO.transform,
            new Color(0.04f, 0.07f, 0.14f, 0.96f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Dark top accent strip
        GameObject headerStrip = MakePanel("HeaderStrip", bg.transform,
            new Color(0.08f, 0.18f, 0.38f, 1f),
            new Vector2(0f, 0.88f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

        // Flask icon label (text emoji as icon)
        GameObject iconLabel = MakeTMP("FlaskIcon", headerStrip.transform,
            "<size=48>\u2697</size>",
            28, Color.white, TextAlignmentOptions.Left);
        SetAnchors(iconLabel, new Vector2(0f, 0f), new Vector2(0.12f, 1f),
            new Vector2(12, 0), new Vector2(0, 0));

        // Title
        GameObject titleGO = MakeTMP("Title", headerStrip.transform,
            "<b>IODINE TITRATION</b>\n<size=60%><color=#9ECFEE>Iodometry Experiment</color></size>",
            26, Color.white, TextAlignmentOptions.Center);
        SetAnchors(titleGO, new Vector2(0.10f, 0f), new Vector2(1f, 1f),
            new Vector2(0, 4), new Vector2(-12, -4));

        // ── Equation card ─────────────────────────────────────────────────
        GameObject eqCard = MakePanel("EquationCard", bg.transform,
            new Color(0.06f, 0.12f, 0.26f, 0.95f),
            new Vector2(0f, 0.80f), new Vector2(1f, 0.88f),
            new Vector2(10, 4), new Vector2(-10, -4));

        GameObject eqLabel = MakeTMP("Equation", eqCard.transform,
            "<color=#FFD700><b>I\u2082</b></color>  +  " +
            "<color=#80DEEA><b>2 Na\u2082S\u2082O\u2083</b></color>  \u2192  " +
            "<color=#A5D6A7><b>2 NaI</b></color>  +  " +
            "<color=#CE93D8><b>Na\u2082S\u2084O\u2086</b></color>",
            20, new Color(0.9f, 0.9f, 1f), TextAlignmentOptions.Center);
        SetAnchors(eqLabel, Vector2.zero, Vector2.one,
            new Vector2(8, 2), new Vector2(-8, -2));

        // ── Steps section label ───────────────────────────────────────────
        GameObject stepsLabel = MakeTMP("StepsLabel", bg.transform,
            "<b>PROCEDURE</b>",
            16, new Color(0.50f, 0.70f, 1f, 1f), TextAlignmentOptions.Left);
        SetAnchors(stepsLabel, new Vector2(0f, 0.745f), new Vector2(1f, 0.80f),
            new Vector2(18, 2), new Vector2(-18, -2));

        // ── 5 Step Cards ──────────────────────────────────────────────────
        // Vertical region: 0.17 → 0.745 (total 0.575). Each card: ~0.107
        float cardTop   = 0.745f;
        float cardH     = 0.108f;
        float cardGap   = 0.007f;

        GameObject[] stepCards          = new GameObject[5];
        Image[]      stepDots           = new Image[5];
        TextMeshProUGUI[] stepNumbers   = new TextMeshProUGUI[5];
        TextMeshProUGUI[] stepDescs     = new TextMeshProUGUI[5];

        string[] stepNums = { "01", "02", "03", "04", "05" };
        string[] stepTexts =
        {
            "Flask filled with\n<color=#D4A017><b>iodine solution</b></color> (yellow-brown)",
            "Tilt <color=#4FC3F7><b>beaker</b></color> over flask to pour\n<color=#7986CB><b>starch indicator</b></color> — flask turns deep blue",
            "Rotate <color=#80DEEA><b>nozzle handle</b></color> to open burette\nand release <color=#E0F7FA><b>Na\u2082S\u2082O\u2083</b></color> dropwise",
            "Na\u2082S\u2082O\u2083 reacts with I\u2082\n<color=#7986CB><b>Deep blue</b></color> fades to <color=#E0F7FA><b>colourless</b></color>",
            "<color=#69F0AE><b>Endpoint Reached!</b></color>\nSolution is colourless — record burette volume"
        };

        for (int i = 0; i < 5; i++)
        {
            float top    = cardTop - i * (cardH + cardGap);
            float bottom = top - cardH;

            // Card background
            GameObject card = MakePanel($"StepCard_{i + 1}", bg.transform,
                new Color(0.08f, 0.12f, 0.22f, 0.88f),
                new Vector2(0f, bottom), new Vector2(1f, top),
                new Vector2(10, 4), new Vector2(-10, -4));
            stepCards[i] = card;

            // Coloured left accent bar
            GameObject accent = MakePanel($"Accent", card.transform,
                new Color(0.25f, 0.30f, 0.50f, 1f),
                new Vector2(0f, 0f), new Vector2(0.018f, 1f),
                Vector2.zero, Vector2.zero);
            Image accentImg = accent.GetComponent<Image>();
            accentImg.color = new Color(0.25f, 0.30f, 0.50f, 1f);
            stepDots[i]     = accentImg;

            // Step number
            GameObject numGO = MakeTMP($"StepNum", card.transform,
                $"<b>{stepNums[i]}</b>",
                22, new Color(0.40f, 0.42f, 0.60f, 1f), TextAlignmentOptions.Center);
            SetAnchors(numGO, new Vector2(0.018f, 0f), new Vector2(0.14f, 1f),
                new Vector2(2, 2), new Vector2(-2, -2));
            stepNumbers[i] = numGO.GetComponent<TextMeshProUGUI>();

            // Separator line
            GameObject sep = MakePanel("Sep", card.transform,
                new Color(0.20f, 0.22f, 0.38f, 0.6f),
                new Vector2(0.14f, 0.1f), new Vector2(0.148f, 0.9f),
                Vector2.zero, Vector2.zero);

            // Step description
            GameObject descGO = MakeTMP($"StepDesc", card.transform,
                $"<color=#4A5070>{stepTexts[i]}</color>",
                15, Color.white, TextAlignmentOptions.Left);
            SetAnchors(descGO, new Vector2(0.155f, 0f), new Vector2(1f, 1f),
                new Vector2(4, 4), new Vector2(-10, -4));
            TextMeshProUGUI descTMP = descGO.GetComponent<TextMeshProUGUI>();
            descTMP.textWrappingMode = TextWrappingModes.Normal;
            stepDescs[i] = descTMP;
        }

        // ── Divider ───────────────────────────────────────────────────────
        MakePanel("Divider", bg.transform,
            new Color(0.15f, 0.22f, 0.42f, 0.8f),
            new Vector2(0f, 0.163f), new Vector2(1f, 0.167f),
            new Vector2(10, 0), new Vector2(-10, 0));

        // ── Status card ───────────────────────────────────────────────────
        GameObject statusCard = MakePanel("StatusCard", bg.transform,
            new Color(0.10f, 0.12f, 0.22f, 0.95f),
            new Vector2(0f, 0.075f), new Vector2(1f, 0.163f),
            new Vector2(10, 4), new Vector2(-10, -4));

        GameObject statusGO = MakeTMP("Status", statusCard.transform,
            "<color=#6B7A99>Press  <b>Start Titration</b>  to begin.</color>",
            18, Color.white, TextAlignmentOptions.Center);
        SetAnchors(statusGO, Vector2.zero, Vector2.one,
            new Vector2(10, 4), new Vector2(-10, -4));
        Image statusPanel = statusCard.GetComponent<Image>();

        // ── Progress bar ──────────────────────────────────────────────────
        // Label
        GameObject progLabel = MakeTMP("ProgressLabel", bg.transform,
            "Titration  <b>0%</b> complete",
            15, new Color(0.75f, 0.85f, 1f), TextAlignmentOptions.Center);
        SetAnchors(progLabel, new Vector2(0f, 0.043f), new Vector2(1f, 0.075f),
            new Vector2(14, 0), new Vector2(-14, 0));

        // Bar background
        GameObject barBg = MakePanel("ProgressBarBg", bg.transform,
            new Color(0.12f, 0.15f, 0.28f, 1f),
            new Vector2(0f, 0.010f), new Vector2(1f, 0.043f),
            new Vector2(14, 4), new Vector2(-14, -4));

        // Bar fill
        GameObject barFill = MakePanel("ProgressBarFill", barBg.transform,
            new Color(0.22f, 0.34f, 0.90f, 1f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image fillImg = barFill.GetComponent<Image>();
        fillImg.type       = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillAmount = 0f;
        fillImg.color      = new Color(0.22f, 0.34f, 0.90f, 1f);

        // ── Wire TitrationDashboardUI ─────────────────────────────────────
        TitrationDashboardUI dashUI = canvasGO.AddComponent<TitrationDashboardUI>();
        dashUI.titrationController = ctrl;
        dashUI.titleText           = titleGO.GetComponent<TextMeshProUGUI>();
        dashUI.equationText        = eqLabel.GetComponent<TextMeshProUGUI>();
        dashUI.stepCards           = stepCards;
        dashUI.stepDots            = stepDots;
        dashUI.stepNumberLabels    = stepNumbers;
        dashUI.stepDescLabels      = stepDescs;
        dashUI.statusPanel         = statusPanel;
        dashUI.statusText          = statusGO.GetComponent<TextMeshProUGUI>();
        dashUI.progressText        = progLabel.GetComponent<TextMeshProUGUI>();
        dashUI.progressBarFill     = fillImg;
        dashUI.progressBarBg       = barBg.GetComponent<Image>();

        EditorUtility.SetDirty(dashUI);
        Debug.Log("[Rebuild] TitrationDashboard rebuilt with 5 step cards.");
    }

    // ======================================================================
    // MISSING WIRING
    // ======================================================================
    static void WireMissingReferences(GameObject titration, IodineTitrationController ctrl)
    {
        // Wire TitrationSetupManager → IodineTitrationController
        GameObject gm = GameObject.Find("GameManager");
        if (gm != null)
        {
            TitrationSetupManager setupMgr = gm.GetComponent<TitrationSetupManager>();
            if (setupMgr != null)
            {
                // TitrationSetupManager now delegates to ExperimentManager — wire that instead.
                if (setupMgr.experimentManager == null)
                    setupMgr.experimentManager = gm.GetComponent<ExperimentManager>();
                EditorUtility.SetDirty(setupMgr);
                Debug.Log("[Rebuild] TitrationSetupManager wired to ExperimentManager.");
            }
        }

        // Wire NozzlePokeResponder on NozzleHandle
        Transform nozzle       = titration.transform.Find("BuretteStand/Burette/BuretteNozzle");
        Transform nozzleHandle = nozzle != null ? nozzle.Find("NozzleHandle") : null;

        if (nozzleHandle != null && nozzle != null)
        {
            NozzlePokeResponder responder = nozzleHandle.GetComponent<NozzlePokeResponder>();
            if (responder == null)
                responder = Undo.AddComponent<NozzlePokeResponder>(nozzleHandle.gameObject);

            BuretteNozzleController nozzleCtrl = nozzle.GetComponent<BuretteNozzleController>();
            if (nozzleCtrl != null)
            {
                responder.nozzleController = nozzleCtrl;
                EditorUtility.SetDirty(responder);
                Debug.Log("[Rebuild] NozzlePokeResponder wired to BuretteNozzleController.");
            }
        }

        // Fix StarchPourDetector thresholds
        Transform beakerT = titration.transform.Find("Beaker");
        if (beakerT != null)
        {
            StarchPourDetector det = beakerT.GetComponent<StarchPourDetector>();
            if (det != null)
            {
                det.tiltThreshold    = 55f;
                det.maxPourDistance  = 0.4f;
                det.pourHoldDuration = 0.4f;
                EditorUtility.SetDirty(det);
                Debug.Log("[Rebuild] StarchPourDetector thresholds updated.");
            }
        }

        // Fix titration progress per drop (~60 drops to endpoint)
        ctrl.progressPerDrop = 0.016f;
        EditorUtility.SetDirty(ctrl);
    }

    // ======================================================================
    // UI FACTORY HELPERS
    // ======================================================================
    static GameObject MakePanel(string name, Transform parent, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        go.layer = LayerMask.NameToLayer("UI");

        go.AddComponent<CanvasRenderer>();
        Image img = go.AddComponent<Image>();
        img.color = color;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin   = anchorMin;
        rt.anchorMax   = anchorMax;
        rt.offsetMin   = offsetMin;
        rt.offsetMax   = offsetMax;
        rt.anchoredPosition = Vector2.zero;
        return go;
    }

    static GameObject MakeTMP(string name, Transform parent, string text,
        float fontSize, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        go.layer = LayerMask.NameToLayer("UI");

        go.AddComponent<CanvasRenderer>();
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text               = text;
        tmp.fontSize           = fontSize;
        tmp.color              = color;
        tmp.alignment          = alignment;
        tmp.richText           = true;
        tmp.textWrappingMode   = TextWrappingModes.Normal;
        tmp.overflowMode       = TextOverflowModes.Overflow;
        tmp.raycastTarget      = false;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go;
    }

    static void SetAnchors(GameObject go, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        rt.anchoredPosition = Vector2.zero;
    }
}
