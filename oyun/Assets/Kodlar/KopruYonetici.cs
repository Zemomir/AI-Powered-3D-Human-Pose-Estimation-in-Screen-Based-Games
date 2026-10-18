using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Yalnız kendi başlattığımız sürecin yaşam döngüsünü yönetiriz.
public class KopruYonetici : MonoBehaviour
{
    public HareketAlici alici;
    public string KopruKlasoru { get; private set; }
    public string PythonYolu
    {
        get
        {
            if (KopruKlasoru == null) return null;
            string paketPython = Path.Combine(KopruKlasoru, "python", "python.exe");
            if (File.Exists(paketPython)) return paketPython;
            string gelistirmePython = Path.Combine(KopruKlasoru, ".venv", "Scripts", "python.exe");
            return File.Exists(gelistirmePython) ? gelistirmePython : gelistirmePython;
        }
    }
    public bool KurulumTamam => PythonYolu != null && File.Exists(PythonYolu);
    public bool Harici { get; private set; }
    public string Hata { get; private set; }
    public bool Baslatiliyor => baslatBekliyor || kapananSurec != null || (surec != null && !mesajGeldi);
    public bool YenidenBaglaniyor => isteniyor && yenidenBaglaniyor;
    public bool Hazir => !TestModu && !Baslatiliyor && alici != null && alici.SonMesajYasi < 1f &&
        (Harici || (surec != null && !surec.HasExited && mesajGeldi));
    public string[] SonSatirlar { get { lock (kilit) return satirlar.ToArray(); } }

    Process surec, kapananSurec;
    readonly object kilit = new object();
    readonly Queue<string> satirlar = new Queue<string>();
    string sonStderr, telefonUrl = "";
    bool onizleme, baslatBekliyor, mesajGeldi, isteniyor, yenidenBaglaniyor;
    volatile bool stdoutBitti, stderrBitti;
    int baslangicMesajNo;
    float baslatZamani, acilisZamani, sonrakiKontrol, oldurZamani, sonrakiAyar;
    float yenidenBaglanmaBeklemesi = 1.2f;
    float sonDurdurmaZamani = -100f;

    public static bool TestModu
    {
        get
        {
            var a = Environment.GetCommandLineArgs();
            return Array.IndexOf(a, "-otopilot") >= 0 || Array.IndexOf(a, "-ekran") >= 0 || Array.IndexOf(a, "-ekranMenu") >= 0;
        }
    }

    public static bool MenuTestModu => Array.IndexOf(Environment.GetCommandLineArgs(), "-ekranMenu") >= 0;
    public static bool MenuAtlanir
    {
        get
        {
            var a = Environment.GetCommandLineArgs();
            return Array.IndexOf(a, "-otopilot") >= 0 || (Array.IndexOf(a, "-ekran") >= 0 && !MenuTestModu);
        }
    }

    public static string KopruKlasoruBul()
    {
        var a = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(a, "-kopru");
        if (i >= 0)
        {
            if (i + 1 >= a.Length) return null;
            try { return Dogrula(Path.GetFullPath(a[i + 1])); }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is IOException) { return null; }
        }
        string exe = Path.GetDirectoryName(Application.dataPath);
        string bulunan = Dogrula(Path.Combine(exe, "kopru"));
        if (bulunan != null) return bulunan;
        bulunan = Dogrula(Path.GetFullPath(Path.Combine(exe, "..")));
        if (bulunan != null) return bulunan;
        return Application.isEditor ? Dogrula(Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."))) : null;
    }

    static string Dogrula(string yol) => File.Exists(Path.Combine(yol, "hareket_yayini.py")) ? yol : null;

    void Awake()
    {
        KopruKlasoru = KopruKlasoruBul();
        acilisZamani = Time.realtimeSinceStartup;
        if (TestModu) enabled = false;
    }

    void OnEnable()
    {
#if UNITY_EDITOR
        AssemblyReloadEvents.beforeAssemblyReload += Durdur;
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        AssemblyReloadEvents.beforeAssemblyReload -= Durdur;
#endif
        Durdur();
        KapanisiTamamla();
    }

    public void KameraAyarla(string yeniTelefonUrl, bool yeniOnizleme)
    {
        if (TestModu) return;
        bool kaynakDegisti = telefonUrl != yeniTelefonUrl;
        bool onizlemeDegisti = onizleme != yeniOnizleme;
        if (kaynakDegisti)
        {
            // DroidCam kapanmış bağlantıyı 10–15 sn meşgul tutabilir.
            YenidenBaglanmaSuresiniAyarla();
            Durdur();
        }
        telefonUrl = yeniTelefonUrl; onizleme = yeniOnizleme;
        if (isteniyor && !kaynakDegisti)
        {
            if (Harici || (surec != null && !surec.HasExited))
            {
                if (onizlemeDegisti) Komut("onizleme", onizleme ? 1 : 0);
            }
            return;
        }
        isteniyor = true;
        Hata = null;
        // Alıcının elle açılmış bir köprüden en az bir mesaj görmesine zaman tanı.
        baslatZamani = Mathf.Max(Time.realtimeSinceStartup + .4f, acilisZamani + 1.2f, sonDurdurmaZamani + yenidenBaglanmaBeklemesi);
        yenidenBaglaniyor |= sonDurdurmaZamani + yenidenBaglanmaBeklemesi > Time.realtimeSinceStartup;
        baslatBekliyor = true;
    }

