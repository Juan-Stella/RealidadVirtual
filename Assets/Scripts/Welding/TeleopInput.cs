using UnityEngine;

namespace FrankaVR.Teleop
{
    /// Comando de teleoperacion, agnostico de la fuente (teclado, gamepad o guante BLE).
    /// Toda la cadena de control consume esta estructura, asi cambiar de teclado al
    /// guante ESP32+BNO055 mas adelante es solo cambiar la implementacion de ITeleopInput.
    public struct TeleopCommand
    {
        public Quaternion Orientation;     // orientacion absoluta deseada de la herramienta
        public Vector3    TranslationRate; // velocidad de la punta en marco base, por eje (~ -1..1)
        public bool       SparkHeld;       // gatillo de chispa (mano secundaria)
        public int        AmperageStep;    // ajuste de amperaje este frame: -1, 0, +1
        public bool       CycleElectrode;  // cambiar de electrodo en la maquina (flanco)
        public bool       ReplaceElectrode;// reponer varilla consumida (flanco)
        public bool       ToggleHelmet;    // subir/bajar la careta (flanco)
        public bool       ResetOrientation;// devolver el electrodo a la vertical (flanco)
        public bool       MoveClutch;      // si false, no se aplica traslacion (evita deriva)

        public static TeleopCommand Neutral => new TeleopCommand
        {
            Orientation = Quaternion.identity,
            TranslationRate = Vector3.zero,
            SparkHeld = false,
            AmperageStep = 0,
            CycleElectrode = false,
            ReplaceElectrode = false,
            ToggleHelmet = false,
            ResetOrientation = false,
            MoveClutch = false
        };
    }

    public interface ITeleopInput
    {
        // currentToolOrientation: orientacion actual de la herramienta (para inicializar modos relativos).
        TeleopCommand Poll(Quaternion currentToolOrientation);
    }
}
