using UnityEngine;

namespace FrankaVR.V5
{
    public enum SimulatorModeV5 { Auto, Desktop, Cardboard }

    /// <summary>Separa claramente el modo de depuracion PC del modo Cardboard.</summary>
    public class SimulatorModeManagerV5 : MonoBehaviour
    {
        public SimulatorModeV5 Mode = SimulatorModeV5.Auto;
        public DesktopHeadLookV5 DesktopHeadLook;
        public ViewpointManager Viewpoints;
        public string ActiveMode = "Desktop";

        void Start()
        {
            if (DesktopHeadLook == null) DesktopHeadLook = FindObjectOfType<DesktopHeadLookV5>();
            if (Viewpoints == null) Viewpoints = FindObjectOfType<ViewpointManager>();
            ApplyMode();
        }

        public void ApplyMode()
        {
            bool cardboard = Mode == SimulatorModeV5.Cardboard ||
                             (Mode == SimulatorModeV5.Auto && Application.platform == RuntimePlatform.Android);
            ActiveMode = cardboard ? "Cardboard" : "Desktop";
            if (DesktopHeadLook != null) DesktopHeadLook.EnabledOnDesktop = !cardboard;
        }
    }
}
