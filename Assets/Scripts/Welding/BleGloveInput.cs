using UnityEngine;

namespace FrankaVR.Teleop
{
    /// <summary>
    /// Fuente de teleoperacion para el guante principal (IMU) + botonera secundaria.
    ///
    /// - Orientacion: la IMU se calibra contra una pose neutra. Desde ahi, la inclinacion
    ///   de la mano se aplica directamente al eje del electrodo.
    /// - Posicion: control por VELOCIDAD con embrague. Al presionar el clutch se toma una
    ///   referencia; la rotacion relativa de la mano se convierte en un vector de velocidad.
    /// - Botonera secundaria: chispa, amperaje, cambio/reposicion de electrodo y careta.
    ///
    /// No hace BLE por si solo: recibe paquetes desde GlovePacketBridge/transport Android.
    /// </summary>
    public class BleGloveInput : MonoBehaviour, ITeleopInput
    {
        [Header("Calibracion IMU")]
        [Tooltip("Alineacion fija entre los ejes fisicos del sensor y los ejes deseados del control.")]
        public Quaternion MountOffset = Quaternion.identity;
        [Tooltip("Al recibir el primer paquete valido, esa pose de mano pasa a ser la pose neutra.")]
        public bool AutoCalibrateOnFirstPacket = true;
        [Tooltip("Suavizado de orientacion. 10-18 suele sentirse estable sin demasiado retardo.")]
        [Range(0f, 30f)] public float OrientationResponse = 14f;

        [Header("Traslacion por inclinacion + clutch")]
        [Tooltip("Por debajo de este angulo no hay movimiento.")]
        public float TiltDeadzoneDeg = 4f;
        [Tooltip("A este angulo se alcanza velocidad maxima.")]
        public float TiltMaxDeg = 28f;
        [Tooltip("Curva de respuesta. >1 da mas precision cerca del centro.")]
        [Range(0.5f, 3f)] public float TiltExponent = 1.35f;
        [Tooltip("Escala por eje del comando final X/Y/Z.")]
        public Vector3 TranslationAxisScale = Vector3.one;
        public bool InvertLateral = false;
        public bool InvertVertical = false;
        public bool InvertForward = false;

        [Header("Joystick del mando ESP32 (traslacion)")]
        [Tooltip("Velocidad maxima con el joystick a fondo (misma escala que el teclado: 0.2 lento, 0.5 crucero).")]
        public float JoystickMaxRate = 0.5f;
        [Tooltip("Curva de respuesta. >1 da mas precision cerca del centro.")]
        [Range(1f, 3f)] public float JoystickExponent = 1.6f;
        public bool InvertJoystickX = false;
        public bool InvertJoystickY = false;
        [Tooltip("Si el joystick deja de transmitir, se detiene el movimiento.")]
        public float JoystickTimeoutSec = 0.35f;

        [Header("Seguridad / conexion")]
        [Tooltip("Si la mano principal deja de transmitir, se libera el clutch.")]
        public float PrimaryTimeoutSec = 0.35f;
        [Tooltip("Si la botonera deja de transmitir, se corta la chispa.")]
        public float ButtonsTimeoutSec = 0.35f;

        [Header("Estado (solo lectura)")]
        public bool HasImuPacket;
        public bool IsCalibrated;
        public bool ClutchHeld;
        public bool SparkHeld;
        public float PacketAgeSec;
        public Vector3 CurrentRate;
        public Vector2 JoystickAxes;
        public bool JoystickVertical;

        float _lastJoystickTime = -999f;
        Quaternion _rawImu = Quaternion.identity;
        Quaternion _zeroImu = Quaternion.identity;
        Quaternion _clutchRef = Quaternion.identity;
        Quaternion _smoothedOrientation = Quaternion.identity;
        bool _prevClutch;
        float _lastPrimaryPacketTime = -999f;
        float _lastButtonsPacketTime = -999f;

        bool _cycleEdge, _replaceEdge, _helmetEdge, _resetOrientationEdge;
        int _ampStepEdge;

        bool _prevAmpPlus, _prevAmpMinus, _prevCycle, _prevReplace, _prevHelmet, _prevReset;

