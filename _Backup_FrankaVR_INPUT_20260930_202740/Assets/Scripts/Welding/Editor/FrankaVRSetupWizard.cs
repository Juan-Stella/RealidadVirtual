#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FrankaVR.Teleop;
using FrankaVR.Welding;

/// <summary>
/// Configuracion asistida de la escena para evitar enlaces manuales repetitivos.
/// No instala el plugin Google Cardboard; solo prepara los objetos y referencias del proyecto.
/// </summary>
public static class FrankaVRSetupWizard
{
    [MenuItem("FrankaVR/1 - Configurar Cardboard + Guantes")]
    public static void SetupCore()
    {
        Camera cam = Camera.main != null ? Camera.main : Object.FindObjectOfType<Camera>();
        if (cam == null)
        {
            EditorUtility.DisplayDialog("FrankaVR", "No encontre ninguna Camera en la escena.", "OK");
            return;
        }

        GameObject vrGo = GetOrCreate("VRSystem");
        ViewpointManager viewpoints = Object.FindObjectOfType<ViewpointManager>();
        if (viewpoints == null) viewpoints = GetOrAdd<ViewpointManager>(vrGo);
        viewpoints.playerCamera = cam;
        viewpoints.EnsureRigRoot();

        CourseViewAutoSetup views = Object.FindObjectOfType<CourseViewAutoSetup>();
        if (views == null) views = GetOrAdd<CourseViewAutoSetup>(vrGo);
        views.Viewpoints = viewpoints;
        views.PlayerCamera = cam;
        views.BuildOnlyIfEmpty = true;

        MobileVRQuality quality = Object.FindObjectOfType<MobileVRQuality>();
        if (quality == null) quality = GetOrAdd<MobileVRQuality>(vrGo);
        quality.TargetFps = 60;

        CardboardStatusHUD status = Object.FindObjectOfType<CardboardStatusHUD>();
        if (status == null) status = GetOrAdd<CardboardStatusHUD>(vrGo);
        status.PlayerCamera = cam;
        status.Viewpoints = viewpoints;
        status.Torch = Object.FindObjectOfType<WeldingTorch>();
        status.Machine = Object.FindObjectOfType<WeldingMachine>();
        status.Task = Object.FindObjectOfType<WeldingTaskManager>();

        BleGloveInput glove = Object.FindObjectOfType<BleGloveInput>();
        GameObject gloveGo = glove != null ? glove.gameObject : GetOrCreate("GloveInput");
        if (glove == null) glove = GetOrAdd<BleGloveInput>(gloveGo);
        GlovePacketBridge bridge = Object.FindObjectOfType<GlovePacketBridge>();
        if (bridge == null) bridge = GetOrAdd<GlovePacketBridge>(gloveGo);
        bridge.Glove = glove;
        bridge.Viewpoints = viewpoints;

        WeldingTeleopController teleop = Object.FindObjectOfType<WeldingTeleopController>();
        if (teleop != null)
        {
            teleop.InputSource = glove;
            teleop.ReferenceCamera = cam;
            teleop.MovementReference = viewpoints.rigRoot;
            teleop.ReferenceRelative = true;
            teleop.AutoUseCameraParentAsMovementReference = true;
        }

        WeldingTorch torch = Object.FindObjectOfType<WeldingTorch>();
        if (torch != null)
        {
            torch.UseArcTipOverride = false;
            torch.UseProceduralElectrodeVisual = true;
            torch.AutoFindElectrodeVisual = true;
            torch.HideOriginalElectrodeVisual = true;
            torch.DisableOriginalElectrodeColliders = true;
        }

        WeldingEnvironmentPolish polish = Object.FindObjectOfType<WeldingEnvironmentPolish>();
        GameObject polishGo = polish != null ? polish.gameObject : GetOrCreate("VisualPolish");
        if (polish == null) polish = GetOrAdd<WeldingEnvironmentPolish>(polishGo);
        polish.ReferenceCamera = cam;
        polish.Torch = torch;
        polish.Seam = Object.FindObjectOfType<WeldSeam>();
        GameObject chapa = GameObject.Find("Chapa");
        if (chapa != null) polish.Workpiece = chapa.transform;

        MarkDirty(vrGo, gloveGo, polishGo, teleop != null ? teleop.gameObject : null,
                  torch != null ? torch.gameObject : null);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = vrGo;
        EditorUtility.DisplayDialog(
            "FrankaVR",
            "Configuracion base lista.\n\n" +
            "Se crearon/enlazaron VRSystem, GloveInput, HUD, rig de vistas y VisualPolish.\n" +
            "El plugin Google Cardboard XR se instala por separado desde Package Manager.",
            "OK");
    }

