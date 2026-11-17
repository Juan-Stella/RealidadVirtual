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
    }
}