    void YenidenBaglanmaSuresiniAyarla()
    {
        // Arama, laptopu seçmeden önce telefonu da denemiş ve bırakmış olabilir.
        float bekleme = !string.IsNullOrEmpty(telefonUrl) ? 15f : 1.2f;
        // Art arda tıklamalar, hâlâ kapanmakta olan gerçek kaynağın beklemesini kısaltmasın.
        yenidenBaglanmaBeklemesi = yenidenBaglaniyor ||
            Time.realtimeSinceStartup < sonDurdurmaZamani + yenidenBaglanmaBeklemesi ?
            Mathf.Max(yenidenBaglanmaBeklemesi, bekleme) : bekleme;
    }

    public void YenidenDene()
    {
        if (TestModu || Harici) return;
        YenidenBaglanmaSuresiniAyarla();
        Durdur();
        Hata = null;
        isteniyor = true;
        baslatBekliyor = true;
        baslatZamani = Mathf.Max(Time.realtimeSinceStartup + .4f, sonDurdurmaZamani + yenidenBaglanmaBeklemesi);
        yenidenBaglaniyor |= sonDurdurmaZamani + yenidenBaglanmaBeklemesi > Time.realtimeSinceStartup;
    }

    public void YenidenAra()
    {
        if (TestModu) return;
        if (Harici || (surec != null && !surec.HasExited)) Komut("yeniden_ara");
        else if (!Baslatiliyor) YenidenDene();
    }

    void Update()
    {
        if (TestModu) return;
        if (kapananSurec != null)
        {
            if (kapananSurec.HasExited)
            {
                kapananSurec.Dispose(); kapananSurec = null;
                sonDurdurmaZamani = Time.realtimeSinceStartup;
                baslatZamani = Mathf.Max(baslatZamani, sonDurdurmaZamani + yenidenBaglanmaBeklemesi);
            }
            else if (Time.realtimeSinceStartup >= oldurZamani)
            {
                try { kapananSurec.Kill(); }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception e) { UnityEngine.Debug.LogWarning("Köprü kapatılamadı: " + e.Message); }
                oldurZamani = Time.realtimeSinceStartup + 1f;
            }
            return; // Eski süreç tamamen kapanmadan yenisi açılmaz.
        }
        if (surec != null)
        {
            if (!surec.HasExited && !mesajGeldi && Time.realtimeSinceStartup >= sonrakiAyar)
            {
                // İlk komut import/açılış sırasında port bağlanmadan gönderilmiş olabilir.
                Komut("onizleme", onizleme ? 1 : 0);
                sonrakiAyar = Time.realtimeSinceStartup + .5f;
            }
            if (!surec.HasExited && alici != null && alici.SonMesajNo > baslangicMesajNo && alici.SonMesajYasi < 1f)
            { mesajGeldi = true; Hata = null; yenidenBaglaniyor = false; }
            if (surec.HasExited && stdoutBitti && stderrBitti)
            {
                string sebep;
                lock (kilit) sebep = sonStderr;
                if (sebep != null && sebep.StartsWith("Kamera açılamadı: ", StringComparison.Ordinal))
                    sebep = sebep.Substring("Kamera açılamadı: ".Length);
                int kod = surec.ExitCode;
                Hata = kod == 0 ? "Köprü kapatıldı" :
                    kod == 1 && sebep != null && (sebep.Contains("ModuleNotFoundError") || sebep.Contains("ImportError")) ?
                    "Kamera köprüsü eksik: oyunu zip'ten yeniden çıkar ya da kurulum.bat'ı çalıştır" :
                    (kod == 2 ? (sebep != null && sebep.StartsWith("Kamera bulunamadı", StringComparison.Ordinal) ? "" : "Kamera bulunamadı: ") : $"Köprü kapandı (kod {kod}): ") +
                    (string.IsNullOrEmpty(sebep) ? "Sebep bildirilmedi" : sebep);
                surec.Dispose(); surec = null;
                YenidenBaglanmaSuresiniAyarla();
                sonDurdurmaZamani = Time.realtimeSinceStartup;
                yenidenBaglaniyor = false;
            }
        }
        if (Harici && (alici == null || alici.SonMesajYasi > 2f)) Harici = false;
        if (isteniyor && surec == null && Hata == null && !Harici && !baslatBekliyor && Time.realtimeSinceStartup >= sonrakiKontrol)
        {
            baslatBekliyor = true;
            baslatZamani = Mathf.Max(baslatZamani, Time.realtimeSinceStartup);
        }
        if (!baslatBekliyor || Time.realtimeSinceStartup < baslatZamani) return;
        baslatBekliyor = false;
        sonrakiKontrol = Time.realtimeSinceStartup + 1f;
        if (!yenidenBaglaniyor && alici != null && alici.SonMesajYasi < 1f)
        {
            Harici = true;
            Hata = null;
            Komut("onizleme", onizleme ? 1 : 0);
            return;
        }
        if (surec != null || !KurulumTamam) return;
        if (alici != null && alici.PortHatasi != null) { Hata = alici.PortHatasi; return; }
        try
        {
            lock (kilit) { satirlar.Clear(); sonStderr = null; }
            var bilgi = new ProcessStartInfo
            {
                FileName = PythonYolu,
                Arguments = Tirnakla(Path.Combine(KopruKlasoru, "hareket_yayini.py")) + " oto" +
                    (string.IsNullOrEmpty(telefonUrl) ? "" : " --telefon " + Tirnakla(telefonUrl)) +
                    " --onizleme " + (onizleme ? "1" : "0") + " --ebeveyn " + KendiPid(),
                UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = KopruKlasoru,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            bilgi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            bilgi.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";
            stdoutBitti = stderrBitti = false;
            surec = new Process { StartInfo = bilgi };
            var baslayan = surec;
            surec.OutputDataReceived += (_, e) => SatirEkle(baslayan, e.Data, false);
            surec.ErrorDataReceived += (_, e) => SatirEkle(baslayan, e.Data, true);
            surec.Start();
            baslangicMesajNo = alici != null ? alici.SonMesajNo : 0;
            surec.BeginOutputReadLine(); surec.BeginErrorReadLine();
            mesajGeldi = false;
            // Açılış sırasında değişmiş ayarlar da yeni komut dinleyicisine ulaşsın.
            Komut("onizleme", onizleme ? 1 : 0);
        }
        catch (Exception e)
        {
            Hata = "Köprü başlatılamadı: " + e.Message;
            Durdur();
        }
    }

