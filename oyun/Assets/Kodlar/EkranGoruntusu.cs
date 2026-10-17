using System;
using UnityEngine;

// Otomatik deneme için: oyun "-ekran <dosya_on_eki>" ile açılırsa belirli anlarda yalnız
// oyunun kendi görüntüsünü kaydeder (masaüstünü değil). Normal oyunda hiçbir şey yapmaz.
public class EkranGoruntusu : MonoBehaviour
{
    public float[] anlar = { 9.5f, 11.5f };
    string onEk;
    int sira;

    void Start()
    {
        var a = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(a, "-ekran");
        if (i >= 0 && i + 1 < a.Length) onEk = a[i + 1]; else { enabled = false; return; }
        int j = Array.IndexOf(a, "-ekranAnlar");  // ör. -ekranAnlar 3,6.5,9.5
        if (j >= 0 && j + 1 < a.Length)
            anlar = Array.ConvertAll(a[j + 1].Split(','), x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture));
    }

    void Update()
    {
        if (sira < anlar.Length && Time.realtimeSinceStartup >= anlar[sira])
        {
            ScreenCapture.CaptureScreenshot($"{onEk}{sira + 1}.png");
            sira++;
        }
    }
}