    [MenuItem("FrankaVR/2 - Crear ejercicio de caño N5")]
    public static void SetupPipeExercise()
    {
        WeldSeam seam = Object.FindObjectOfType<WeldSeam>();
        WeldingTaskManager task = Object.FindObjectOfType<WeldingTaskManager>();
        if (seam == null || task == null)
        {
            EditorUtility.DisplayDialog("FrankaVR", "Necesito encontrar WeldSeam y WeldingTaskManager en la escena.", "OK");
            return;
        }

        GameObject pipeGo = GameObject.Find("PipeExercise");
        if (pipeGo == null)
        {
            pipeGo = new GameObject("PipeExercise");
            Undo.RegisterCreatedObjectUndo(pipeGo, "Crear PipeExercise");
        }

        GameObject chapa = GameObject.Find("Chapa");
        if (chapa != null)
        {
            Renderer r = chapa.GetComponentInChildren<Renderer>();
            pipeGo.transform.position = r != null ? r.bounds.center : chapa.transform.position;
            pipeGo.layer = chapa.layer;
        }

        PipeTrainingWorkpiece pipe = GetOrAdd<PipeTrainingWorkpiece>(pipeGo);
        pipe.Seam = seam;
        pipe.Task = task;
        pipe.ActivateN5OnStart = true;
        pipe.BuildOnStart = true;
        pipe.CopyLayerFromChapa = true;

        // Actualizar el foco de las vistas para que miren al nuevo ejercicio.
        CourseViewAutoSetup views = Object.FindObjectOfType<CourseViewAutoSetup>();
        if (views != null) views.Focus = pipeGo.transform;

        WeldingEnvironmentPolish polish = Object.FindObjectOfType<WeldingEnvironmentPolish>();
        if (polish != null) polish.Workpiece = pipeGo.transform;

        MarkDirty(pipeGo, views != null ? views.gameObject : null, polish != null ? polish.gameObject : null);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = pipeGo;

        EditorUtility.DisplayDialog(
            "FrankaVR",
            "Ejercicio de caño N5 preparado.\n\n" +
            "La geometria se genera al entrar en Play para no ensuciar la escena con objetos temporales.",
            "OK");
    }

    [MenuItem("FrankaVR/3 - Validar escena")]
    public static void ValidateScene()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        int problems = 0;

        Camera cam = Camera.main;
        Check(cam != null, "Main Camera con tag MainCamera", ref problems, sb);

        ViewpointManager viewpoints = Object.FindObjectOfType<ViewpointManager>();
        Check(viewpoints != null, "ViewpointManager", ref problems, sb);
        if (viewpoints != null)
        {
            viewpoints.EnsureRigRoot();
            Check(viewpoints.rigRoot != null, "RigRoot de Cardboard", ref problems, sb);
        }

        BleGloveInput glove = Object.FindObjectOfType<BleGloveInput>();
        Check(glove != null, "BleGloveInput", ref problems, sb);
        Check(Object.FindObjectOfType<GlovePacketBridge>() != null, "GlovePacketBridge", ref problems, sb);

        WeldingTeleopController teleop = Object.FindObjectOfType<WeldingTeleopController>();
        Check(teleop != null, "WeldingTeleopController", ref problems, sb);
        if (teleop != null)
        {
            Check(teleop.Ik != null, "Teleop -> PandaIK", ref problems, sb);
            Check(teleop.Torch != null, "Teleop -> WeldingTorch", ref problems, sb);
            Check(teleop.InputSource != null, "Teleop -> InputSource", ref problems, sb);
            Check(teleop.MovementReference != null, "Teleop -> MovementReference", ref problems, sb);
        }

        WeldingTorch torch = Object.FindObjectOfType<WeldingTorch>();
        Check(torch != null, "WeldingTorch", ref problems, sb);
        if (torch != null)
        {
            Check(torch.Tip != null, "Torch -> Tip", ref problems, sb);
            Check(torch.Machine != null, "Torch -> WeldingMachine", ref problems, sb);
            Check(torch.IgnoreRoot != null, "Torch -> IgnoreRoot del Panda", ref problems, sb);
        }

        Check(Object.FindObjectOfType<WeldSeam>() != null, "WeldSeam", ref problems, sb);
        Check(Object.FindObjectOfType<WeldingTaskManager>() != null, "WeldingTaskManager", ref problems, sb);

        string title = problems == 0 ? "FrankaVR - Configuracion OK" : "FrankaVR - Revisar configuracion";
        EditorUtility.DisplayDialog(title, sb.ToString(), "OK");
    }

    static void Check(bool ok, string label, ref int problems, System.Text.StringBuilder sb)
    {
        sb.Append(ok ? "✓ " : "✗ ").Append(label).AppendLine();
        if (!ok) problems++;
    }

    static GameObject GetOrCreate(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go != null) return go;
        go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Crear " + name);
        return go;
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        if (c != null) return c;
        return Undo.AddComponent<T>(go);
    }

    static void MarkDirty(params GameObject[] gos)
    {
        foreach (GameObject go in gos)
            if (go != null) EditorUtility.SetDirty(go);
    }
}
#endif
