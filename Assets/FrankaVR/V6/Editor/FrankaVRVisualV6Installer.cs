#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FrankaVR.V5;
using FrankaVR.Welding;

namespace FrankaVR.V6.Editor
{
    public static class FrankaVRVisualV6Installer
    {
        [MenuItem("FrankaVR/7 - Instalar mejoras visuales + movimiento V6")]
        public static void Install()
        {
            GameObject root = GameObject.Find("ExperienceV6") ?? new GameObject("ExperienceV6");

            Camera cam = Camera.main;
            if (cam != null)
            {
                var oldHead = cam.GetComponent<DesktopHeadLookV5>();
                if (oldHead != null) oldHead.enabled = false;
                EnsureComponent<DesktopNavigatorV6>(cam.gameObject);
            }

            // HUD V6
            var hud = EnsureComponent<VRTrainingHUDV6>(root);
            var oldHud = Object.FindObjectOfType<VRTrainingHUDV5>();
            if (oldHud != null) oldHud.enabled = false;
            hud.PlayerCamera = cam;

            // Menu V6
            var menu = EnsureComponent<CourseWorldMenuV6>(root);
            var oldMenu = Object.FindObjectOfType<CourseWorldMenuV5>();
            if (oldMenu != null) oldMenu.enabled = false;
            menu.PlayerCamera = cam;

            // Entorno V6
            var env = EnsureComponent<WorkshopEnvironmentV6>(root);
            var oldEnv = Object.FindObjectOfType<WorkshopEnvironmentV5>();
            if (oldEnv != null) oldEnv.enabled = false;
            env.Workpieces = Object.FindObjectOfType<TrainingWorkpieceFactoryV5>();
            env.Torch = Object.FindObjectOfType<WeldingTorch>();
            env.Build();

            // Visual de arco V6
            WeldingTorch torch = Object.FindObjectOfType<WeldingTorch>();
            if (torch != null)
            {
                var arcRoot = GameObject.Find("WeldVisualV6") ?? new GameObject("WeldVisualV6");
                var arc = EnsureComponent<WeldArcVisualEnhancerV6>(arcRoot);
                arc.Torch = torch;
            }

            // Renderer de cordon V6
            var oldBead = Object.FindObjectOfType<WeldBeadRenderer>();
            if (oldBead != null)
            {
                oldBead.enabled = false;
                var beadV6 = EnsureComponent<WeldBeadRendererV6>(oldBead.gameObject);
                beadV6.Seam = oldBead.Seam;
                var mr = oldBead.GetComponent<MeshRenderer>();
                var mr2 = beadV6.GetComponent<MeshRenderer>();
                if (mr != null && mr2 != null) mr2.sharedMaterial = mr.sharedMaterial;
            }

            EditorUtility.DisplayDialog("FrankaVR V6", "Se instalaron las mejoras visuales y el movimiento de depuracion V6.", "OK");
        }

        [MenuItem("FrankaVR/8 - Validar V6")]
        public static void Validate()
        {
            string report = "";
            report += Check<DesktopNavigatorV6>("DesktopNavigatorV6");
            report += Check<VRTrainingHUDV6>("VRTrainingHUDV6");
            report += Check<CourseWorldMenuV6>("CourseWorldMenuV6");
            report += Check<WorkshopEnvironmentV6>("WorkshopEnvironmentV6");
            report += Check<WeldArcVisualEnhancerV6>("WeldArcVisualEnhancerV6");
            report += Check<WeldBeadRendererV6>("WeldBeadRendererV6");
            EditorUtility.DisplayDialog("Validacion V6", report, "OK");
        }

        static string Check<T>(string label) where T : Object
        {
            return (Object.FindObjectOfType<T>() != null ? "✓ " : "✗ ") + label + "\n";
        }

        static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = go.AddComponent<T>();
            return c;
        }
    }
}
#endif
