#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FrankaVR.V6;

namespace FrankaVR.V6_2.Editor
{
    public static class FrankaVRUICleanupV62Installer
    {
        [MenuItem("FrankaVR/9 - Limpiar textos y UI V6.2")]
        public static void InstallAndClean()
        {
            GameObject root = GameObject.Find("ExperienceV6_2");
            if (root == null) root = new GameObject("ExperienceV6_2");

            SceneUICleanupV62 cleanup = root.GetComponent<SceneUICleanupV62>();
            if (cleanup == null) cleanup = root.AddComponent<SceneUICleanupV62>();
            cleanup.CleanupOnAwake = true;
            cleanup.KeepWatchingForLegacyRuntimeObjects = true;

            int disabled = SceneUICleanupV62.CleanupNow(true);

            CourseWorldMenuV6 menu = Object.FindObjectOfType<CourseWorldMenuV6>();
            if (menu != null)
            {
                menu.enabled = true;
                menu.HideWhileRunning = true;
                EditorUtility.SetDirty(menu);
            }

            VRTrainingHUDV6 hud = Object.FindObjectOfType<VRTrainingHUDV6>();
            if (hud != null)
            {
                hud.enabled = true;
                hud.ShowHelpDuringRunning = false;
                EditorUtility.SetDirty(hud);
            }

            EditorUtility.SetDirty(root);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);

            EditorUtility.DisplayDialog(
                "FrankaVR UI V6.2",
                "Limpieza aplicada.\n\nElementos legacy desactivados: " + disabled +
                "\n\nQuedan como UI principal:\n- CourseWorldMenuV6\n- VRTrainingHUDV6\n\nEl panel grande se ocultara automaticamente durante la soldadura.",
                "OK");
        }

        [MenuItem("FrankaVR/10 - Validar UI V6.2")]
        public static void Validate()
        {
            string report = "";
            report += Object.FindObjectOfType<SceneUICleanupV62>() != null ? "✓ SceneUICleanupV62\n" : "✗ SceneUICleanupV62\n";
            report += Object.FindObjectOfType<CourseWorldMenuV6>() != null ? "✓ CourseWorldMenuV6\n" : "✗ CourseWorldMenuV6\n";
            report += Object.FindObjectOfType<VRTrainingHUDV6>() != null ? "✓ VRTrainingHUDV6\n" : "✗ VRTrainingHUDV6\n";

            EditorUtility.DisplayDialog("Validacion UI V6.2", report, "OK");
        }
    }
}
#endif
