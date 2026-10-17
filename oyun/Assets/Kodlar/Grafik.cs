using UnityEngine;

using System;

public class Grafik : MonoBehaviour
{
    void Awake()
    {
        Ayarla(PlayerPrefs.GetInt("GrafikKalitesi", 0));
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
        QualitySettings.vSyncCount = 1;
        // Unity processes these options before Awake. Preserve their requested mode and size.
        var args = Environment.GetCommandLineArgs();
        bool screenArg = Array.Exists(args, a => a == "-screen-fullscreen" || a == "-screen-width" || a == "-screen-height");
        if (!screenArg && PlayerPrefs.HasKey("TamEkranTercihi"))
            TamEkranUygula(PlayerPrefs.GetInt("TamEkranTercihi") != 0, false);
    }

    void Update()
    {
        bool altEnter = (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) &&
            (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt));
        if (Input.GetKeyDown(KeyCode.F11) || altEnter)
            TamEkranUygula(Screen.fullScreenMode != FullScreenMode.FullScreenWindow, true);
    }

    static void TamEkranUygula(bool tamEkran, bool kaydet)
    {
        if (kaydet)
        {
            PlayerPrefs.SetInt("TamEkranTercihi", tamEkran ? 1 : 0);
            PlayerPrefs.Save();
        }
        if (tamEkran)
            Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.FullScreenWindow);
        else
        {
            var ekran = Screen.currentResolution;
            Screen.SetResolution(Mathf.Max(640, Mathf.RoundToInt(ekran.width * .8f)),
                Mathf.Max(360, Mathf.RoundToInt(ekran.height * .8f)), FullScreenMode.Windowed);
        }
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