    void SatirEkle(Process sahibi, string satir, bool hata)
    {
        if (sahibi != surec) return;
        if (satir == null) { if (hata) stderrBitti = true; else stdoutBitti = true; return; }
        if (string.IsNullOrWhiteSpace(satir)) return;
        lock (kilit)
        {
            satirlar.Enqueue((hata ? "stderr: " : "stdout: ") + satir);
            while (satirlar.Count > 20) satirlar.Dequeue();
            if (hata) sonStderr = satir;
        }
    }

    static int KendiPid() { using (var p = Process.GetCurrentProcess()) return p.Id; }

    // Windows komut satırı: boşluk, tırnak ve sondaki ters eğik çizgiler korunur.
    static string Tirnakla(string s)
    {
        var b = new StringBuilder("\"");
        int ters = 0;
        foreach (char c in s)
        {
            if (c == '\\') { ters++; continue; }
            if (c == '"') b.Append('\\', ters * 2 + 1);
            else b.Append('\\', ters);
            b.Append(c); ters = 0;
        }
        return b.Append('\\', ters * 2).Append('"').ToString();
    }

    public void KalibreEt() { if (!TestModu) Komut("kalibre"); }

    static void Komut(string komut, int? deger = null)
    {
        try
        {
            using (var udp = new UdpClient())
            {
                byte[] veri = Encoding.UTF8.GetBytes("{\"komut\":\"" + komut + "\"" +
                    (deger.HasValue ? ",\"deger\":" + deger.Value : "") + "}");
                udp.Send(veri, veri.Length, new IPEndPoint(IPAddress.Loopback, 5066));
            }
        }
        catch (SocketException) { }
    }

    public void Durdur()
    {
        baslatBekliyor = false;
        isteniyor = false;
        Harici = false;
        if (surec == null) return; // Harici köprüyü kapatma.
        kapananSurec = surec; surec = null;
        yenidenBaglaniyor = true;
        oldurZamani = Time.realtimeSinceStartup + 3.5f;
        try
        {
            if (!kapananSurec.HasExited) Komut("cik");
        }
        catch (InvalidOperationException)
        {
            kapananSurec.Dispose(); kapananSurec = null;
            yenidenBaglaniyor = false;
        }
    }

    // Yalnız kapanış/domain yenilemesi: menü tıklaması buraya girmez.
    void KapanisiTamamla()
    {
        if (kapananSurec == null) return;
        try
        {
            if (!kapananSurec.WaitForExit(250)) kapananSurec.Kill();
            kapananSurec.WaitForExit(250);
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception e) { UnityEngine.Debug.LogWarning("Köprü kapatılamadı: " + e.Message); }
        finally { kapananSurec.Dispose(); kapananSurec = null; }
    }

    void OnApplicationQuit() { Durdur(); KapanisiTamamla(); }
    void OnDestroy() { Durdur(); KapanisiTamamla(); }
}
