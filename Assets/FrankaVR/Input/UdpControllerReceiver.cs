using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace FrankaVR.Teleop
{
    /// <summary>
    /// Transporte Wi-Fi del mando ESP32. El ESP32 crea la red "FrankaVR-Mando"
    /// (192.168.4.1); el celular o la PC se conectan a esa red. Esta clase escucha
    /// paquetes CSV (CTL/IMU/BTN, uno por linea) en el puerto 4211 y cada segundo
    /// manda "HELLO" al ESP32 (4210) para que le conteste directo a esta IP.
    /// No requiere plugins nativos: funciona igual en el Editor y en Android.
    /// </summary>
    public class UdpControllerReceiver : MonoBehaviour
    {
        public int ListenPort = 4211;
        public string ControllerAddress = "192.168.4.1";
        public int ControllerPort = 4210;
        public float HelloIntervalSec = 1f;
        public GlovePacketBridge Bridge;

        [Header("Estado (solo lectura)")]
        public bool Listening;
        public int Received;
        public string LastSender = "";
        public string LastError = "";

        volatile UdpClient _socket;
        Thread _thread;
        volatile bool _run;
        volatile string _sender = "";
        readonly ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();
        IPEndPoint _controller;
        float _nextHello;

        void OnEnable() => Open();
        void OnDisable() => Close();

        void Open()
        {
            Close();
            try
            {
                _socket = new UdpClient(AddressFamily.InterNetwork);
                _socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _socket.Client.Bind(new IPEndPoint(IPAddress.Any, ListenPort));
                _socket.EnableBroadcast = true;
                _controller = new IPEndPoint(IPAddress.Parse(ControllerAddress), ControllerPort);
                _run = true;
                UdpClient socket = _socket;
                _thread = new Thread(() => Loop(socket)) { IsBackground = true, Name = "FrankaVR mando UDP" };
                _thread.Start();
                Listening = true;
                LastError = "";
            }
            catch (Exception e)
            {
                Listening = false;
                LastError = e.Message;
                Debug.LogWarning("[Mando UDP] No se pudo abrir el puerto " + ListenPort + ": " + e.Message);
            }
        }

        // Cada hilo lee solo su socket: al reabrir (OnEnable, cambio de red) el hilo viejo termina
        // en vez de quedar leyendo el socket nuevo en paralelo.
        void Loop(UdpClient socket)
        {
            var from = new IPEndPoint(IPAddress.Any, 0);
            while (_run && socket == _socket)
            {
                try
                {
                    byte[] data = socket.Receive(ref from);
                    _sender = from.Address.ToString();
                    foreach (string line in Encoding.ASCII.GetString(data).Split('\n'))
                    {
                        string trimmed = line.Trim();
                        if (trimmed.Length > 2) _queue.Enqueue(trimmed);
                    }
                }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { if (!_run || socket != _socket) break; }
                catch (Exception) { }
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        // La red del ESP32 no tiene internet: con datos moviles prendidos Android manda los
        // HELLO por la red celular y el ESP32 nunca aprende la IP del celular (queda en
        // broadcast, con mas latencia). Atar la app a la red Wi-Fi lo evita. Necesita el
        // permiso ACCESS_NETWORK_STATE (lo agrega CardboardAndroidGradle); sin permiso
        // queda como antes y alcanza con apagar los datos moviles.
        float _nextWifiCheck;
        bool _wifiBindUnavailable;
        AndroidJavaObject _boundWifi;

        void BindToWifi()
        {
            if (_wifiBindUnavailable || Time.unscaledTime < _nextWifiCheck) return;
            _nextWifiCheck = Time.unscaledTime + 3f;
            // El QR del visor (primer arranque) baja sus parametros de internet: no atar antes.
            if (!Google.XR.Cardboard.Api.HasDeviceParams()) return;
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var manager = activity.Call<AndroidJavaObject>("getSystemService", "connectivity"))
                {
                    AndroidJavaObject[] networks = manager.Call<AndroidJavaObject[]>("getAllNetworks");
                    AndroidJavaObject wifi = null;
                    foreach (AndroidJavaObject network in networks ?? new AndroidJavaObject[0])
                    {
                        AndroidJavaObject caps = manager.Call<AndroidJavaObject>("getNetworkCapabilities", network);
                        // Solo una Wi-Fi SIN internet validado (la del ESP32). Con internet Android ya la usa.
                        bool isWifi = caps != null && caps.Call<bool>("hasTransport", 1) && // TRANSPORT_WIFI
                                      !caps.Call<bool>("hasCapability", 16);                  // NET_CAPABILITY_VALIDATED
                        caps?.Dispose();
                        if (isWifi && wifi == null) wifi = network; else network.Dispose();
                    }
                    if (wifi == null) return;
                    if (_boundWifi != null && _boundWifi.Call<bool>("equals", wifi)) { wifi.Dispose(); return; }
                    if (manager.Call<bool>("bindProcessToNetwork", wifi))
                    {
                        _boundWifi?.Dispose();
                        _boundWifi = wifi;
                        Debug.Log("[Mando UDP] App atada a la red Wi-Fi del mando");
                        Open(); // los sockets nuevos usan la red atada; el viejo no
                    }
                    else wifi.Dispose();
                }
            }
            catch (Exception e)
            {
                _wifiBindUnavailable = true;
                LastError = "Sin acceso a redes de Android: apagar datos moviles. " + e.Message;
                Debug.LogWarning("[Mando UDP] " + LastError);
            }
        }
#endif

        void Update()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            BindToWifi();
#endif
            if (Bridge == null) Bridge = GetComponent<GlovePacketBridge>();
            if (Bridge == null) Bridge = FindAnyObjectByType<GlovePacketBridge>();
            // Tras una pausa (app en segundo plano) descartar lo viejo: no repetir botones.
            while (_queue.Count > 32) _queue.TryDequeue(out _);
            while (_queue.TryDequeue(out string line))
            {
                Received++;
                if (Bridge != null) Bridge.OnBleMessage(line);
            }
            LastSender = _sender;
            if (Listening && Time.unscaledTime >= _nextHello)
            {
                _nextHello = Time.unscaledTime + Mathf.Max(0.2f, HelloIntervalSec);
                try
                {
                    byte[] hello = Encoding.ASCII.GetBytes("HELLO,FrankaVR," + ListenPort);
                    _socket.Send(hello, hello.Length, _controller);
                }
                catch (Exception e) { LastError = e.Message; } // sin la red del mando: normal
            }
        }

        void Close()
        {
            _run = false;
            Listening = false;
            try { _socket?.Close(); } catch (Exception) { }
            _socket = null;
            _thread = null;
        }
    }
}