        public TeleopCommand Poll(Quaternion currentToolOrientation)
        {
            PacketAgeSec = HasImuPacket ? Mathf.Max(0f, Time.unscaledTime - _lastPrimaryPacketTime) : 999f;

            bool primaryFresh = HasImuPacket && PacketAgeSec <= Mathf.Max(0.05f, PrimaryTimeoutSec);
            bool buttonsFresh = (Time.unscaledTime - _lastButtonsPacketTime) <= Mathf.Max(0.05f, ButtonsTimeoutSec);
            if (!primaryFresh) ClutchHeld = false;
            if (!buttonsFresh) SparkHeld = false;

            if (!HasImuPacket)
            {
                return new TeleopCommand
                {
                    Orientation = Quaternion.identity,
                    TranslationRate = Vector3.zero,
                    SparkHeld = SparkHeld,
                    AmperageStep = ConsumeAmpStep(),
                    CycleElectrode = Consume(ref _cycleEdge),
                    ReplaceElectrode = Consume(ref _replaceEdge),
                    ToggleHelmet = Consume(ref _helmetEdge),
                    ResetOrientation = Consume(ref _resetOrientationEdge),
                    MoveClutch = false
                };
            }

            Quaternion imu = NormalizeSafe(MountOffset * _rawImu);

            if (!IsCalibrated && AutoCalibrateOnFirstPacket)
                CalibrateOrientationToCurrent();

            // Delta en marco mundo: la pose neutra produce identidad y al inclinar la mano
            // el mismo giro se aplica al vector BaseApproach del controlador.
            Quaternion relative = IsCalibrated
                ? NormalizeSafe(imu * Quaternion.Inverse(_zeroImu))
                : imu;

            float k = OrientationResponse <= 0f
                ? 1f
                : 1f - Mathf.Exp(-OrientationResponse * Mathf.Max(0f, Time.deltaTime));
            _smoothedOrientation = Quaternion.Slerp(_smoothedOrientation, relative, k);

            Vector3 rate = Vector3.zero;
            if (ClutchHeld)
            {
                if (!_prevClutch)
                    _clutchRef = imu;

                Quaternion relMove = NormalizeSafe(Quaternion.Inverse(_clutchRef) * imu);
                Vector3 rotVecDeg = RotationVectorDegrees(relMove);

                // Rotacion X -> avance/retroceso Z
                // Rotacion Y -> subir/bajar Y
                // Rotacion Z -> lateral X
                rate = new Vector3(
                    ShapeAxis(rotVecDeg.z),
                    ShapeAxis(rotVecDeg.y),
                    ShapeAxis(rotVecDeg.x));

                if (InvertLateral) rate.x = -rate.x;
                if (InvertVertical) rate.y = -rate.y;
                if (InvertForward) rate.z = -rate.z;

                rate = Vector3.Scale(rate, TranslationAxisScale);
                rate = Vector3.ClampMagnitude(rate, 1f);
            }
            else
            {
                // Mando ESP32: la IMU solo orienta y el joystick traslada.
                rate = JoystickRate();
            }

            _prevClutch = ClutchHeld;
            CurrentRate = rate;
            bool moveEnabled = ClutchHeld || JoystickFresh;

            return new TeleopCommand
            {
                Orientation = _smoothedOrientation,
                TranslationRate = rate,
                SparkHeld = SparkHeld,
                AmperageStep = ConsumeAmpStep(),
                CycleElectrode = Consume(ref _cycleEdge),
                ReplaceElectrode = Consume(ref _replaceEdge),
                ToggleHelmet = Consume(ref _helmetEdge),
                ResetOrientation = Consume(ref _resetOrientationEdge),
                MoveClutch = moveEnabled
            };
        }

        /// <summary>True si llegaron paquetes JOY recientes.</summary>
        public bool JoystickFresh =>
            (Time.unscaledTime - _lastJoystickTime) <= Mathf.Max(0.05f, JoystickTimeoutSec);

        /// <summary>
        /// Velocidad pedida por el joystick, en la misma convencion que TranslationRate:
        /// X lateral, Y vertical, Z adelante. Con el boton del joystick apretado, el eje Y
        /// del stick mueve en altura en vez de adelante/atras.
        /// </summary>
        public Vector3 JoystickRate()
        {
            if (!JoystickFresh) return Vector3.zero;
            float x = ShapeJoystick(InvertJoystickX ? -JoystickAxes.x : JoystickAxes.x);
            float y = ShapeJoystick(InvertJoystickY ? -JoystickAxes.y : JoystickAxes.y);
            Vector3 rate = JoystickVertical ? new Vector3(x, y, 0f) : new Vector3(x, 0f, y);
            return Vector3.ClampMagnitude(rate, 1f) * JoystickMaxRate;
        }

        float ShapeJoystick(float v)
        {
            return Mathf.Sign(v) * Mathf.Pow(Mathf.Clamp01(Mathf.Abs(v)), JoystickExponent);
        }

