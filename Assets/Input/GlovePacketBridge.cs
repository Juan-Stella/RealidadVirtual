using System;
using System.Globalization;
using UnityEngine;

namespace FrankaVR.Teleop
{
    /// <summary>
    /// Puente simple entre un transporte BLE Android y BleGloveInput.
    ///
    /// El plugin/servicio Android solo tiene que llamar por UnitySendMessage:
    ///   gameObject = objeto que tenga este componente
    ///   metodo     = OnBleMessage
    ///
    /// Formatos CSV soportados:
    ///   IMU,qw,qx,qy,qz,clutch
    ///   BTN,spark,ampPlus,ampMinus,cycle,replace,helmet,resetOrientation[,recenter,nextView,calibrate]
    ///   CTL,jx,jy,gatillo,acercar,retirar,ok,deshacer,borrar,altura,calibrar,vista,imu,qw,qx,qy,qz
    ///       (mando ESP32 por Wi-Fi: ver HandControllerInput y UdpControllerReceiver)
    ///
    /// Todos los booleanos pueden ser 0/1 o true/false.
    /// </summary>
    public class GlovePacketBridge : MonoBehaviour
    {
        public BleGloveInput Glove;
        public HandControllerInput Controller;
        public ViewpointManager Viewpoints;
        public bool LogPackets = false;

        bool _prevRecenter, _prevNextView, _prevCalibrate;

        void Awake()
        {
            if (Glove == null) Glove = FindObjectOfType<BleGloveInput>();
            if (Viewpoints == null) Viewpoints = FindObjectOfType<ViewpointManager>();
            // Mando ESP32 por Wi-Fi: receptor UDP y estado del mando junto al puente.
            if (Controller == null) Controller = FindAnyObjectByType<HandControllerInput>();
            if (Controller == null) Controller = gameObject.AddComponent<HandControllerInput>();
            if (GetComponent<UdpControllerReceiver>() == null) gameObject.AddComponent<UdpControllerReceiver>();
        }

        public void OnBleMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            if (LogPackets) Debug.Log("[BLE] " + message);

            try
            {
                string[] p = message.Trim().Split(',');
                if (p.Length == 0) return;

                string kind = p[0].Trim().ToUpperInvariant();
                if (kind == "IMU") ParseImu(p);
                else if (kind == "BTN") ParseButtons(p);
                else if (kind == "CTL") ParseController(p);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BLE] Paquete invalido: '" + message + "' -> " + ex.Message);
            }
        }

        void ParseImu(string[] p)
        {
            if (Glove == null) return;
            if (p.Length != 6) throw new FormatException("IMU requiere qw,qx,qy,qz,clutch");

            // El protocolo manda qw primero por comodidad del firmware.
            float qw = F(p[1]);
            float qx = F(p[2]);
            float qy = F(p[3]);
            float qz = F(p[4]);
            bool clutch = B(p[5]);

            Quaternion orientation = new Quaternion(qx, qy, qz, qw);
            float magnitudeSq = qx*qx + qy*qy + qz*qz + qw*qw;
            if (float.IsInfinity(magnitudeSq) || magnitudeSq < 1e-8f)
                throw new FormatException("Cuaternion IMU nulo o fuera de rango");
            Glove.OnPrimaryGlovePacket(orientation, clutch);
        }

        void ParseButtons(string[] p)
        {
            if (Glove == null) return;
            if (p.Length < 8 || p.Length > 11)
                throw new FormatException("BTN requiere 7 botones y hasta 3 acciones opcionales");

            bool spark   = B(p[1]);
            bool ampPlus = B(p[2]);
            bool ampMinus= B(p[3]);
            bool cycle   = B(p[4]);
            bool replace = B(p[5]);
            bool helmet  = B(p[6]);
            bool reset   = B(p[7]);

            bool recenter = p.Length > 8 && B(p[8]);
            bool nextView = p.Length > 9 && B(p[9]);
            bool calibrate= p.Length > 10 && B(p[10]);

            Glove.OnSecondaryButtons(spark, ampPlus, ampMinus, cycle, replace, helmet, reset);

            if (recenter && !_prevRecenter) CardboardRecenterProxy.TryRecenter();
            if (nextView && !_prevNextView && Viewpoints != null) Viewpoints.Next();
            if (calibrate && !_prevCalibrate) Glove.CalibrateOrientationToCurrent();

            _prevRecenter = recenter;
            _prevNextView = nextView;
            _prevCalibrate = calibrate;
        }

        void ParseController(string[] p)
        {
            if (Controller == null) return;
            if (p.Length != 17) throw new FormatException("CTL requiere 16 campos");
            Quaternion q = new Quaternion(F(p[14]), F(p[15]), F(p[16]), F(p[13]));
            Controller.OnPacket(F(p[1]), F(p[2]), B(p[3]), B(p[4]), B(p[5]), B(p[6]), B(p[7]),
                B(p[8]), B(p[9]), B(p[10]), B(p[11]), B(p[12]), q);
        }

        static float F(string s)
        {
            float value = float.Parse(s, CultureInfo.InvariantCulture);
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new FormatException("Numero no finito");
            return value;
        }

        static bool B(string s)
        {
            s = s.Trim();
            if (s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("on", StringComparison.OrdinalIgnoreCase)) return true;
            if (s == "0" || s.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("off", StringComparison.OrdinalIgnoreCase)) return false;
            throw new FormatException("Booleano invalido: " + s);
        }
    }
}
