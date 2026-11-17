#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FrankaVR.Welding;
using FrankaVR.Teleop;

namespace FrankaVR.V5.EditorTools
{
    /// <summary>Instala SOLO componentes V5; no reemplaza scripts existentes.</summary>
    public static class FrankaVRCourseV5Wizard
    {
        [MenuItem("FrankaVR/5 - Instalar experiencia de curso V5")]
        public static void Install()
        {
            WeldingTaskManager task = Object.FindObjectOfType<WeldingTaskManager>();
            WeldSeam seam = Object.FindObjectOfType<WeldSeam>();
            WeldingTorch torch = Object.FindObjectOfType<WeldingTorch>();
            WeldingMachine machine = Object.FindObjectOfType<WeldingMachine>();
            Camera cam = Camera.main != null ? Camera.main : Object.FindObjectOfType<Camera>();

            if (task == null || seam == null || torch == null || machine == null || cam == null)
            {
                EditorUtility.DisplayDialog("FrankaVR V5",
                    "Falta alguno de estos componentes base: WeldingTaskManager, WeldSeam, WeldingTorch, WeldingMachine o Main Camera.\n\nNo se modifico la escena.", "OK");
                return;
            }

            GameObject courseGo = GetOrCreate("CourseSystemV5");
            TrainingWorkpieceFactoryV5 factory = GetOrAdd<TrainingWorkpieceFactoryV5>(courseGo);
            factory.Seam = seam;
            GameObject chapa = GameObject.Find("Chapa");
            if (chapa != null) factory.LegacyChapa = chapa.transform;

            CourseTrainingManagerV5 course = GetOrAdd<CourseTrainingManagerV5>(courseGo);
            course.Task = task;
            course.Workpieces = factory;

            CourseWorldMenuV5 menu = GetOrAdd<CourseWorldMenuV5>(courseGo);
            menu.Course = course;
            menu.PlayerCamera = cam;

            GameObject visualGo = GetOrCreate("WorkshopVisualsV5");
            WorkshopEnvironmentV5 env = GetOrAdd<WorkshopEnvironmentV5>(visualGo);
            env.Workpieces = factory;
            env.Torch = torch;

            WorkshopLightingV5 lights = GetOrAdd<WorkshopLightingV5>(visualGo);
            lights.Workpieces = factory;

            WeldArcVisualEnhancerV5 arc = GetOrAdd<WeldArcVisualEnhancerV5>(visualGo);
            arc.Torch = torch;
            arc.AdditionalLight = false; // WeldingFX ya posee luz: evitar dos pixel lights.

            GameObject vrGo = GetOrCreate("VRExperienceV5");
            VRTrainingHUDV5 hud = GetOrAdd<VRTrainingHUDV5>(vrGo);
            hud.PlayerCamera = cam;
            hud.Course = course;
            hud.Task = task;
            hud.Torch = torch;
            hud.Machine = machine;
            hud.HybridInput = Object.FindObjectOfType<HybridTeleopInput>();
            hud.Viewpoints = Object.FindObjectOfType<ViewpointManager>();

            DesktopHeadLookV5 head = GetOrAdd<DesktopHeadLookV5>(vrGo);
            head.PlayerCamera = cam;

            SimulatorModeManagerV5 mode = GetOrAdd<SimulatorModeManagerV5>(vrGo);
            mode.DesktopHeadLook = head;
            mode.Viewpoints = Object.FindObjectOfType<ViewpointManager>();

            GazeInteractor gaze = Object.FindObjectOfType<GazeInteractor>();
            if (gaze == null) gaze = GetOrAdd<GazeInteractor>(vrGo);
            gaze.playerCamera = cam;

            // Evitar dos HUD superpuestos si existe el anterior.
            CardboardStatusHUD oldHud = Object.FindObjectOfType<CardboardStatusHUD>();
            if (oldHud != null) oldHud.enabled = false;

            // El viejo generador de caño no debe crear otra pieza simultaneamente.
            PipeTrainingWorkpiece oldPipe = Object.FindObjectOfType<PipeTrainingWorkpiece>();
            if (oldPipe != null) oldPipe.enabled = false;

            EditorUtility.SetDirty(courseGo);
            EditorUtility.SetDirty(visualGo);
            EditorUtility.SetDirty(vrGo);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = courseGo;

            EditorUtility.DisplayDialog("FrankaVR V5",
                "Experiencia de curso instalada SIN reemplazar el codigo base.\n\n" +
                "Play: 1-5 selecciona ejercicio | T entrenamiento | Y examen | F finalizar | M menu.\n" +
                "PC: boton derecho + mouse emula la cabeza.\n" +
                "Hardware > Emulador de guante V5 prueba la ruta BLE sin ESP32.", "OK");
        }

        [MenuItem("FrankaVR/6 - Validar experiencia V5")]
        public static void ValidateV5()
        {
            System.Text.StringBuilder s = new System.Text.StringBuilder();
            int bad = 0;
            Check(Object.FindObjectOfType<CourseTrainingManagerV5>() != null, "CourseTrainingManagerV5", s, ref bad);
            Check(Object.FindObjectOfType<TrainingWorkpieceFactoryV5>() != null, "TrainingWorkpieceFactoryV5", s, ref bad);
            Check(Object.FindObjectOfType<WorkshopEnvironmentV5>() != null, "WorkshopEnvironmentV5", s, ref bad);
            Check(Object.FindObjectOfType<WorkshopLightingV5>() != null, "WorkshopLightingV5", s, ref bad);
            Check(Object.FindObjectOfType<WeldArcVisualEnhancerV5>() != null, "WeldArcVisualEnhancerV5", s, ref bad);
            Check(Object.FindObjectOfType<VRTrainingHUDV5>() != null, "VRTrainingHUDV5", s, ref bad);
            Check(Object.FindObjectOfType<DesktopHeadLookV5>() != null, "DesktopHeadLookV5", s, ref bad);
            Check(Object.FindObjectOfType<HybridTeleopInput>() != null, "HybridTeleopInput (teclado/guante)", s, ref bad);
            Check(Object.FindObjectOfType<ViewpointManager>() != null, "ViewpointManager Cardboard", s, ref bad);
            Check(Object.FindObjectOfType<WeldingTaskManager>() != null, "WeldingTaskManager", s, ref bad);
            Check(Object.FindObjectOfType<WeldingTorch>() != null, "WeldingTorch", s, ref bad);
            EditorUtility.DisplayDialog(bad == 0 ? "V5 OK" : "V5 - Revisar", s.ToString(), "OK");
        }

        static void Check(bool ok, string label, System.Text.StringBuilder s, ref int bad)
        {
            s.Append(ok ? "✓ " : "✗ ").Append(label).AppendLine();
            if (!ok) bad++;
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
            return c != null ? c : Undo.AddComponent<T>(go);
        }
    }
}
#endif
