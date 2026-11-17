#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using FrankaVR.Teleop;

namespace FrankaVR.V5.EditorTools
{
    /// <summary>
    /// Emula el ESP32/IMU enviando paquetes a BleGloveInput. Sirve para validar toda la
    /// cadena del futuro hardware antes de construirlo.
    /// </summary>
    public class GloveEmulatorWindowV5 : EditorWindow
    {
        float pitch, yaw, roll;
        bool clutch, spark, streaming = true;
        bool ampPlusPulse, ampMinusPulse, cyclePulse, replacePulse, helmetPulse, resetPulse;
        double nextPacket;
        int hz = 50;

        [MenuItem("FrankaVR/Hardware/Emulador de guante V5")]
        static void Open() => GetWindow<GloveEmulatorWindowV5>("Guante V5");

        void OnEnable() { EditorApplication.update += Tick; }
        void OnDisable() { EditorApplication.update -= Tick; }

        void OnGUI()
        {
            EditorGUILayout.LabelField("EMULADOR DE GUANTE", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Play + Streaming simula paquetes IMU reales y hace que HybridTeleopInput cambie a Guante BLE.", MessageType.Info);
            streaming = EditorGUILayout.Toggle("Streaming", streaming);
            hz = EditorGUILayout.IntSlider("Paquetes / s", hz, 10, 100);

            EditorGUILayout.Space();
            pitch = EditorGUILayout.Slider("Pitch", pitch, -45f, 45f);
            yaw   = EditorGUILayout.Slider("Yaw", yaw, -60f, 60f);
            roll  = EditorGUILayout.Slider("Roll", roll, -45f, 45f);
            clutch = EditorGUILayout.Toggle("Clutch", clutch);
            spark = EditorGUILayout.Toggle("Chispa", spark);

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("A +")) ampPlusPulse = true;
            if (GUILayout.Button("A -")) ampMinusPulse = true;
            if (GUILayout.Button("Cambiar electrodo")) cyclePulse = true;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reponer")) replacePulse = true;
            if (GUILayout.Button("Careta")) helmetPulse = true;
            if (GUILayout.Button("Recalibrar")) resetPulse = true;
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Volver mano a neutro")) pitch = yaw = roll = 0f;
        }

        void Tick()
        {
            if (!streaming || !EditorApplication.isPlaying) return;
            double now = EditorApplication.timeSinceStartup;
            if (now < nextPacket) return;
            nextPacket = now + 1.0 / Mathf.Max(1, hz);

            BleGloveInput glove = FindObjectOfType<BleGloveInput>();
            if (glove == null) return;

            Quaternion q = Quaternion.Euler(pitch, yaw, roll);
            glove.OnPrimaryGlovePacket(q, clutch);
            glove.OnSecondaryButtons(spark, ampPlusPulse, ampMinusPulse, cyclePulse, replacePulse, helmetPulse, resetPulse);

            ampPlusPulse = ampMinusPulse = cyclePulse = replacePulse = helmetPulse = resetPulse = false;
            Repaint();
        }
    }
}
#endif
