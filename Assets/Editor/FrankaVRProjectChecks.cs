using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FrankaVR.Teleop;
using FrankaVR.Welding;
using FrankaVR.V5;
using FrankaVR.V6;

/// <summary>Prueba de integracion en el Editor. No guarda cambios de Play en la escena.</summary>
[InitializeOnLoad]
public static class FrankaVRProjectChecks
{
    const string RunningKey = "FrankaVR.ProjectChecks.Running";
    const string ControlKey = "FrankaVR.ProjectChecks.ControlOnly";
    const string SceneryKey = "FrankaVR.ProjectChecks.SceneryOnly";
    const string SeamKey = "FrankaVR.ProjectChecks.SeamOnly";
    static IEnumerator _steps;
    static int _lastFrame = -1;
    static double _deadline;

    static FrankaVRProjectChecks()
    {
        if (UnityEditor.SessionState.GetBool(RunningKey, false)) Subscribe();
    }

    // Unity.exe -batchmode -projectPath ... -executeMethod FrankaVRProjectChecks.RunBatch
    [MenuItem("FrankaVR/Verificacion/Integracion N1-N5")]
    public static void RunBatch()
    {
        UnityEditor.SessionState.SetBool(SceneryKey, false);
        UnityEditor.SessionState.SetBool(SeamKey, false);
        UnityEditor.SessionState.SetBool(ControlKey, false);
        EditorSceneManager.OpenScene("Assets/Main.unity", OpenSceneMode.Single);
        ValidateScene();
        UnityEditor.SessionState.SetBool(RunningKey, true);
        Subscribe();
        EditorApplication.EnterPlaymode();
    }

    // Solo capturas del entorno: iteracion visual rapida, sin prueba de control.
    [MenuItem("FrankaVR/Verificacion/Capturas del entorno")]
    public static void RunSceneryBatch()
    {
        EditorSceneManager.OpenScene("Assets/Main.unity", OpenSceneMode.Single);
        UnityEditor.SessionState.SetBool(ControlKey, false);
        UnityEditor.SessionState.SetBool(SceneryKey, true);
        UnityEditor.SessionState.SetBool(SeamKey, false);
        UnityEditor.SessionState.SetBool(RunningKey, true);
        Subscribe();
        EditorApplication.EnterPlaymode();
    }

    [MenuItem("FrankaVR/Verificacion/Cordon completo N1")]
    public static void RunSeamBatch()
    {
        EditorSceneManager.OpenScene("Assets/Main.unity", OpenSceneMode.Single);
        UnityEditor.SessionState.SetBool(ControlKey, false);
        UnityEditor.SessionState.SetBool(SceneryKey, false);
        UnityEditor.SessionState.SetBool(SeamKey, true);
        UnityEditor.SessionState.SetBool(RunningKey, true);
        Subscribe();
        EditorApplication.EnterPlaymode();
    }

    [MenuItem("FrankaVR/Verificacion/Control de punta")]
    public static void RunControlBatch()
    {
        UnityEditor.SessionState.SetBool(SceneryKey, false);
        UnityEditor.SessionState.SetBool(SeamKey, false);
        EditorSceneManager.OpenScene("Assets/Main.unity", OpenSceneMode.Single);
        ValidateScene();
        UnityEditor.SessionState.SetBool(ControlKey, true);
        UnityEditor.SessionState.SetBool(RunningKey, true);
        Subscribe();
        EditorApplication.EnterPlaymode();
    }

    public static void BuildDesktop()
    {
        EditorSceneManager.OpenScene("Assets/Main.unity", OpenSceneMode.Single);
        ValidateScene();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Main.unity" },
            locationPathName = "Builds/FrankaVR_PC/FrankaVR.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        bool success = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
        Directory.CreateDirectory("Verificacion");
        File.WriteAllText("Verificacion/Resultado_Build_PC.txt", report.summary.result + "\n" +
            "Errores: " + report.summary.totalErrors + "\nBytes: " + report.summary.totalSize);
        EditorApplication.Exit(success ? 0 : 1);
    }

    static void Subscribe()
    {
        _deadline = EditorApplication.timeSinceStartup + 300;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
    }

    static void OnLog(string message, string trace, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            Complete(false, message + "\n" + trace);
    }

