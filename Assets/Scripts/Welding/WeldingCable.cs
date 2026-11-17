using UnityEngine;

namespace FrankaVR.Welding
{
    /// Cable de soldadura entre la maquina y el porta-electrodos, dibujado como catenaria.
    ///
    /// No es decoracion gratuita: en teleoperacion real el cable es una restriccion fisica
    /// (limita el alcance y se engancha). Verlo colgar y seguir al brazo hace mucho por la
    /// sensacion de estar operando una maquina y no un puntero en el vacio.
    ///
    /// La forma es una catenaria aproximada: interpolacion entre los dos extremos mas una
    /// flecha (comba) que cuelga por gravedad, con el maximo en el medio del vano.
    [RequireComponent(typeof(LineRenderer))]
    public class WeldingCable : MonoBehaviour
    {
        [Header("Extremos")]
        public Transform FromMachine;   // salida de la maquina de soldar
        public Transform ToTorch;       // toma del porta-electrodos

        [Header("Forma")]
        [Tooltip("Puntos de la curva. 14-20 alcanza y es barato en mobile.")]
        [Range(4, 40)] public int Segments = 16;
        [Tooltip("Comba maxima del cable, en metros, con el brazo cerca de la maquina.")]
        public float MaxSag = 0.28f;
        [Tooltip("Largo util del cable. Al estirarse cerca de este valor, la comba se va a cero.")]
        public float CableLength = 1.8f;

        [Header("Aspecto")]
        public float Width = 0.012f;
        public Color CableColor = new Color(0.10f, 0.10f, 0.12f);

        LineRenderer _lr;

        void Awake()
        {
            _lr = GetComponent<LineRenderer>();
            _lr.useWorldSpace = true;
            _lr.startWidth = Width;
            _lr.endWidth = Width;
            _lr.positionCount = Mathf.Max(4, Segments);

            if (_lr.material != null) _lr.material.color = CableColor;
        }

        void LateUpdate()
        {
            if (_lr == null || FromMachine == null || ToTorch == null) return;

            Vector3 a = FromMachine.position;
            Vector3 b = ToTorch.position;
            float span = Vector3.Distance(a, b);

            // Cuanto mas estirado, menos cuelga. Al llegar al largo util, queda tenso.
            float slack = Mathf.Clamp01(1f - span / Mathf.Max(0.01f, CableLength));
            float sag = MaxSag * slack;

            int n = _lr.positionCount;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                Vector3 p = Vector3.Lerp(a, b, t);
                // Parabola: maxima comba en el medio del vano, cero en los extremos.
                p.y -= sag * 4f * t * (1f - t);
                _lr.SetPosition(i, p);
            }
        }
    }
}
