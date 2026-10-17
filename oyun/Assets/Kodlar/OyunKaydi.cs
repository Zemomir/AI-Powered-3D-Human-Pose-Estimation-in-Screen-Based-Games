using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// Oyun verisini Python kaydıyla unix saatini (saat sütununu) kullanarak eşlemek için yazar.
// Motor'dan sonra çalışır ki satır o karenin son durumunu yazsın (C basılan karede yeni sıfır).
[DefaultExecutionOrder(100)]
public class OyunKaydi : MonoBehaviour
{
    public HareketAlici alici;
    public Motor motor;

    static readonly string[] Sutunlar = {
        "saat", "t", "olay", "hal", "bagli", "kalibre", "klavye", "donuk",
        "ham_egim", "ham_one", "egim", "one", "egim_sifir", "one_sifir", "hiz",
        "hedef_hiz", "hiz_hassasiyeti", "donus_hassasiyeti", "yumusatma", "yol_disinda", "ilerleme"
    };
    StreamWriter yazici;
    float sonSatir, sonFlush;
    bool uyariVerildi;

    void Start()
    {
        try
        {
            string dizin = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "olcumler"));
            Directory.CreateDirectory(dizin);
            string yol = Path.Combine(dizin, "motor_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".csv");
            yazici = new StreamWriter(yol, false, new UTF8Encoding(false)) { NewLine = "\n" };
            yazici.WriteLine(string.Join(",", Sutunlar));
            sonSatir = sonFlush = Time.realtimeSinceStartup;
        }
        catch (IOException e) { KayitHatasi(e); }
        catch (UnauthorizedAccessException e) { KayitHatasi(e); }
    }

    void Update()
    {
        if (yazici == null) return;
        string olay = TusOlayi();
        float simdi = Time.realtimeSinceStartup;
        if (olay.Length > 0 || simdi - sonSatir >= 1f / 30f)
        {
            try
            {
                yazici.WriteLine(string.Join(",", new[] {
                    UnixSaat(), Say(Time.realtimeSinceStartup), olay, motor.Hal.ToString(), Bool(alici.Bagli), Bool(alici.Kalibre),
                    Bool(alici.KlavyeModu), Bool(alici.GoruntuDonuk), Say(alici.Egim), Say(alici.One),
                    Say(motor.DuzeltilmisEgim), Say(motor.DuzeltilmisOne), Say(motor.EgimSifir), Say(motor.OneSifir),
                    Say(motor.Hiz), Say(motor.HedefHiz), Say(motor.HizHassasiyeti), Say(motor.DonusHassasiyeti),
                    Bool(motor.YumusatmaAcik), Bool(motor.YolDisinda), Say(motor.Ilerleme)
                }));
                sonSatir = simdi;
                if (simdi - sonFlush >= 1f) { yazici.Flush(); sonFlush = simdi; }
            }
            catch (IOException e) { KayitHatasi(e); }
            catch (UnauthorizedAccessException e) { KayitHatasi(e); }
        }
    }

    string TusOlayi()
    {
        var tuslar = new System.Collections.Generic.List<string>();
        if (Input.GetKeyDown(KeyCode.R)) tuslar.Add("R");
        if (Input.GetKeyDown(KeyCode.C)) tuslar.Add("C");
        if (Input.GetKeyDown(KeyCode.Y)) tuslar.Add("Y");
        if (Input.GetKeyDown(KeyCode.K)) tuslar.Add("K");
        foreach (char c in Input.inputString)
            if (c == '+' || c == '-' || c == '1' || c == '2') tuslar.Add(c.ToString());
        return string.Join(" ", tuslar);
    }

    static string UnixSaat() => (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0).ToString("F3", CultureInfo.InvariantCulture);
    static string Say(float x) => x.ToString("R", CultureInfo.InvariantCulture);
    static string Say(float? x) => x.HasValue ? Say(x.Value) : "";
    static string Bool(bool x) => x ? "1" : "0";

    void KayitHatasi(Exception e)
    {
        if (!uyariVerildi) { Debug.LogWarning("Oyun kaydı durduruldu: " + e.Message); uyariVerildi = true; }
        Kapat();
    }

    void OnDestroy() { Kapat(); }
    void OnApplicationQuit() { Kapat(); }
    void Kapat()
    {
        if (yazici == null) return;
        try { yazici.Dispose(); }
        catch (IOException e) { if (!uyariVerildi) { Debug.LogWarning("Oyun kaydı kapatılamadı: " + e.Message); uyariVerildi = true; } }
        catch (UnauthorizedAccessException e) { if (!uyariVerildi) { Debug.LogWarning("Oyun kaydı kapatılamadı: " + e.Message); uyariVerildi = true; } }
        finally { yazici = null; }
    }
}
