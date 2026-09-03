using UnityEngine;

namespace FrankaVR.Welding
{
    /// Careta de soldar autooscurecible montada delante de la camara VR.
    ///
    /// Es el elemento que mas justifica el uso de VR en este proyecto: en soldadura real
    /// el operador trabaja casi a ciegas: con la careta bajada solo ve el charco de fusion
    /// y el arco; el resto de la escena desaparece. Reproducir esa restriccion de campo
    /// visual solo tiene sentido en un visor estereoscopico, no en un monitor.
    ///
    /// Funcionamiento: un quad oscuro delante de la camara cuya opacidad sube en pocos
    /// milisegundos al cebar el arco (como un filtro DIN 9-13) y baja al apagarlo.
    public class WeldingHelmet : MonoBehaviour
    {
        [Header("Referencias")]
        public WeldingTorch Torch;
        public Transform    HeadCamera;      // camara VR (o Camera.main)
        public Renderer     VisorQuad;       // quad delante de la camara

        [Header("Filtro")]
        [Range(0f, 1f)] public float ClearOpacity = 0.06f; // careta levantada / sin arco
        [Range(0f, 1f)] public float DarkOpacity  = 0.93f; // filtro activo con arco
        public float DarkenTimeSec = 0.05f;  // ms de reaccion al cebar
        public float ClearTimeSec  = 0.45f;  // vuelve mas lento, como el vidrio real
        public Color FilterTint = new Color(0.05f, 0.09f, 0.06f); // verde botella

        [Header("Colocacion")]
        public float Distance = 0.28f;
        public Vector3 LocalOffset = Vector3.zero;

        [Header("Estado")]
        public bool HelmetDown = true;       // se puede levantar para inspeccionar
        public float CurrentOpacity;

        Material _mat;

        void Start()
        {
            if (HeadCamera == null && Camera.main != null) HeadCamera = Camera.main.transform;
            if (VisorQuad != null)
            {
                _mat = VisorQuad.material;
                CurrentOpacity = ClearOpacity;
                ApplyOpacity();
            }
        }

        public void ToggleHelmet() => HelmetDown = !HelmetDown;

        void LateUpdate()
        {
            if (VisorQuad == null) return;

            // Seguir la cabeza.
            if (HeadCamera != null)
            {
                VisorQuad.transform.position = HeadCamera.position + HeadCamera.forward * Distance
                                             + HeadCamera.TransformDirection(LocalOffset);
                VisorQuad.transform.rotation = HeadCamera.rotation;
            }

            bool arcBright = Torch != null && (Torch.Arcing || Torch.State == ArcState.Striking);
            float goal = (HelmetDown && arcBright) ? DarkOpacity : ClearOpacity;
            if (!HelmetDown) goal = 0f;

            float rate = goal > CurrentOpacity ? DarkenTimeSec : ClearTimeSec;
            CurrentOpacity = Mathf.MoveTowards(CurrentOpacity, goal, Time.deltaTime / Mathf.Max(0.01f, rate));

            VisorQuad.enabled = CurrentOpacity > 0.001f;
            ApplyOpacity();
        }

        void ApplyOpacity()
        {
            if (_mat == null) return;
            _mat.color = new Color(FilterTint.r, FilterTint.g, FilterTint.b, CurrentOpacity);
        }
    }
}
