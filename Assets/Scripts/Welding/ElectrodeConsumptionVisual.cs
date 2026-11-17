using UnityEngine;

namespace FrankaVR.Welding
{
    /// <summary>
    /// Compatibilidad con versiones anteriores del proyecto.
    /// La visualizacion de consumo ahora vive exclusivamente en WeldingTorch para evitar
    /// dos varillas superpuestas. Si este componente ya estaba agregado a la escena,
    /// puede quedarse: solo asegura que el visual procedural este activado.
    /// </summary>
    [DisallowMultipleComponent]
    public class ElectrodeConsumptionVisual : MonoBehaviour
    {
        public WeldingTorch Torch;

        [Tooltip("Conservado por compatibilidad. WeldingTorch se encarga de ocultar la varilla original.")]
        public Transform OriginalVisualRoot;
        public bool HideOriginalVisual = true;
        public float DiameterScale = 1.15f;

        void Awake()
        {
            if (Torch == null) Torch = GetComponent<WeldingTorch>();
            if (Torch == null) Torch = FindObjectOfType<WeldingTorch>();
            if (Torch == null) return;

            Torch.UseProceduralElectrodeVisual = true;
            Torch.HideOriginalElectrodeVisual = HideOriginalVisual;
            Torch.DisableOriginalElectrodeColliders = true;
            Torch.AutoFindElectrodeVisual = true;
            Torch.UseArcTipOverride = false;
            Torch.VisualDiameterScale = Mathf.Clamp(DiameterScale, 0.8f, 2f);

            if (OriginalVisualRoot != null)
                Torch.ElectrodeVisual = OriginalVisualRoot;

            // No dibuja nada propio. Toda la representacion queda centralizada en WeldingTorch.
            enabled = false;
        }
    }
}
