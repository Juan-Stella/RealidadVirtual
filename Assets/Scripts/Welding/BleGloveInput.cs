using UnityEngine;

namespace FrankaVR.Teleop
{
    /// STUB del guante (ESP32 + BNO055 por BLE). La logica de control ya esta lista:
    /// solo falta el puente BLE Android->Unity que llame a OnGlovePacket() cuando llega
    /// un paquete. Asi no hay que rediseniar el control cuando llegue el hardware.
    ///
    /// Paquete sugerido (little-endian):
    ///   float qw, qx, qy, qz  -> orientacion absoluta del BNO055
    ///   byte  buttons         -> bit0=spark, bit1=clutch, bit2=amp+, bit3=amp-, bit4=cycle
    ///
    /// Modelo de posicion: con un solo IMU no hay posicion absoluta confiable, asi que
    /// la posicion se comanda por velocidad (rate): con el clutch presionado, la
    /// inclinacion de la mano respecto a la referencia tomada al presionar el clutch
    /// define la direccion y magnitud de la velocidad.
    public class BleGloveInput : MonoBehaviour, ITeleopInput
    {
        [Header("Calibracion")]
        public Quaternion MountOffset = Quaternion.identity; // alinea ejes del sensor con el mundo
        public float TiltRateGain = 2f;
        public float TiltDeadzoneDeg = 5f;

        TeleopCommand _latest = TeleopCommand.Neutral;
        Quaternion _rawImu = Quaternion.identity;
        bool _spark, _clutch, _cycle, _replace, _helmet, _prevClutch;
        int  _ampStep;
        Quaternion _clutchRef = Quaternion.identity;

        public TeleopCommand Poll(Quaternion currentToolOrientation)
        {
            Quaternion imu = MountOffset * _rawImu;

            Vector3 rate = Vector3.zero;
            if (_clutch)
            {
                if (!_prevClutch) _clutchRef = imu; // referencia al presionar clutch
                Quaternion rel = Quaternion.Inverse(_clutchRef) * imu;
                rel.ToAngleAxis(out float ang, out Vector3 axis);
                if (ang > 180f) ang -= 360f;
                if (Mathf.Abs(ang) > TiltDeadzoneDeg)
                {
                    Vector3 tilt = axis.normalized * (ang * Mathf.Deg2Rad);
                    rate = new Vector3(tilt.z, tilt.y, tilt.x) * TiltRateGain; // mapeo a calibrar
                    rate = Vector3.ClampMagnitude(rate, 1f);
                }
            }
            _prevClutch = _clutch;

            _latest = new TeleopCommand
            {
                Orientation     = imu,
                TranslationRate = rate,
                SparkHeld       = _spark,
                AmperageStep    = _ampStep,
                CycleElectrode   = _cycle,
                ReplaceElectrode = _replace,
                ToggleHelmet     = _helmet,
                MoveClutch       = _clutch
            };
            _ampStep = 0; _cycle = false; _replace = false; _helmet = false; // consumir flancos
            return _latest;
        }

        /// Llamar desde el receptor BLE al recibir un paquete del guante.
        public void OnGlovePacket(Quaternion imuOrientation, bool spark, bool clutch, int ampStep,
                                  bool cycle, bool replaceElectrode = false, bool toggleHelmet = false)
        {
            _rawImu = imuOrientation;
            _spark  = spark;
            _clutch = clutch;
            if (ampStep != 0) _ampStep = ampStep;
            if (cycle)            _cycle   = true;
            if (replaceElectrode) _replace = true;
            if (toggleHelmet)     _helmet  = true;
        }
    }
}
