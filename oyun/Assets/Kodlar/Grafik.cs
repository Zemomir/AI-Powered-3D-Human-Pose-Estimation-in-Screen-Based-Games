using UnityEngine;

public class Grafik : MonoBehaviour
{
    void Awake()
    {
        QualitySettings.antiAliasing = 4;
        QualitySettings.shadowDistance = 150f;
        QualitySettings.shadowCascades = 4;
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.High;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
        QualitySettings.vSyncCount = 1;
    }
}