        /// <summary>Paquete JOY del mando ESP32 (zona muerta ya aplicada en el firmware).</summary>
        public void OnJoystick(float x, float y, bool vertical)
        {
            JoystickAxes = new Vector2(x, y);
            JoystickVertical = vertical;
            _lastJoystickTime = Time.unscaledTime;
        }

        float ShapeAxis(float deg)
        {
            float a = Mathf.Abs(deg);
            if (a <= TiltDeadzoneDeg) return 0f;

            float den = Mathf.Max(1f, TiltMaxDeg - TiltDeadzoneDeg);
            float t = Mathf.Clamp01((a - TiltDeadzoneDeg) / den);
            t = Mathf.Pow(t, TiltExponent);
            return Mathf.Sign(deg) * t;
        }

        static Vector3 RotationVectorDegrees(Quaternion q)
        {
            q.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            if (axis.sqrMagnitude < 1e-8f || Mathf.Abs(angle) < 1e-5f) return Vector3.zero;
            return axis.normalized * angle;
        }

        static Quaternion NormalizeSafe(Quaternion q)
        {
            float m = Mathf.Sqrt(q.x*q.x + q.y*q.y + q.z*q.z + q.w*q.w);
            if (m < 1e-8f) return Quaternion.identity;
            float inv = 1f / m;
            return new Quaternion(q.x*inv, q.y*inv, q.z*inv, q.w*inv);
        }

        static bool Consume(ref bool edge)
        {
            bool v = edge;
            edge = false;
            return v;
        }

        int ConsumeAmpStep()
        {
            int v = _ampStepEdge;
            _ampStepEdge = 0;
            return v;
        }

        /// <summary>Usa la pose IMU actual como orientacion neutra.</summary>
        public void CalibrateOrientationToCurrent()
        {
            if (!HasImuPacket) return;
            _zeroImu = NormalizeSafe(MountOffset * _rawImu);
            _smoothedOrientation = Quaternion.identity;
            IsCalibrated = true;
        }

        public void ClearCalibration()
        {
            IsCalibrated = false;
            _zeroImu = Quaternion.identity;
            _smoothedOrientation = Quaternion.identity;
        }

        /// <summary>
        /// Paquete de la mano principal. El quaternion debe venir en orden Unity (x,y,z,w).
        /// clutch=true activa el control de velocidad.
        /// </summary>
        public void OnPrimaryGlovePacket(Quaternion imuOrientation, bool clutch)
        {
            _rawImu = NormalizeSafe(imuOrientation);
            ClutchHeld = clutch;
            HasImuPacket = true;
            _lastPrimaryPacketTime = Time.unscaledTime;

            if (!IsCalibrated && AutoCalibrateOnFirstPacket)
                CalibrateOrientationToCurrent();
        }

        /// <summary>Paquete de la botonera de la otra mano.</summary>
        public void OnSecondaryButtons(bool spark, bool ampPlus, bool ampMinus,
                                       bool cycle, bool replaceElectrode,
                                       bool toggleHelmet, bool resetOrientation)
        {
            _lastButtonsPacketTime = Time.unscaledTime;
            SparkHeld = spark;

            if (ampPlus && !_prevAmpPlus) _ampStepEdge += 1;
            if (ampMinus && !_prevAmpMinus) _ampStepEdge -= 1;
            if (cycle && !_prevCycle) _cycleEdge = true;
            if (replaceElectrode && !_prevReplace) _replaceEdge = true;
            if (toggleHelmet && !_prevHelmet) _helmetEdge = true;
            if (resetOrientation && !_prevReset)
            {
                _resetOrientationEdge = true;
                CalibrateOrientationToCurrent();
            }

            _prevAmpPlus = ampPlus;
            _prevAmpMinus = ampMinus;
            _prevCycle = cycle;
            _prevReplace = replaceElectrode;
            _prevHelmet = toggleHelmet;
            _prevReset = resetOrientation;
        }

        /// Compatibilidad con el stub anterior.
        public void OnGlovePacket(Quaternion imuOrientation, bool spark, bool clutch, int ampStep,
                                  bool cycle, bool replaceElectrode = false, bool toggleHelmet = false)
        {
            OnPrimaryGlovePacket(imuOrientation, clutch);
            _lastButtonsPacketTime = Time.unscaledTime;
            SparkHeld = spark;
            if (ampStep != 0) _ampStepEdge += Mathf.Clamp(ampStep, -1, 1);
            if (cycle) _cycleEdge = true;
            if (replaceElectrode) _replaceEdge = true;
            if (toggleHelmet) _helmetEdge = true;
        }
    }
}