    static void Tick()
    {
        if (EditorApplication.timeSinceStartup > _deadline)
        { Complete(false, "Timeout al entrar o ejecutar Play"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.isPaused || Time.frameCount == _lastFrame) return;
        _lastFrame = Time.frameCount;
        try
        {
            bool controlOnly = UnityEditor.SessionState.GetBool(ControlKey, false);
            bool sceneryOnly = UnityEditor.SessionState.GetBool(SceneryKey, false);
            bool seamOnly = UnityEditor.SessionState.GetBool(SeamKey, false);
            if (_steps == null) _steps = seamOnly ? SeamSmoke() : sceneryOnly ? ScenerySmoke() : controlOnly ? ControlSmoke() : Smoke();
            if (!_steps.MoveNext()) Complete(true, seamOnly ? "Cordon completo N1 con teclas." : sceneryOnly ? "Capturas del entorno generadas." : controlOnly ? "Control de punta N1: aproximacion, avance, arco, orientacion, superficies y exclusion de examen verificados." :
                "Referencias, curso N1-N5, superficies, CSV, timeouts, cierre, deposicion y control de punta N1 verificados.");
        }
        catch (Exception ex) { Complete(false, ex.ToString()); }
    }

    static T One<T>() where T : MonoBehaviour
    {
        T[] components = UnityEngine.Object.FindObjectsByType<T>();
        Require(components.Length == 1, "Se esperaba exactamente un " + typeof(T).Name);
        return components[0];
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    static void ValidateScene()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) == 0,
                    "Missing script en " + child.name);
        var teleop = One<WeldingTeleopController>();
        var hybrid = One<HybridTeleopInput>();
        var views = One<ViewpointManager>();
        Require(teleop.InputSource == hybrid && hybrid.Keyboard != null && hybrid.Glove != null, "Entrada hibrida incompleta");
        Require(teleop.Ik.endEffector.name == "Soldador" && !teleop.ClampHeight, "Restriccion de IK modificada");
        Require(teleop.Machine == teleop.Torch.Machine && teleop.Machine != null, "Maquina no enlazada");
        Require(teleop.MovementReference == views.rigRoot && views.rigRoot != views.playerCamera.transform, "La cabeza no esta separada del cuerpo");
        Require(!teleop.AutoStartTask && One<CourseTrainingManagerV5>().PrepareOnStart, "Arranque del curso ambiguo");
        Require(One<DesktopNavigatorV6>().RigRoot == views.rigRoot, "Navegador desconectado del rig");
        Require(One<VRTrainingHUDV6>().enabled && One<CourseWorldMenuV6>().enabled, "UI principal incompleta");
    }

    static IEnumerator Smoke()
    {
        for (int i = 0; i < 12; i++) yield return null;
        ValidateScene();
        var course = One<CourseTrainingManagerV5>();
        var task = One<WeldingTaskManager>();
        var torch = One<WeldingTorch>();
        var teleop = One<WeldingTeleopController>();
        var hybrid = One<HybridTeleopInput>();
        var bridge = One<GlovePacketBridge>();
        var factory = One<TrainingWorkpieceFactoryV5>();
        course.ResultsDirectoryOverride = "Verificacion/Resultados_prueba";
        Require(course.Phase == CoursePhaseV5.Selection && task.State == FrankaVR.Welding.SessionState.Briefing,
            "La sesion arranco sin seleccionar modo");
        Require(!hybrid.GloveMotionActive, "El guante toma control sin paquetes");
        Require(GameObject.Find("VRTrainingHUDV6_Runtime") != null && GameObject.Find("CourseWorldMenuV6_Runtime") != null, "UI no construida");
        CheckPanels();
        Capture("01_seleccion");
        One<GazeInteractor>();
        GazeButton selector = null;
        foreach (GazeButton button in UnityEngine.Object.FindObjectsByType<GazeButton>())
            if (button.label == "N2 · TOPE") selector = button;
        Require(selector != null && selector.GetComponent<Collider>().isTrigger, "Seleccion por puntero no preparada");
        Physics.SyncTransforms();
        Require(Physics.Raycast(Camera.main.transform.position, (selector.transform.position - Camera.main.transform.position).normalized,
            out RaycastHit uiHit, 5f, 1 << 5, QueryTriggerInteraction.Collide) && uiHit.collider.GetComponent<GazeButton>() == selector,
            "El puntero no alcanza el boton de ejercicio");
        selector.Activate();
        Require(course.SelectedExercise == 1 && course.Phase == CoursePhaseV5.Briefing, "Boton no selecciona N2");
        course.BackToMenu(); course.SelectExercise(0); course.BackToMenu();
        var navigator = One<DesktopNavigatorV6>();
        float originalFov = Camera.main.fieldOfView;
        Vector3 rigPosition = navigator.RigRoot.position;
        navigator.ZoomBy(100f);
        Require(Mathf.Approximately(Camera.main.fieldOfView, navigator.MinFieldOfView), "Zoom no respeta limite");
        for (int i = 0; i < 3; i++) yield return null;
        Require(navigator.RigRoot.position == rigPosition, "Zoom movio el cuerpo");
        CheckPanels();
        Capture("05_zoom");
        navigator.ResetZoom();
        Require(Mathf.Approximately(Camera.main.fieldOfView, originalFov), "Zoom no vuelve al valor inicial");
        bridge.OnBleMessage("IMU,NaN,0,0,0,1");
        bridge.OnBleMessage("IMU,0,0,0,0,1");
        Require(!hybrid.Glove.HasImuPacket, "Un paquete invalido tomo el control");
        bridge.OnBleMessage("IMU,1,0,0,0,1");
        hybrid.Poll(teleop.Ik.endEffector.rotation);
        Require(hybrid.GloveMotionActive && hybrid.Glove.IsCalibrated, "CSV IMU no llega a la entrada hibrida");
        float amps = teleop.Machine.Amperage;
        bridge.OnBleMessage("BTN,0,1,0,0,0,0,0");
        for (int i = 0; i < 3; i++) yield return null;
        Require(Mathf.Approximately(teleop.Machine.Amperage, amps + teleop.Machine.AmpStep), "Amperaje no responde al comando");
        int electrode = teleop.Machine.ElectrodeIndex;
        bridge.OnBleMessage("BTN,0,0,0,1,0,0,0");
        for (int i = 0; i < 3; i++) yield return null;
        Require(teleop.Machine.ElectrodeIndex == (electrode + 1) % teleop.Machine.ElectrodeCount, "Electrodo no cambia");
        float timeout = Time.unscaledTime + Mathf.Max(hybrid.Glove.PrimaryTimeoutSec, hybrid.Glove.ButtonsTimeoutSec) + 0.1f;
        while (Time.unscaledTime < timeout) yield return null;
        Require(!hybrid.GloveMotionActive && !hybrid.Glove.ClutchHeld && !hybrid.Glove.SparkHeld, "Timeout no libera el mando");
        Debug.Log("[FrankaVR Checks] CSV, comandos y retorno al teclado OK");

        for (int exercise = 0; exercise < 5; exercise++)
        {
            course.SelectExercise(exercise);
            for (int i = 0; i < 3; i++) yield return null;
            Require(task.WorkOrderId == "N" + (exercise + 1) && task.Seam.Count >= 60, "Orden o costura incorrecta");
            Require(!factory.LegacyChapa.gameObject.activeSelf, "Chapa antigua solapada");
            Physics.SyncTransforms();
            int surfaceHits = 0;
            for (int i = 0; i < task.Seam.Count; i++)
            {
                Vector3 point = task.Seam.PointAt(i);
                Vector3 normal = task.Seam.NormalAt(i);
                if (Physics.Raycast(point + normal * 0.03f, -normal, out RaycastHit hit, 0.06f, 1 << 6))
                {
                    Require(Vector3.Distance(hit.point, point) < 0.005f, "Superficie separada de la costura " + task.WorkOrderId);
                    surfaceHits++;
                }
            }
            Require(surfaceHits == task.Seam.Count, "Costura sin superficie soldable: " + task.WorkOrderId + " " + surfaceHits + "/" + task.Seam.Count);
            if (task.Order.Joint == JointType.FilletT)
            {
                int sample = task.Seam.Count / 2;
                Vector3 normal = task.Seam.NormalAt(sample);
                Vector3 side = Vector3.Cross(normal, task.Seam.TangentAt(sample)).normalized;
                float angle = task.Seam.IdealWorkAngle * Mathf.Deg2Rad; // con el signo de la costura real
                Vector3 handDirection = normal * Mathf.Cos(angle) + side * Mathf.Sin(angle);
                Vector3 point = task.Seam.PointAt(sample);
                Require(Physics.Raycast(point + handDirection * 0.06f, -handDirection, out RaycastHit hit, 0.08f, 1 << 6)
                    && Vector3.Distance(hit.point, point) < 0.005f, "La chapa vertical bloquea el angulo recomendado de N3");
            }
            CheckPanels();
            Debug.Log("[FrankaVR Checks] " + task.WorkOrderId + " superficie OK: " + surfaceHits + " puntos");
        }
        Capture("02_instrucciones_N5");

        course.StartTraining();
        var before = task.Seam.Get(0);
        task.Seam.Deposit(before.Pos, before.Normal, 2000f, 1f, 0.1f);
        float deposited = task.Seam.Get(0).Energy;
        Require(course.IndexPipe(180f), "N5 no permite giro durante practica");
        Require(Mathf.Approximately(task.Seam.Get(0).Energy, deposited), "El giro borra la energia depositada");
        Require(Vector3.Distance(task.Seam.PointAt(0), before.Pos) > 0.10f, "El giro no transforma la costura");
        torch.SetSparkRequested(true);
        Require(!course.IndexPipe(15f), "La pieza gira con chispa solicitada");
        torch.SetSparkRequested(false);
        course.FinishExercise();
        course.StartExam();
        Require(!course.IndexPipe(15f), "El caño gira en examen fijo");
        course.FinishExercise();

        course.SelectExercise(0);
        Require(task.Seam.Count == factory.LineSamples, "N1 hereda la densidad de muestras de N5");
        course.StartExam();
        for (int i = 0; i < 3; i++) yield return null;
        Require(!GameObject.Find("SeamPathGuide").GetComponent<LineRenderer>().enabled, "El examen muestra la guia");
        course.FinishExercise();
        bridge.OnBleMessage("BTN,1,0,0,0,0,0,0");
        for (int i = 0; i < 3; i++) yield return null;
        Require(!torch.SparkRequested, "El arco se reactiva despues de finalizar");
        course.BackToMenu();
        course.StartTraining();
        for (int i = 0; i < 3; i++) yield return null;
        Require(task.State == FrankaVR.Welding.SessionState.Running && GameObject.Find("SeamPathGuide").GetComponent<LineRenderer>().enabled, "La practica no muestra su guia");
        var hud = One<VRTrainingHUDV6>();
        hud.ShowHelpDuringRunning = true;
        for (int i = 0; i < 3; i++) yield return null;
        Require(GameObject.Find("CourseWorldMenuV6_Runtime") != null, "La ayuda no aparece durante practica");
        CheckPanels();
        Capture("06_ayuda");
        hud.ShowHelpDuringRunning = false;

        IEnumerator reachAudit = AuditReach(course, task, teleop, torch);
        while (reachAudit.MoveNext()) yield return reachAudit.Current;
        Require(Mathf.Approximately(teleop.Ik.toleranceMeters, 0.005f) && Mathf.Approximately(teleop.Ik.toleranceDegrees, 3f),
            "Precision no restaura las tolerancias de la escena");
        IEnumerator controlAudit = AuditTipControl(course, task, teleop, torch);
        while (controlAudit.MoveNext()) yield return controlAudit.Current;
        course.BackToMenu();
        course.SelectExercise(0);
        course.StartTraining();

        // Colocacion controlada para probar arco/deposicion; no valida alcance del IK.
        teleop.enabled = false;
        teleop.Ik.enabled = false;
        // Aislar la herramienta de los ArticulationBody, que siguen asentandose
        // aunque el solver este detenido. La prueba no cambia el robot guardado.
        torch.transform.SetParent(null, true);
        int midpoint = task.Seam.Count / 2;
        Vector3 weldPoint = task.Seam.PointAt(midpoint);
        Vector3 weldNormal = task.Seam.NormalAt(midpoint);
        torch.transform.rotation = Quaternion.FromToRotation(torch.PointingDir, -weldNormal) * torch.transform.rotation;
        torch.transform.position += weldPoint + weldNormal * 0.00325f - torch.ArcOriginPosition;
        Physics.SyncTransforms();
        torch.SetSparkRequested(true);
        float weldUntil = Time.unscaledTime + 0.9f;
        while (Time.unscaledTime < weldUntil) yield return null;
        Capture("03_practica");
        Require(torch.Arcing && task.Seam.Get(midpoint).Energy > 0f,
            "No hay deposicion N1: estado=" + torch.State + ", arco=" + torch.ArcLenMm +
            ", superficie=" + torch.HasArcSurface + ", chispa=" + torch.SparkRequested +
            ", energia=" + task.Seam.Get(midpoint).Energy);
        Debug.Log("[FrankaVR Checks] Deposicion N1 OK; energia=" + task.Seam.Get(midpoint).Energy + "; arco=" + torch.ArcLenMm);
        course.FinishExercise();
        for (int i = 0; i < 3; i++) yield return null;
        Require(task.State == FrankaVR.Welding.SessionState.Finished && !torch.SparkRequested, "Finalizacion incorrecta");
        CheckPanels();
        Capture("04_resultados");
        Require(File.Exists(course.LastResultPath) && File.Exists(Path.ChangeExtension(course.LastResultPath, ".csv")), "No se exportaron JSON/CSV");
        var saved = JsonUtility.FromJson<WeldingSessionReport.Result>(File.ReadAllText(course.LastResultPath));
        Require(saved.exercise == "N1" && Mathf.Approximately(saved.score, task.FinalScore), "Resultado exportado no coincide con la sesion");
    }

    static IEnumerator ControlSmoke()
    {
        float until = Time.unscaledTime + 5f;
        while (Time.unscaledTime < until) yield return null;
        var course = One<CourseTrainingManagerV5>();
        course.ResultsDirectoryOverride = "Verificacion/Resultados_prueba";
        IEnumerator audit = AuditTipControl(course, One<WeldingTaskManager>(),
            One<WeldingTeleopController>(), One<WeldingTorch>());
        while (audit.MoveNext()) yield return audit.Current;
        IEnumerator seam = AuditFullSeam(course, One<WeldingTaskManager>(),
            One<WeldingTeleopController>(), One<WeldingTorch>());
        while (seam.MoveNext()) yield return seam.Current;
        CheckPanels();
        GameObject scenery = GameObject.Find("IndustrialWorkshopDetails");
        Require(scenery != null, "No se genera el taller industrial");
        foreach (Collider collider in scenery.GetComponentsInChildren<Collider>())
            Require(!collider.enabled, "Un adorno industrial altera la fisica");
        Require(GameObject.Find("WorkshopOperator") != null, "No aparece el operario");
        var camera = Camera.main;
        Vector3 cameraPosition = camera.transform.position;
        Quaternion cameraRotation = camera.transform.rotation;
        camera.transform.position = new Vector3(-2.5f, 1.85f, -3.1f);
        camera.transform.LookAt(new Vector3(0f, 1.35f, 1.3f));
        for (int i = 0; i < 3; i++) yield return null;
        Capture("10_taller_industrial");
        camera.transform.position = new Vector3(0.65f,1.55f,0.5f);
        camera.transform.LookAt(new Vector3(2.25f,1.05f,2.65f));
        for (int i = 0; i < 3; i++) yield return null;
        Capture("11_operario");
        camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
        IEnumerator line = LineCaptures();
        while (line.MoveNext()) yield return line.Current;
    }

    const bool DiagnoseFirstContact = false;

    static string Geometry(WeldingTeleopController teleop, WeldingTorch torch)
    {
        var sb = new System.Text.StringBuilder("geometria (m, mundo):\n");
        Transform ee = teleop.Ik.endEffector;
        void Row(string label, Vector3 p) => sb.Append(label).Append(' ').Append(p.ToString("F3"))
            .Append(" rel_EE=").Append(ee.InverseTransformPoint(p).ToString("F3")).Append('\n');
        Row("EE", ee.position);
        Row("hand", teleop.Ik.toolReference != null ? teleop.Ik.toolReference.position : ee.position);
        Row("Tip", torch.Tip.position);
        Row("ArcOrigin", torch.ArcOriginPosition);
        sb.Append("PointingDir ").Append(torch.PointingDir.ToString("F3")).Append(" Remaining ").Append(torch.RemainingMm.ToString("0")).Append('\n');
        foreach (Collider c in ee.GetComponentsInChildren<Collider>(true))
            sb.Append("col ").Append(c.name).Append(" enabled=").Append(c.enabled).Append(" min=").Append(c.bounds.min.ToString("F3"))
              .Append(" max=").Append(c.bounds.max.ToString("F3")).Append('\n');
        GameObject plate = GameObject.Find("Plate_N1");
        if (plate != null) sb.Append("Plate_N1 max=").Append(plate.GetComponent<Collider>().bounds.max.ToString("F3")).Append('\n');
        return sb.ToString();
    }

    // Penetraciones reales de los colliders del robot contra cualquier otro collider.
    static string RobotContacts(Transform robot)
    {
        var sb = new System.Text.StringBuilder();
        var others = new Collider[64];
        foreach (Collider mine in robot.GetComponentsInChildren<Collider>())
        {
            if (!mine.enabled || mine.isTrigger) continue;
            Bounds b = mine.bounds;
            int n = Physics.OverlapBoxNonAlloc(b.center, b.extents + Vector3.one * 0.003f, others, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider other = others[i];
                if (other == mine) continue;
                ArticulationBody a = mine.GetComponentInParent<ArticulationBody>(), o = other.GetComponentInParent<ArticulationBody>();
                if (a != null && o != null && (a == o || a.transform.parent == o.transform || o.transform.parent == a.transform)) continue;
                if (Physics.ComputePenetration(mine, mine.transform.position, mine.transform.rotation,
                        other, other.transform.position, other.transform.rotation, out Vector3 dir, out float depth))
                    sb.Append(mine.name).Append(" (").Append(a != null ? a.name : "-").Append(") vs ").Append(other.name)
                      .Append(" (").Append(o != null ? o.name : "-").Append(") ").Append((depth * 1000f).ToString("0.0")).Append(" mm\n");
            }
        }
        return sb.Length > 0 ? sb.ToString() : "(sin penetraciones)";
    }

    static IEnumerator SeamSmoke()
    {
        float until = Time.unscaledTime + 5f;
        while (Time.unscaledTime < until) yield return null;
        var course = One<CourseTrainingManagerV5>();
        course.ResultsDirectoryOverride = "Verificacion/Resultados_prueba";
        One<WeldingTeleopController>().enabled = false;
        IEnumerator seam = AuditFullSeam(course, One<WeldingTaskManager>(), One<WeldingTeleopController>(), One<WeldingTorch>());
        while (seam.MoveNext()) yield return seam.Current;
    }

    static IEnumerator ScenerySmoke()
    {
        float until = Time.unscaledTime + 8f;
        while (Time.unscaledTime < until) yield return null;
        Require(GameObject.Find("IndustrialWorkshopDetails") != null, "No se genera el taller industrial");
        Capture("01_seleccion");
        IEnumerator line = LineCaptures();
        while (line.MoveNext()) yield return line.Current;
    }

    static IEnumerator LineCaptures()
    {
        Require(GameObject.Find("LineAGV") != null, "No aparece la linea de fabrica");
        foreach (Collider collider in GameObject.Find("IndustrialWorkshopDetails").GetComponentsInChildren<Collider>())
            Require(!collider.enabled, "La escenografia de la nave altera la fisica");
        var camera = Camera.main;
        Vector3 cameraPosition = camera.transform.position;
        Quaternion cameraRotation = camera.transform.rotation;
        var views = new[] {
            new[] { new Vector3(1.55f, 1.5f, 0.07f), new Vector3(-8f, 1.0f, -1.5f) },
            new[] { new Vector3(2.5f, 3.2f, -10f), new Vector3(-9f, 1.0f, -2f) },
            new[] { new Vector3(-5.3f, 1.7f, -6.5f), new Vector3(-8.6f, 1.0f, -2f) },
            new[] { new Vector3(-2.5f, 1.85f, -3.1f), new Vector3(0f, 1.35f, 1.3f) } };
        string[] names = { "12_linea_desde_puesto", "13_nave", "14_linea_cerca", "10_taller_industrial" };
        for (int v = 0; v < views.Length; v++)
        {
            camera.transform.position = views[v][0];
            camera.transform.LookAt(views[v][1]);
            for (int i = 0; i < 3; i++) yield return null;
            Capture(names[v]);
        }
        camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
    }

    // Cordon N1 completo solo con las teclas: Espacio + la tecla de avance sobre la costura.
    static IEnumerator AuditFullSeam(CourseTrainingManagerV5 course, WeldingTaskManager task,
        WeldingTeleopController teleop, WeldingTorch torch)
    {
        course.BackToMenu(); course.SelectExercise(0); course.StartTraining();
        for (int i = 0; i < 3; i++) yield return null;
        Physics.SyncTransforms();
        torch.LoadNewElectrode();
        bool relative = teleop.ReferenceRelative;
        teleop.ReferenceRelative = false;
        Vector3 start = task.Seam.PointAt(0), t0 = task.Seam.TangentAt(0);
        teleop.ResetTo(start - t0 * 0.004f + Vector3.up * 0.06f);
        var command = TeleopCommand.Neutral;
        float until = Time.unscaledTime + 4f;
        while (Time.unscaledTime < until) { teleop.ApplyMotion(command, true, Time.deltaTime); yield return null; }
        Require(teleop.BeginAssistedApproach(), "No inicia la aproximacion al comienzo de la costura");
        until = Time.unscaledTime + 30f;
        var approachLog = new System.Text.StringBuilder("aproximacion t,etapa,dist_inicio_mm,altura_mm,ik_err_mm,ik_err_deg,j1..j7\n");
        var armLog = teleop.Ik.GetComponent<PandaArm>();
        float approachT0 = Time.unscaledTime, approachLogAt = 0f;
        while (teleop.Approaching && Time.unscaledTime < until)
        {
            teleop.ApplyMotion(command, true, Time.deltaTime);
            if (Time.unscaledTime >= approachLogAt)
            {
                approachLogAt = Time.unscaledTime + 0.5f;
                approachLog.Append((Time.unscaledTime - approachT0).ToString("0.0")).Append(',').Append(teleop.ApproachStage).Append(',')
                    .Append((Vector3.Distance(torch.ArcOriginPosition, start) * 1000f).ToString("0")).Append(',')
                    .Append(((torch.ArcOriginPosition.y - start.y) * 1000f).ToString("0")).Append(',')
                    .Append((teleop.Ik.ErrorMeters * 1000f).ToString("0.0")).Append(',').Append(teleop.Ik.ErrorDegrees.ToString("0.0"));
                for (int j = 0; j < PandaArm.N; j++) approachLog.Append(',').Append(armLog.CurrentDeg(j).ToString("0"));
                approachLog.Append('\n');
            }
            yield return null;
        }
        File.WriteAllText("Verificacion/Aproximacion_diagnostico.csv", approachLog.ToString());
        Require(Vector3.Distance(torch.ArcOriginPosition, start) < 0.015f,
            "La aproximacion no llego al inicio de la costura: " + (Vector3.Distance(torch.ArcOriginPosition, start) * 1000f).ToString("0") + " mm " + teleop.AssistMessage);
        command.Orientation = Quaternion.FromToRotation(Vector3.down, teleop.Ik.approachDirection);
        command.SparkHeld = true;
        command.MoveClutch = true;
        // Solo la tecla cuyo eje de vista coincide con la tangente, como haria el alumno.
        command.TranslationRate = Mathf.Abs(t0.x) >= Mathf.Abs(t0.z) ? new Vector3(Mathf.Sign(t0.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(t0.z));
        Vector3 end = task.Seam.PointAt(task.Seam.Count - 1);
        until = Time.unscaledTime + 75f;
        int frames = 0, locked = 0, arcing = 0;
        float captureAt = Time.unscaledTime + 25f, logAt = 0f, t0Time = Time.unscaledTime;
        var diag = new System.Text.StringBuilder("t,along_mm,lateral_mm,height_mm,arc_mm,estado,costura,bloqueado,objetivo_mm,ik_err_mm,ik_err_deg,varilla_mm,j1,j2,j3,j4,j5,j6,j7\n");
        var arm = teleop.Ik.GetComponent<PandaArm>();
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        string contacts = null;
        while (Time.unscaledTime < until && Vector3.Distance(torch.ArcOriginPosition, end) > 0.006f)
        {
            teleop.ApplyMotion(command, true, Time.deltaTime);
            torch.SetSparkRequested(!teleop.MotionBlocked);
            if (contacts == null && teleop.Ik.ErrorMeters > 0.008f)
            {
                contacts = RobotContacts(teleop.Ik.transform) + Geometry(teleop, torch);
                if (DiagnoseFirstContact) break;
            }
            if (Time.unscaledTime >= logAt)
            {
                logAt = Time.unscaledTime + 0.5f;
                Vector3 tip = torch.ArcOriginPosition, rel = tip - start;
                Vector3 nrm = task.Seam.NormalAt(0), side = Vector3.Cross(nrm, t0).normalized;
                diag.AppendFormat(inv, "{0:0.0},{1:0.0},{2:0.0},{3:0.0},{4:0.0},{5},{6},{7},{8:0.0},{9:0.0},{10:0.0},{11:0}",
                    Time.unscaledTime - t0Time, Vector3.Dot(rel, t0) * 1000f, Vector3.Dot(rel, side) * 1000f, Vector3.Dot(rel, nrm) * 1000f,
                    torch.ArcLenMm, torch.State, teleop.SeamLocked, teleop.MotionBlocked,
                    Vector3.Distance(tip, teleop.PhysicalTarget) * 1000f, teleop.Ik.ErrorMeters * 1000f, teleop.Ik.ErrorDegrees, torch.RemainingMm);
                for (int j = 0; j < PandaArm.N; j++) diag.Append(',').Append(arm.CurrentDeg(j).ToString("0", inv));
                diag.Append('\n');
            }
            frames++;
            if (teleop.SeamLocked) locked++;
            if (torch.Arcing) arcing++;
            if (Time.unscaledTime > captureAt) { Capture("15_soldando"); captureAt = float.PositiveInfinity; }
            yield return null;
        }
        torch.SetSparkRequested(false);
        command = TeleopCommand.Neutral;
        command.Orientation = Quaternion.FromToRotation(Vector3.down, teleop.Ik.approachDirection);
        command.MoveClutch = true;
        command.TranslationRate = new Vector3(0f, 1f, 0f);
        until = Time.unscaledTime + 1.5f;
        while (Time.unscaledTime < until) { teleop.ApplyMotion(command, true, Time.deltaTime); yield return null; }
        teleop.ReferenceRelative = relative;
        for (int i = 0; i < 10; i++) yield return null;
        task.Seam.Tally(out int good, out int cold, out int burn, out int undercut, out int porosity, out int untouched);
        float goodPct = 100f * good / Mathf.Max(1, task.Seam.Count);
        File.WriteAllText("Verificacion/Cordon_diagnostico.csv", diag.ToString());
        File.WriteAllText("Verificacion/Cordon_resumen.txt", "buenos=" + good + "/" + task.Seam.Count + " frios=" + cold +
            " perforacion=" + burn + " socavado=" + undercut + " porosidad=" + porosity + " sin_soldar=" + untouched +
            " arco=" + (100f * arcing / Mathf.Max(1, frames)).ToString("0") + "% costura=" + (100f * locked / Mathf.Max(1, frames)).ToString("0") +
            "% llegada_mm=" + (Vector3.Distance(torch.ArcOriginPosition, end) * 1000f).ToString("0") +
            "\ncontactos al trabarse:\n" + (contacts ?? "(no se trabo)"));
        Debug.Log("[CordonCompleto] buenos=" + good + "/" + task.Seam.Count + " (" + goodPct.ToString("0") + "%) frios=" + cold +
            " perforacion=" + burn + " socavado=" + undercut + " porosidad=" + porosity + " sin soldar=" + untouched +
            " arco=" + (100f * arcing / Mathf.Max(1, frames)).ToString("0") + "% bloqueo costura=" + (100f * locked / Mathf.Max(1, frames)).ToString("0") +
            "% llegada=" + (Vector3.Distance(torch.ArcOriginPosition, end) * 1000f).ToString("0") + "mm");
        Capture("16_cordon_completo");
        Require(goodPct >= 70f, "Cordon N1 completo con teclas: solo " + goodPct.ToString("0") + "% correcto");
        course.FinishExercise();
        teleop.SetPrecisionSolver(false);
    }

    static IEnumerator AuditTipControl(CourseTrainingManagerV5 course, WeldingTaskManager task,
        WeldingTeleopController teleop, WeldingTorch torch)
    {
        teleop.enabled = false;
        course.BackToMenu(); course.SelectExercise(0); course.StartTraining();
        for (int i = 0; i < 3; i++) yield return null;
        Physics.SyncTransforms();
        int mid = task.Seam.Count / 2;
        Vector3 point = task.Seam.PointAt(mid);
        Vector3 normal = task.Seam.NormalAt(mid);
        Vector3 guarded = teleop.GuardSurface(point + normal * 0.02f, point - normal * 0.02f);
        Require(Vector3.Dot(guarded - point, normal) > 0f, "Proteccion deja atravesar la chapa");
        Require(teleop.NearestSeam(point + task.Seam.TangentAt(mid) * 0.001f,
            out Vector3 projected, out _, out _) && Vector3.Distance(projected, point) < 0.002f,
            "Guia salta entre muestras en vez de interpolar");
        teleop.ResetTo(torch.ArcOriginPosition);
        Require(teleop.BeginAssistedApproach(), "No inicia aproximacion asistida de practica");
        var command = TeleopCommand.Neutral;
        float until = Time.unscaledTime + 36f;
        while (teleop.Approaching && Time.unscaledTime < until)
        {
            teleop.ApplyMotion(command, true, Time.deltaTime);
            yield return null;
        }
        command.Orientation = Quaternion.FromToRotation(Vector3.down, teleop.Ik.approachDirection);
        float settle = Time.unscaledTime + 4f;
        while (Time.unscaledTime < settle)
        { teleop.ApplyMotion(command, true, Time.deltaTime); yield return null; }
        float arc = torch.MeasureArcLenMm();
        Debug.Log("[ControlPunta] Aproximacion arco=" + arc + " error=" + Vector3.Distance(torch.ArcOriginPosition, teleop.PhysicalTarget));
        Require(arc > 0.4f && arc < 15f, "Aproximacion no deja la punta en rango util: " + arc);
        Vector3 start = torch.ArcOriginPosition;
        command.SparkHeld = true;
        command.MoveClutch = true;
        command.TranslationRate = task.Seam.TangentAt(mid) * (0.006f / teleop.MoveSpeed);
        // Marco mundo para una trayectoria conocida, sin inyectar teclas al usuario.
        bool relative = teleop.ReferenceRelative;
        teleop.ReferenceRelative = false;
        until = Time.unscaledTime + 3f;
        int stableFrames = 0;
        while (Time.unscaledTime < until)
        {
            teleop.ApplyMotion(command, true, Time.deltaTime);
            torch.SetSparkRequested(true);
            if (torch.Arcing) stableFrames++;
            yield return null;
        }
        torch.SetSparkRequested(false);
        teleop.ReferenceRelative = relative;
        Debug.Log("[ControlPunta] Avance=" + Vector3.Distance(start, torch.ArcOriginPosition) * 1000f + "mm arco=" + torch.ArcLenMm);
        Require(torch.ArcLenMm > 0.4f && torch.ArcLenMm < 20f, "Control de avance pierde la superficie");
        Require(stableFrames > 10, "Avance guiado no mantiene arco estable");
        Capture("08_control_punta");
        Require(teleop.UsedTrainingAssistance && WeldingSessionReport.Snapshot(task, "Training", false).assistedControl,
            "El resultado no identifica el uso de asistencia");
        Vector3 held = teleop.PhysicalTarget;
        command.SparkHeld = false; command.TranslationRate = Vector3.zero;
        command.Orientation = Quaternion.AngleAxis(10f, Vector3.up) * command.Orientation;
        until = Time.unscaledTime + 4f;
        while (Time.unscaledTime < until)
        { teleop.ApplyMotion(command, true, Time.deltaTime); yield return null; }
        float rotationError = Vector3.Distance(held, torch.ArcOriginPosition);
        Debug.Log("[ControlPunta] Error al orientar=" + rotationError * 1000f + "mm");
        Require(rotationError < 0.006f, "La orientacion desplaza la punta sostenida");
        // Regresion: retirada con Espacio mantenido, antes bloqueada por la ayuda.
        start = torch.ArcOriginPosition;
        command.SparkHeld = true; command.TranslationRate = Vector3.up * (0.006f / teleop.MoveSpeed);
        teleop.ReferenceRelative = false;
        until = Time.unscaledTime + 3f;
        while (Time.unscaledTime < until)
        { teleop.ApplyMotion(command, true, Time.deltaTime); yield return null; }
        float retreat = torch.ArcOriginPosition.y - start.y;
        Debug.Log("[ControlPunta] Retirada con arco pedido=" + retreat * 1000f + "mm");
        Require(teleop.AssistanceSuspended && retreat > 0.012f, "La asistencia impide retirar con Espacio");
        command.SparkHeld = false; command.TranslationRate = Vector3.up * (0.03f / teleop.MoveSpeed);
        start = torch.ArcOriginPosition;
        until = Time.unscaledTime + 2f;
        while (Time.unscaledTime < until)
        { teleop.ApplyMotion(command, true, Time.deltaTime); yield return null; }
        Require(torch.ArcOriginPosition.y - start.y > 0.035f, "No permite retirada libre sin arco");
        teleop.ReferenceRelative = relative;
        Vector3 free = point + normal * 0.05f;
        Require(Vector3.Distance(teleop.GuardSurface(point + normal * 0.002f, free), free) < 0.0001f,
            "El freno de superficies bloquea salir de la chapa");
        Vector3 sliding = teleop.GuardSurface(point + normal*0.002f,
            point + task.Seam.TangentAt(mid)*0.02f - normal*0.02f);
        Require(Vector3.Dot(sliding-point,normal) >= -0.0003f &&
            Vector3.Dot(sliding-point,task.Seam.TangentAt(mid)) > 0.018f,
            "La proteccion cancela el avance tangencial al rozar la chapa");
        command.SparkHeld = false; command.TranslationRate = Vector3.zero;
        // C se usa cerca de la pieza: volver a 20 mm sobre la costura antes de probarla.
        teleop.ResetTo(point + normal * 0.02f);
        until = Time.unscaledTime + 4f;
        while (Time.unscaledTime < until) { teleop.ApplyMotion(command, true, Time.deltaTime); yield return null; }
        start = torch.ArcOriginPosition;
        Require(teleop.BeginSafeRetreat(), "C no inicia retirada");
        until = Time.unscaledTime+5f;
        while (teleop.Retracting && Time.unscaledTime < until)
        { teleop.ApplyMotion(command,true,Time.deltaTime); yield return null; }
        Require(!teleop.Retracting && torch.ArcOriginPosition.y-start.y > 0.06f,
            "C no retira el robot real mas de 60 mm: subida=" + ((torch.ArcOriginPosition.y - start.y) * 1000f).ToString("0") +
            "mm retirando=" + teleop.Retracting + " aviso=" + teleop.AssistMessage);
        Debug.Log("[ControlPunta] Recuperacion C="+(torch.ArcOriginPosition.y-start.y)*1000f+"mm");
        // Bloqueo controlado del servo para verificar que no acumula ordenes.
        teleop.Ik.enabled = false;
        command.TranslationRate = Vector3.right*(0.1f/teleop.MoveSpeed);
        until = Time.unscaledTime+2f;
        while (!teleop.MotionBlocked && Time.unscaledTime < until)
        { teleop.ApplyMotion(command,true,Time.deltaTime); yield return null; }
        Require(teleop.MotionBlocked && !teleop.Ik.solving, "No pausa una orden cuyo brazo no avanza");
        Require(Vector3.Distance(teleop.PhysicalTarget,torch.ArcOriginPosition)<0.002f,
            "La orden bloqueada queda acumulada");
        teleop.Ik.enabled = true;
        command.TranslationRate = Vector3.zero;
        Require(teleop.BeginSafeRetreat(), "No permite recuperar despues de bloqueo");
        until=Time.unscaledTime+5f;
        while (teleop.Retracting && Time.unscaledTime<until)
        { teleop.ApplyMotion(command,true,Time.deltaTime); yield return null; }
        Require(!teleop.MotionBlocked && !teleop.Retracting, "La recuperacion queda bloqueada");
        Capture("09_retirada_taller");
        course.FinishExercise(); course.StartExam();
        Require(!teleop.BeginAssistedApproach(), "La asistencia se activa en examen");
        teleop.SetPrecisionSolver(false);
    }

    static IEnumerator AuditReach(CourseTrainingManagerV5 course, WeldingTaskManager task,
        WeldingTeleopController teleop, WeldingTorch torch)
    {
        // Robot real y solver existente. No se desconecta ni reposiciona la herramienta.
        bool enabled = teleop.enabled;
        teleop.enabled = false;
        teleop.Ik.solving = true;
        var clamp = typeof(WeldingTeleopController).GetMethod("ClampWorkspace",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var csv = new System.Text.StringBuilder("ejercicio,precision,punto,error_punta_mm,error_direccion_grados,arco_mm,objetivo_limitado\n");
        var weldCsv = new System.Text.StringBuilder("ejercicio,energia_j,arco_estable,arco_mm\n");
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        for (int exercise = 0; exercise < 5; exercise++)
        {
            course.BackToMenu();
            // Retirar antes de cambiar geometria, como hace el operador al volver
            // al menu. Evita arrancar la aproximacion desde dentro de la nueva junta.
            Vector3 clearance = torch.ArcOriginPosition + Vector3.up * 0.12f;
            float retractUntil = Time.unscaledTime + 3f;
            while (Time.unscaledTime < retractUntil)
            {
                Vector3 desired = teleop.Ik.endEffector.position + clearance - torch.ArcOriginPosition;
                teleop.Ik.target.position = (Vector3)clamp.Invoke(teleop, new object[] { desired });
                yield return null;
            }
            course.SelectExercise(exercise);
            for (int i = 0; i < 3; i++) yield return null;
            Physics.SyncTransforms();
            int sample = task.Seam.Count / 2;
            if (exercise == 4)
                for (int i = 0; i < task.Seam.Count; i++)
                    if (task.Seam.PointAt(i).y > task.Seam.PointAt(sample).y) sample = i;
            Vector3 normal = task.Seam.NormalAt(sample);
            Vector3 tangent = task.Seam.TangentAt(sample);
            Vector3 side = Vector3.Cross(normal, tangent).normalized;
            Vector3 hand = (normal + tangent * Mathf.Tan(task.Order.IdealTravelAngle * Mathf.Deg2Rad) +
                side * Mathf.Tan(task.Seam.IdealWorkAngle * Mathf.Deg2Rad)).normalized;
            Vector3 point = task.Seam.PointAt(sample) + hand * 0.00325f;
            teleop.Ik.approachDirection = -hand;
            for (int precision = 0; precision < 2; precision++)
            {
                teleop.SetPrecisionSolver(precision == 1);
                float until = Time.unscaledTime + (exercise == 0 && precision == 0 ? 12f : 8f);
                bool limited = false;
                while (Time.unscaledTime < until)
                {
                    Vector3 desired = teleop.Ik.endEffector.position + point - torch.ArcOriginPosition;
                    Vector3 bounded = (Vector3)clamp.Invoke(teleop, new object[] { desired });
                    limited |= Vector3.Distance(desired, bounded) > 0.001f;
                    teleop.Ik.target.position = bounded;
                    if (Vector3.Distance(point, torch.ArcOriginPosition) < (precision == 1 ? 0.003f : 0.008f) &&
                        Vector3.Angle(torch.PointingDir, -hand) < (precision == 1 ? 1f : 8f)) break;
                    yield return null;
                }
                float error = Vector3.Distance(point, torch.ArcOriginPosition) * 1000f;
                float direction = Vector3.Angle(torch.PointingDir, -hand);
                csv.Append(task.WorkOrderId).Append(',').Append(precision).Append(',').Append(sample).Append(',')
                    .Append(error.ToString("0.00", culture)).Append(',').Append(direction.ToString("0.00", culture)).Append(',')
                    .Append(torch.MeasureArcLenMm().ToString("0.00", culture)).Append(',').Append(limited).Append('\n');
                Debug.Log("[FrankaVR Reach] " + task.WorkOrderId + " precision=" + precision + " punta=" + error + " mm; direccion=" + direction + "; limite=" + limited);
                if (precision == 1)
                {
                    task.StartTask();
                    course.Phase = CoursePhaseV5.Running;
                    torch.SetSparkRequested(true);
                    float weldUntil = Time.unscaledTime + 1.2f;
                    while (Time.unscaledTime < weldUntil)
                    {
                        Vector3 desired = teleop.Ik.endEffector.position + point - torch.ArcOriginPosition;
                        teleop.Ik.target.position = (Vector3)clamp.Invoke(teleop, new object[] { desired });
                        yield return null;
                    }
                    float energy = task.Seam.Get(sample).Energy;
                    Debug.Log("[FrankaVR RobotWeld] " + task.WorkOrderId + " arco=" + torch.ArcLenMm +
                        "; estado=" + torch.State + "; energia=" + energy);
                    weldCsv.Append(task.WorkOrderId).Append(',').Append(energy.ToString("0.00", culture)).Append(',')
                        .Append(torch.Arcing).Append(',').Append(torch.ArcLenMm.ToString("0.00", culture)).Append('\n');
                    Capture("07_robot_" + task.WorkOrderId);
                    torch.SetSparkRequested(false);
                    task.FinishTask();
                    course.Phase = CoursePhaseV5.Results;
                }
            }
        }
        Directory.CreateDirectory("Verificacion");
        File.WriteAllText("Verificacion/Alcance_IK.csv", csv.ToString());
        File.WriteAllText("Verificacion/Soldadura_IK.csv", weldCsv.ToString());
        teleop.SetPrecisionSolver(false);
        teleop.enabled = enabled;
    }

    public static void BuildAndroid()
    {
        FrankaVRSetupWizard.ConfigureCardboardAndroid();
        EditorSceneManager.OpenScene("Assets/Main.unity", OpenSceneMode.Single);
        ValidateScene();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Main.unity" }, locationPathName = "Builds/FrankaVR_Android/FrankaVR.apk",
            target = BuildTarget.Android, options = BuildOptions.Development });
        Directory.CreateDirectory("Verificacion");
        File.WriteAllText("Verificacion/Resultado_Build_Android.txt", report.summary.result + "\nErrores: " + report.summary.totalErrors);
        EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
    }

    static void CheckPanels()
    {
        foreach (TextMesh text in UnityEngine.Object.FindObjectsByType<TextMesh>())
        {
            string root = text.transform.root.name;
            if (root != "VRTrainingHUDV6_Runtime" && root != "CourseWorldMenuV6_Runtime") continue;
            Vector3 size = text.GetComponent<Renderer>().localBounds.size * text.transform.localScale.x;
            Require(size.x <= (root.StartsWith("VRTraining") ? 1.18f : 0.90f) + 0.01f, "Texto fuera del panel: " + text.name);
        }
    }

    static void Capture(string name)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        Camera camera = Camera.main;
        var target = new RenderTexture(1600, 900, 24);
        var texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        RenderTexture oldTarget = camera.targetTexture;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            texture.Apply();
            Directory.CreateDirectory("Verificacion/Capturas");
            File.WriteAllBytes("Verificacion/Capturas/" + name + ".png", texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    static void Complete(bool success, string message)
    {
        UnityEditor.SessionState.SetBool(RunningKey, false);
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        Directory.CreateDirectory("Verificacion");
        File.WriteAllText("Verificacion/Resultado_Unity.txt", (success ? "PASS\n" : "FAIL\n") + message);
        Debug.Log("[FrankaVR Checks] " + (success ? "PASS " : "FAIL ") + message);
        // Desde el menu del Editor no se cierra Unity: solo se sale de Play.
        if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
        else { _steps = null; EditorApplication.ExitPlaymode(); }
    }
}
