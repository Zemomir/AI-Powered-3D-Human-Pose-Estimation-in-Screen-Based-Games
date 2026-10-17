using UnityEngine;

public class Grafik : MonoBehaviour
{
    void Awake()
    {
        Ayarla(PlayerPrefs.GetInt("GrafikKalitesi", 0));
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
        QualitySettings.vSyncCount = 1;
    }

    public static void Ayarla(int kademe)
    {
        kademe = Mathf.Clamp(kademe, 0, 2);
        QualitySettings.antiAliasing = kademe == 0 ? 4 : kademe == 1 ? 2 : 0;
        QualitySettings.shadowDistance = kademe == 0 ? 150f : kademe == 1 ? 80f : 35f;
        QualitySettings.shadowCascades = kademe == 0 ? 4 : kademe == 1 ? 2 : 0;
        QualitySettings.shadows = kademe == 2 ? ShadowQuality.HardOnly : ShadowQuality.All;
        QualitySettings.shadowResolution = kademe == 0 ? ShadowResolution.High : kademe == 1 ? ShadowResolution.Medium : ShadowResolution.Low;
        PlayerPrefs.SetInt("GrafikKalitesi", kademe);
        PlayerPrefs.Save();
    }
}
