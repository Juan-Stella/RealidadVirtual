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

        UdpClient _socket;
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
                _thread = new Thread(Loop) { IsBackground = true, Name = "FrankaVR mando UDP" };
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

        void Loop()
        {
            var from = new IPEndPoint(IPAddress.Any, 0);
            while (_run)
            {
                try
                {
                    byte[] data = _socket.Receive(ref from);
                    _sender = from.Address.ToString();
                    foreach (string line in Encoding.ASCII.GetString(data).Split('\n'))
                    {
                        string trimmed = line.Trim();
                        if (trimmed.Length > 2) _queue.Enqueue(trimmed);
                    }
                }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { if (!_run) break; }
                catch (Exception) { }
            }
        }

        void Update()
        {
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
