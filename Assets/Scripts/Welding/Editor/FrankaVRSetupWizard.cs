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
    [MenuItem("FrankaVR/12 - Preparar Cardboard Android")]
    public static void ConfigureCardboardAndroid()
    {
        var settings = AssetDatabase.LoadAssetAtPath<UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget>(
            "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
        if (settings == null) throw new System.InvalidOperationException("No existe el asset XR del proyecto");
        if (!settings.HasSettingsForBuildTarget(BuildTargetGroup.Android))
            settings.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
        if (!settings.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
            settings.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
        var android = settings.SettingsForBuildTarget(BuildTargetGroup.Android);
        var loader = AssetDatabase.LoadAssetAtPath<Google.XR.Cardboard.XRLoader>("Assets/XR/Loaders/XRLoader.asset");
        if (loader == null) throw new System.InvalidOperationException("No se encontro el loader Cardboard existente");
        android.InitManagerOnStart = true;
        android.Manager.automaticLoading = true;
        android.Manager.automaticRunning = true;
        android.Manager.TrySetLoaders(new System.Collections.Generic.List<UnityEngine.XR.Management.XRLoader> { loader });
        EditorUtility.SetDirty(android.Manager);
        EditorUtility.SetDirty(android);
        EditorUtility.SetDirty(settings);
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36;
        // Los meshes URDF ya estan importados. Sus librerias nativas de escritorio
        // no deben empaquetarse como plugins Android (x86/x64 comparten nombre).
        foreach (PluginImporter plugin in PluginImporter.GetAllImporters())
        {
            string path = plugin.assetPath.Replace('\\', '/');
            if (!path.Contains("com.unity.robotics.urdf-importer") || !path.Contains("/AssimpNet/Native/")) continue;
            plugin.SetExcludeFromAnyPlatform(BuildTarget.Android, true);
            plugin.SetCompatibleWithPlatform(BuildTarget.Android, false);
            AssetDatabase.WriteImportSettingsIfDirty(plugin.assetPath);
            Debug.Log("[Android plugins] Excluido: " + plugin.assetPath + "; compatible=" + plugin.GetCompatibleWithPlatform(BuildTarget.Android));
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[FrankaVR] Cardboard Android preparado. Standalone conserva su configuracion XR.");
    }

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

        ConfigureHybridInput(viewpoints, cam);

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

        MarkDirty(vrGo, polishGo, torch != null ? torch.gameObject : null);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = vrGo;

        EditorUtility.DisplayDialog(
            "FrankaVR",
            "Configuracion base lista.\n\n" +
            "Entrada HIBRIDA activada:\n" +
            "- sin guante conectado: teclado\n" +
            "- con IMU BLE activo: guante\n" +
            "- chispa/comandos: teclado o botonera\n\n" +
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

        KeyboardTeleopInput keyboard = Object.FindObjectOfType<KeyboardTeleopInput>();
        BleGloveInput glove = Object.FindObjectOfType<BleGloveInput>();
        HybridTeleopInput hybrid = Object.FindObjectOfType<HybridTeleopInput>();
        Check(keyboard != null, "KeyboardTeleopInput", ref problems, sb);
        Check(glove != null, "BleGloveInput", ref problems, sb);
        Check(hybrid != null, "HybridTeleopInput", ref problems, sb);
        Check(Object.FindObjectOfType<GlovePacketBridge>() != null, "GlovePacketBridge", ref problems, sb);

        if (hybrid != null)
        {
            Check(hybrid.Keyboard != null, "Hybrid -> Keyboard", ref problems, sb);
            Check(hybrid.Glove != null, "Hybrid -> Glove", ref problems, sb);
        }

        WeldingTeleopController teleop = Object.FindObjectOfType<WeldingTeleopController>();
        Check(teleop != null, "WeldingTeleopController", ref problems, sb);
        if (teleop != null)
        {
            Check(teleop.Ik != null, "Teleop -> PandaIK", ref problems, sb);
            Check(teleop.Torch != null, "Teleop -> WeldingTorch", ref problems, sb);
            Check(teleop.InputSource != null, "Teleop -> InputSource", ref problems, sb);
            Check(teleop.InputSource is HybridTeleopInput, "Teleop -> HybridTeleopInput", ref problems, sb);
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

    [MenuItem("FrankaVR/4 - Reparar control Teclado + Guante")]
    public static void RepairHybridInput()
    {
        Camera cam = Camera.main != null ? Camera.main : Object.FindObjectOfType<Camera>();
        ViewpointManager viewpoints = Object.FindObjectOfType<ViewpointManager>();
        if (viewpoints != null) viewpoints.EnsureRigRoot();

        ConfigureHybridInput(viewpoints, cam);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        EditorUtility.DisplayDialog(
            "FrankaVR",
            "Control hibrido reparado.\n\n" +
            "Ahora el teclado funciona sin BLE y el guante toma el movimiento automaticamente cuando envia IMU.",
            "OK");
    }

    static void ConfigureHybridInput(ViewpointManager viewpoints, Camera cam)
    {
        KeyboardTeleopInput keyboard = Object.FindObjectOfType<KeyboardTeleopInput>();
        BleGloveInput glove = Object.FindObjectOfType<BleGloveInput>();
        HybridTeleopInput hybrid = Object.FindObjectOfType<HybridTeleopInput>();

        GameObject inputGo = null;
        if (hybrid != null) inputGo = hybrid.gameObject;
        else if (glove != null) inputGo = glove.gameObject;
        else if (keyboard != null) inputGo = keyboard.gameObject;
        else inputGo = GetOrCreate("GloveInput");

        if (keyboard == null) keyboard = GetOrAdd<KeyboardTeleopInput>(inputGo);
        if (glove == null) glove = GetOrAdd<BleGloveInput>(inputGo);
        if (hybrid == null) hybrid = GetOrAdd<HybridTeleopInput>(inputGo);

        hybrid.Keyboard = keyboard;
        hybrid.Glove = glove;
        hybrid.PreferGloveWhenFresh = true;

        GlovePacketBridge bridge = Object.FindObjectOfType<GlovePacketBridge>();
        if (bridge == null) bridge = GetOrAdd<GlovePacketBridge>(inputGo);
        bridge.Glove = glove;
        bridge.Viewpoints = viewpoints;

        WeldingTeleopController teleop = Object.FindObjectOfType<WeldingTeleopController>();
        if (teleop != null)
        {
            teleop.InputSource = hybrid;
            teleop.ReferenceCamera = cam;
            if (viewpoints != null && viewpoints.rigRoot != null)
                teleop.MovementReference = viewpoints.rigRoot;
            teleop.ReferenceRelative = true;
            teleop.AutoUseCameraParentAsMovementReference = true;
            EditorUtility.SetDirty(teleop);
        }

        MarkDirty(inputGo);
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

    // El eje de herramienta es +Y de Soldador (sale del flange). El porta-electrodo estaba
    // armado en -Y, hacia adentro de la muneca: el arco quedaba junto a la boca del cable y,
    // al consumirse la varilla, el mango chocaba contra la chapa y el brazo se trababa.
    // Deja las mordazas en la cara del flange y el electrodo de 350 mm hacia la pieza.
    [MenuItem("FrankaVR/13 - Orientar porta-electrodo hacia la pieza")]
    public static void OrientElectrodeHolder()
    {
        WeldingTorch torch = Object.FindObjectOfType<WeldingTorch>();
        if (torch == null || torch.Tip == null) throw new System.InvalidOperationException("No hay WeldingTorch con Tip en la escena");
        // El cordon no tenia material asignado y se veia magenta.
        Material bead = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scripts/Welding/MatCordon.mat");
        foreach (WeldBeadRenderer beadRenderer in Object.FindObjectsOfType<WeldBeadRenderer>(true))
        {
            MeshRenderer mesh = beadRenderer.GetComponent<MeshRenderer>();
            if (mesh == null || mesh.sharedMaterial != null || bead == null) continue;
            Undo.RecordObject(mesh, "Material del cordon");
            mesh.sharedMaterial = bead;
            EditorSceneManager.MarkSceneDirty(mesh.gameObject.scene);
        }
        Transform holder = torch.transform;
        // Mordazas en -Y = armado viejo, hacia adentro de la muneca. En +Y ya esta corregido.
        Transform jaw = holder.Find("MordazaFija");
        if (jaw == null || jaw.localPosition.y > 0f)
        {
            EditorSceneManager.SaveScene(torch.gameObject.scene);
            Debug.Log("[FrankaVR] Porta-electrodo ya orientado; material del cordon verificado.");
            return;
        }
        string[] parts = { "Mango", "Nervadura0", "Nervadura1", "Nervadura2", "Nervadura3", "BocaCable", "TomaCable",
            "Cuello", "MordazaFija", "MordazaMovil", "Palanca", "Resorte", "Electrodo" };
        foreach (string name in parts)
        {
            Transform part = holder.Find(name);
            if (part == null) continue;
            Undo.RecordObject(part, "Orientar porta-electrodo");
            Vector3 p = part.localPosition;
            part.localPosition = new Vector3(p.x, -p.y - 0.154f, p.z); // mordazas (-0.166) -> +0.012
        }
        Undo.RecordObject(torch.Tip, "Orientar porta-electrodo");
        torch.Tip.localPosition = new Vector3(0f, 0.002f, 0f); // la varilla nace en las mordazas
        Undo.RecordObject(torch, "Orientar porta-electrodo");
        torch.ElectrodeAxisLocal = Vector3.up;
        EditorSceneManager.MarkSceneDirty(torch.gameObject.scene);
        EditorSceneManager.SaveScene(torch.gameObject.scene);
        Debug.Log("[FrankaVR] Porta-electrodo orientado hacia la pieza y escena guardada.");
    }

    // Torcha MIG robotizada recta sobre el eje +Y de Soldador (sale del flange):
    // soporte, cuerpo, cuello, tobera y tubo de contacto. El alambre libre (stick-out)
    // lo dibuja WeldingTorch desde Tip. El porta-electrodo SMAW queda desactivado.
    [MenuItem("FrankaVR/14 - Montar torcha MIG")]
    public static void MountMigTorch()
    {
        WeldingTorch torch = Object.FindObjectOfType<WeldingTorch>();
        if (torch == null || torch.Tip == null) throw new System.InvalidOperationException("No hay WeldingTorch con Tip en la escena");
        Transform holder = torch.transform;
        string[] smaw = { "Mango", "Nervadura0", "Nervadura1", "Nervadura2", "Nervadura3", "BocaCable",
            "Cuello", "MordazaFija", "MordazaMovil", "Palanca", "Resorte", "Electrodo" };
        foreach (string name in smaw)
        {
            Transform part = holder.Find(name);
            if (part == null || !part.gameObject.activeSelf) continue;
            Undo.RecordObject(part.gameObject, "Torcha MIG");
            part.gameObject.SetActive(false);
        }
        Shader standard = Shader.Find("Standard");
        Material Mat(string name, Color color, float metallic, float gloss)
        {
            string path = "Assets/Scripts/Welding/Mat" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(standard) { name = "Mat" + name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.color = color; m.SetFloat("_Metallic", metallic); m.SetFloat("_Glossiness", gloss);
            EditorUtility.SetDirty(m);
            return m;
        }
        Material black = Mat("TorchBody", new Color(0.07f, 0.075f, 0.08f), 0.2f, 0.45f);
        Material chrome = Mat("TorchNeck", new Color(0.62f, 0.64f, 0.66f), 0.9f, 0.7f);
        Material copper = Mat("TorchNozzle", new Color(0.78f, 0.47f, 0.24f), 0.85f, 0.65f);
        Material aluminum = Mat("TorchFlange", new Color(0.70f, 0.72f, 0.75f), 0.8f, 0.55f);
        Material sensor = Mat("TorchSensor", new Color(0.13f, 0.20f, 0.30f), 0.4f, 0.5f);
        // (nombre, centro Y, diametro, largo, material, collider)
        // La brida del Panda queda en Y = -0.154 (panda_link8 esta corrido a donde terminaba la pinza):
        // adaptador + sensor anticolision + brazo de la torcha cubren ese tramo, sin cambiar el TCP.
        var parts = new (string, float, float, float, Material, bool)[] {
            ("TorchFlange", -0.148f, 0.066f, 0.012f, aluminum, true),
            ("TorchSensor", -0.117f, 0.056f, 0.050f, sensor,   true),
            ("TorchArm",    -0.046f, 0.040f, 0.092f, black,    true),
            ("TorchMount",   0.020f, 0.060f, 0.040f, black,  true),
            ("TorchBody",    0.080f, 0.036f, 0.080f, black,  true),
            ("TorchNeck",    0.1315f, 0.014f, 0.026f, chrome, true),
            ("TorchNozzle",  0.1545f, 0.016f, 0.020f, copper, true), // boquilla fina: entra en la esquina de la cartela
            ("TorchContact", 0.1625f, 0.008f, 0.005f, copper, false) };
        foreach (var (name, centerY, diameter, length, material, collider) in parts)
        {
            Transform part = holder.Find(name);
            if (part == null)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Undo.RegisterCreatedObjectUndo(go, "Torcha MIG");
                go.name = name;
                part = go.transform;
                part.SetParent(holder, false);
            }
            part.localPosition = new Vector3(0f, centerY, 0f);
            part.localRotation = Quaternion.identity;
            part.localScale = new Vector3(diameter, length * 0.5f, diameter);
            part.GetComponent<Renderer>().sharedMaterial = material;
            Collider c = part.GetComponent<Collider>();
            if (c != null && !collider) Object.DestroyImmediate(c);
        }
        Transform port = holder.Find("TomaCable");
        if (port != null) { Undo.RecordObject(port, "Torcha MIG"); port.localPosition = new Vector3(0.022f, 0.03f, 0f); }
        Undo.RecordObject(torch.Tip, "Torcha MIG");
        torch.Tip.localPosition = new Vector3(0f, 0.165f, 0f); // extremo del tubo de contacto
        Undo.RecordObject(torch, "Torcha MIG");
        torch.ElectrodeAxisLocal = Vector3.up;
        torch.StubLengthMm = 0f;
        torch.HotTipScale = 0.003f;
        torch.ElectrodeColdColor = new Color(0.72f, 0.45f, 0.22f, 1f);
        WeldingMachine machine = Object.FindObjectOfType<WeldingMachine>();
        if (machine != null)
        {
            Undo.RecordObject(machine, "Torcha MIG");
            machine.AmpMax = 300f; machine.AmpStep = 10f; machine.Amperage = 160f;
        }
        // Luz del arco MIG: con 6 de intensidad y 2.5 m de alcance el brazo blanco quedaba quemado.
        // El brillo del arco lo da WeldArcVisualEnhancerV6; esta luz solo ilumina la zona de trabajo.
        WeldingFX fx = Object.FindObjectOfType<WeldingFX>();
        if (fx != null)
        {
            Undo.RecordObject(fx, "Torcha MIG");
            fx.BaseIntensity = 2.5f; fx.LightRange = 1.2f;
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(torch.gameObject.scene);
        EditorSceneManager.SaveScene(torch.gameObject.scene);
        Debug.Log("[FrankaVR] Torcha MIG montada y escena guardada.");
    }
}
#endif
