using UnityEngine;

namespace FrankaVR.Teleop
{
    /// <summary>
    /// Fuente hibrida de teleoperacion:
    /// - en PC usa teclado por defecto;
    /// - cuando llegan paquetes validos del guante, usa el guante para movimiento/orientacion;
    /// - las acciones discretas (chispa, amperaje, cambio de electrodo, etc.) pueden venir
    ///   tanto del teclado como de la botonera del guante.
    ///
    /// Esto permite desarrollar y depurar en PC sin tener el ESP32 conectado, sin tener que
    /// cambiar manualmente InputSource cada vez.
    /// </summary>
    public class HybridTeleopInput : MonoBehaviour, ITeleopInput
    {
        [Header("Fuentes")]
        public KeyboardTeleopInput Keyboard;
        public BleGloveInput Glove;

        [Header("Conmutacion automatica")]
        [Tooltip("Si hay paquetes recientes del IMU, el guante toma el control de movimiento/orientacion.")]
        public bool PreferGloveWhenFresh = true;

        [Header("Estado (solo lectura)")]
        public string ActiveMotionSource = "Teclado";
        public bool GloveMotionActive;

        void Awake()
        {
            ResolveReferences();
        }

        void Reset()
        {
            ResolveReferences();
        }

        void ResolveReferences()
        {
            if (Keyboard == null) Keyboard = GetComponent<KeyboardTeleopInput>();
            if (Glove == null) Glove = GetComponent<BleGloveInput>();

            if (Keyboard == null) Keyboard = FindObjectOfType<KeyboardTeleopInput>();
            if (Glove == null) Glove = FindObjectOfType<BleGloveInput>();
        }

        public TeleopCommand Poll(Quaternion currentToolOrientation)
        {
            ResolveReferences();

            TeleopCommand keyboardCmd = Keyboard != null
                ? Keyboard.Poll(currentToolOrientation)
                : TeleopCommand.Neutral;

            TeleopCommand gloveCmd = TeleopCommand.Neutral;
            bool gloveFresh = false;

            if (Glove != null)
            {
                // Poll actualiza PacketAgeSec y aplica los timeouts de seguridad internos.
                gloveCmd = Glove.Poll(currentToolOrientation);
                gloveFresh = Glove.HasImuPacket &&
                             Glove.PacketAgeSec <= Mathf.Max(0.05f, Glove.PrimaryTimeoutSec);
            }

            GloveMotionActive = PreferGloveWhenFresh && gloveFresh;
            ActiveMotionSource = GloveMotionActive ? "Guante BLE" : "Teclado";

            // Movimiento/orientacion: una sola fuente a la vez.
            TeleopCommand result = GloveMotionActive ? gloveCmd : keyboardCmd;

            // Acciones discretas: permitimos teclado Y botonera para facilitar pruebas.
            result.SparkHeld = keyboardCmd.SparkHeld || gloveCmd.SparkHeld;
            result.AmperageStep = Mathf.Clamp(keyboardCmd.AmperageStep + gloveCmd.AmperageStep, -1, 1);
            result.CycleElectrode = keyboardCmd.CycleElectrode || gloveCmd.CycleElectrode;
            result.ReplaceElectrode = keyboardCmd.ReplaceElectrode || gloveCmd.ReplaceElectrode;
            result.ToggleHelmet = keyboardCmd.ToggleHelmet || gloveCmd.ToggleHelmet;
            result.ResetOrientation = keyboardCmd.ResetOrientation || gloveCmd.ResetOrientation;

            return result;
        }
    }
}
