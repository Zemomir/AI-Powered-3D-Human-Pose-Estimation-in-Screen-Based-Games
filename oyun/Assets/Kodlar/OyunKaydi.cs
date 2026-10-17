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
    public Yildizlar yildizlar;
    public Seyirciler seyirciler;

    static readonly string[] Sutunlar = {
        "saat", "t", "olay", "hal", "bagli", "kalibre", "klavye", "donuk",
        "ham_egim", "ham_one", "egim", "one", "egim_sifir", "one_sifir", "hiz",
        "hedef_hiz", "hiz_hassasiyeti", "donus_hassasiyeti", "yumusatma", "yol_disinda", "ilerleme", "yildiz_toplam",
        "sol_el", "sag_el", "sol_kalkik", "sag_kalkik", "cak_toplam", "hiz_tutuldu"
    };
    StreamWriter yazici;
    float sonSatir, sonFlush;
    bool uyariVerildi;
    readonly System.Collections.Generic.List<string> tuslar = new System.Collections.Generic.List<string>();

    void Start()
    {
        try
        {
            string kopru = KopruYonetici.KopruKlasoruBul();
            string dizin = kopru != null ? Path.Combine(kopru, "olcumler") :
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "olcumler"));
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
        if (motor == null || alici == null) return;
        string olay = TusOlayi();
        if (yildizlar != null && !string.IsNullOrEmpty(yildizlar.KareOlayi))
            olay += (olay.Length == 0 ? "" : " ") + yildizlar.KareOlayi;
        if (seyirciler != null && !string.IsNullOrEmpty(seyirciler.KareOlayi))
            olay += (olay.Length == 0 ? "" : " ") + seyirciler.KareOlayi;
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
                    Bool(motor.YumusatmaAcik), Bool(motor.YolDisinda), Say(motor.Ilerleme),
                    (yildizlar != null ? yildizlar.Toplanan : 0).ToString(CultureInfo.InvariantCulture),
                    Say(alici.SolEl), Say(alici.SagEl), Bool(seyirciler != null && seyirciler.SolKalkik),
                    Bool(seyirciler != null && seyirciler.SagKalkik),
                    (seyirciler != null ? seyirciler.SolCak + seyirciler.SagCak : 0).ToString(CultureInfo.InvariantCulture),
                    Bool(alici.HizTutuluyor)
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
        tuslar.Clear();
        if (Input.GetKeyDown(KeyCode.R)) tuslar.Add("R");
        if (Input.GetKeyDown(KeyCode.C)) tuslar.Add("C");
        if (Input.GetKeyDown(KeyCode.Y)) tuslar.Add("Y");
        if (Input.GetKeyDown(KeyCode.K)) tuslar.Add("K");
        if (Input.GetKeyDown(KeyCode.H)) tuslar.Add("H");
        if (Input.GetKeyDown(KeyCode.M)) tuslar.Add("M");
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
