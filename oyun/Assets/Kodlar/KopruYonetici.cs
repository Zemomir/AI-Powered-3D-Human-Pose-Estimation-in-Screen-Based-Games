using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

// Yalnız kendi başlattığımız sürecin yaşam döngüsünü yönetiriz.
public class KopruYonetici : MonoBehaviour
{
    public HareketAlici alici;
    public string KopruKlasoru { get; private set; }
    public string PythonYolu => KopruKlasoru == null ? null : Path.Combine(KopruKlasoru, ".venv", "Scripts", "python.exe");
    public bool KurulumTamam => PythonYolu != null && File.Exists(PythonYolu);
    public bool Harici { get; private set; }
    public string Hata { get; private set; }
    public bool Baslatiliyor => baslatBekliyor || (surec != null && !mesajGeldi);
    public string[] SonSatirlar { get { lock (kilit) return satirlar.ToArray(); } }

    Process surec;
    readonly object kilit = new object();
    readonly Queue<string> satirlar = new Queue<string>();
    string sonStderr, kaynak = "0";
    int aci;
    bool onizleme, baslatBekliyor, mesajGeldi;
    float baslatZamani, acilisZamani;
    float sonDurdurmaZamani = -10f;

    public static bool TestModu
    {
        get
        {
            var a = Environment.GetCommandLineArgs();
            return Array.IndexOf(a, "-otopilot") >= 0 || Array.IndexOf(a, "-ekran") >= 0;
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

    public void KameraAyarla(string yeniKaynak, int yeniAci, bool yeniOnizleme)
    {
        if (TestModu) return;
        if (surec != null && !surec.HasExited && kaynak == yeniKaynak && aci == yeniAci && onizleme == yeniOnizleme) return;
        kaynak = yeniKaynak; aci = yeniAci; onizleme = yeniOnizleme;
        Durdur();
        Hata = null;
        Harici = false;
        // Alıcının elle açılmış bir köprüden en az bir mesaj görmesine zaman tanı.
        baslatZamani = Mathf.Max(Time.realtimeSinceStartup + .4f, acilisZamani + 1.2f, sonDurdurmaZamani + 1.2f);
        baslatBekliyor = true;
    }

    void Update()
    {
        if (TestModu) return;
        if (surec != null)
        {
            if (alici != null && alici.SonMesajYasi < 1f) mesajGeldi = true;
            if (surec.HasExited)
            {
                surec.WaitForExit(); // Asenkron çıktı okuyucularının son satırı tamamlaması.
                string sebep;
                lock (kilit) sebep = sonStderr;
                if (sebep != null && sebep.StartsWith("Kamera açılamadı: ", StringComparison.Ordinal))
                    sebep = sebep.Substring("Kamera açılamadı: ".Length);
                Hata = "Kamera açılamadı: " + (string.IsNullOrEmpty(sebep) ? "Köprü beklenmedik biçimde kapandı" : sebep) +
                    $" (çıkış kodu {surec.ExitCode})";
                surec.Dispose(); surec = null;
            }
        }
        if (!baslatBekliyor || Time.realtimeSinceStartup < baslatZamani) return;
        baslatBekliyor = false;
        if (alici != null && alici.SonMesajYasi < 1f)
        {
            Harici = true;
            return;
        }
        if (!KurulumTamam) return;
        if (alici != null && alici.PortHatasi != null) { Hata = alici.PortHatasi; return; }
        try
        {
            lock (kilit) { satirlar.Clear(); sonStderr = null; }
            var bilgi = new ProcessStartInfo
            {
                FileName = PythonYolu,
                Arguments = Tirnakla(Path.Combine(KopruKlasoru, "hareket_yayini.py")) + " " + Tirnakla(kaynak) +
                    " " + aci + " --onizleme " + (onizleme ? "1" : "0") + " --ebeveyn " + KendiPid(),
                UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = KopruKlasoru,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            bilgi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            bilgi.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";
            surec = new Process { StartInfo = bilgi };
            surec.OutputDataReceived += (_, e) => SatirEkle(e.Data, false);
            surec.ErrorDataReceived += (_, e) => SatirEkle(e.Data, true);
            surec.Start();
            surec.BeginOutputReadLine(); surec.BeginErrorReadLine();
            mesajGeldi = false;
        }
        catch (Exception e)
        {
            Hata = "Kamera açılamadı: " + e.Message;
            Durdur();
        }
    }

    void SatirEkle(string satir, bool hata)
    {
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

    static void Komut(string komut)
    {
        try
        {
            using (var udp = new UdpClient())
            {
                byte[] veri = Encoding.UTF8.GetBytes("{\"komut\":\"" + komut + "\"}");
                udp.Send(veri, veri.Length, new IPEndPoint(IPAddress.Loopback, 5066));
            }
        }
        catch (SocketException) { }
    }

    public void Durdur()
    {
        baslatBekliyor = false;
        if (surec == null) return; // Harici köprüyü kapatma.
        try
        {
            if (!surec.HasExited)
            {
                Komut("cik");
                if (!surec.WaitForExit(1000)) surec.Kill();
            }
            surec.WaitForExit(1000);
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception e) { UnityEngine.Debug.LogWarning("Köprü kapatılamadı: " + e.Message); }
        finally
        {
            surec.Dispose(); surec = null;
            // Kapanıştaki son UDP mesajı, yeniden başlatmada harici köprü sanılmasın.
            sonDurdurmaZamani = Time.realtimeSinceStartup;
        }
    }

    void OnApplicationQuit() { Durdur(); }
    void OnDestroy() { Durdur(); }
}
