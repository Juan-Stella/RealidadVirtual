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

    void Awake()
    {
        Application.targetFrameRate = TargetFps;
        QualitySettings.vSyncCount = 0;
        QualitySettings.pixelLightCount = PixelLights;
        QualitySettings.shadowDistance = ShadowDistance;
        QualitySettings.lodBias = LodBias;
        if (DisableSoftParticles) QualitySettings.softParticles = false;
#if UNITY_ANDROID && !UNITY_EDITOR
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
        if (Google.XR.Cardboard.Api.IsGearButtonPressed) Google.XR.Cardboard.Api.ScanDeviceParams();
        if (Google.XR.Cardboard.Api.IsCloseButtonPressed) Application.Quit();
        if (Google.XR.Cardboard.Api.IsTriggerHeldPressed) Google.XR.Cardboard.Api.Recenter();
        if (Google.XR.Cardboard.Api.HasNewDeviceParams()) Google.XR.Cardboard.Api.ReloadDeviceParams();
        Google.XR.Cardboard.Api.UpdateScreenParams();
#endif
    }
}
