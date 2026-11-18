using UnityEngine;

/// <summary>
/// Perfil conservador para Cardboard/mobile: prioriza frame-rate y estabilidad visual.
/// </summary>
public class MobileVRQuality : MonoBehaviour
{
    public int TargetFps = 60;
    public int PixelLights = 2;
    public float ShadowDistance = 7f;
    public float LodBias = 0.85f;
    public bool DisableSoftParticles = true;
    [Tooltip("Escala de la textura de cada ojo en Cardboard. >1 da texto y bordes mas nitidos a costa de GPU.")]
    public float EyeRenderScale = 1.25f;
    [Tooltip("Antialias (MSAA) en el celular: bordes del robot y de los paneles mas limpios.")]
    public int AntiAliasing = 4;
    bool _eyeScaleApplied;

    void Awake()
    {
        Application.targetFrameRate = TargetFps;
        QualitySettings.vSyncCount = 0;
        QualitySettings.pixelLightCount = PixelLights;
        QualitySettings.shadowDistance = ShadowDistance;
        QualitySettings.lodBias = LodBias;
        if (DisableSoftParticles) QualitySettings.softParticles = false;
#if UNITY_ANDROID && !UNITY_EDITOR
        QualitySettings.antiAliasing = AntiAliasing;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Camera camera = Camera.main;
        if (camera != null)
        {
            var pose = camera.GetComponent<UnityEngine.SpatialTracking.TrackedPoseDriver>();
            if (pose == null) pose = camera.gameObject.AddComponent<UnityEngine.SpatialTracking.TrackedPoseDriver>();
            pose.SetPoseSource(UnityEngine.SpatialTracking.TrackedPoseDriver.DeviceType.GenericXRDevice,
                UnityEngine.SpatialTracking.TrackedPoseDriver.TrackedPose.Head);
            pose.trackingType = UnityEngine.SpatialTracking.TrackedPoseDriver.TrackingType.RotationOnly;
            pose.updateType = UnityEngine.SpatialTracking.TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
        }
#endif
    }

    void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Google.XR.Cardboard.Api.HasDeviceParams()) Google.XR.Cardboard.Api.ScanDeviceParams();
#endif
    }

    void Update()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!_eyeScaleApplied) ApplyEyeRenderScale();
        if (Google.XR.Cardboard.Api.IsGearButtonPressed) Google.XR.Cardboard.Api.ScanDeviceParams();
        if (Google.XR.Cardboard.Api.IsCloseButtonPressed) Application.Quit();
        if (Google.XR.Cardboard.Api.IsTriggerHeldPressed) Google.XR.Cardboard.Api.Recenter();
        if (Google.XR.Cardboard.Api.HasNewDeviceParams()) Google.XR.Cardboard.Api.ReloadDeviceParams();
        Google.XR.Cardboard.Api.UpdateScreenParams();
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    // La pantalla XR aparece cuando el loader de Cardboard termina: se reintenta hasta encontrarla.
    void ApplyEyeRenderScale()
    {
        var displays = new System.Collections.Generic.List<UnityEngine.XR.XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(displays);
        foreach (UnityEngine.XR.XRDisplaySubsystem display in displays)
        {
            if (!display.running) continue;
            display.scaleOfAllRenderTargets = EyeRenderScale;
            _eyeScaleApplied = true;
        }
    }
#endif
}
