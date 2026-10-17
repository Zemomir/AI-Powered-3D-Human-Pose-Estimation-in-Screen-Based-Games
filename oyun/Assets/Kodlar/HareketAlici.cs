using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

// Python tarafından (hareket_yayini.py) UDP ile gelen gövde hareketini dinler.
// Mesaj: {"t": 12.3, "kalibre": true, "egim": 8.2, "one": 0.031, "tut": false}  (egim/one null olabilir)
// ZAMAN_ASIMI_SN boyunca mesaj gelmezse "bağlı değil" sayılır: Python çökerse "sinyal yok"
// bile gönderemez, oyun son eğimde takılı kalmasın diye bu kural oyunda da var.
public class HareketAlici : MonoBehaviour
{
    public int port = 5065;
    public const float ZAMAN_ASIMI_SN = 0.5f;

    // Klavye modu: kamerasız deneme için (K tuşu). Oklar/WASD eğimi ve öne-geriyi taklit eder.
    public bool KlavyeModu { get; private set; }

    public bool Bagli => KlavyeModu || (sonMesajZamani > 0 && Time.realtimeSinceStartup - sonMesajZamani < ZAMAN_ASIMI_SN);
    public bool Kalibre => KlavyeModu || (Bagli && kalibre);
    public float? Egim => KlavyeModu ? klavyeEgim : (Bagli ? egim : null);
    public float? One => KlavyeModu ? klavyeOne : (Bagli ? one : null);
    public float? SolEl => KlavyeModu ? (Input.GetKey(KeyCode.Q) ? 1f : -1.5f) : (Bagli && !donuk ? solEl : null);
    public float? SagEl => KlavyeModu ? (Input.GetKey(KeyCode.E) ? 1f : -1.5f) : (Bagli && !donuk ? sagEl : null);
    public bool GoruntuDonuk => !KlavyeModu && Bagli && donuk;  // Python çalışıyor ama telefondan kare gelmiyor
    public bool HizTutuluyor => !KlavyeModu && Bagli && kalibre && !donuk && egim.HasValue && one.HasValue && tut;
    public string PortHatasi { get; private set; }               // ekranda gerçek sebep görünsün
    public string Durum { get; private set; }
    public string Kaynak { get; private set; }
    public int? Aci { get; private set; }
    public float SonMesajYasi => sonMesajZamani < 0 ? float.PositiveInfinity : Time.realtimeSinceStartup - sonMesajZamani;
    public int SonMesajNo { get; private set; }

    UdpClient istemci;
    Thread dinleyici;
    readonly object kilit = new object();
    string bekleyenMesaj;
    float sonMesajZamani = -1f;
    bool kalibre, donuk, tut;
    float? egim, one, solEl, sagEl;
    float klavyeEgim, klavyeOne;

    void OnEnable()
    {
        PortHatasi = null;
        try
        {
            istemci = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
        }
        catch (SocketException e)
        {
            PortHatasi = $"Port {port} dolu: oyun iki kez açılmış olabilir";
            Debug.LogError($"UDP {port} açılamadı: {e.Message}");
            return;
        }
        var yerel = istemci;  // iş parçacığı başlamadan OnDisable gelse de null görmesin
        dinleyici = new Thread(() => Dinle(yerel)) { IsBackground = true, Name = "HareketAlici" };
        dinleyici.Start();
    }

    void Dinle(UdpClient c)  // OnDisable alanı null'lar; iş parçacığı kendi kopyasıyla çalışır
    {
        var uzak = new IPEndPoint(IPAddress.Any, 0);
        while (true)
        {
            byte[] veri;
            try
            {
                veri = c.Receive(ref uzak);
            }
            catch (ObjectDisposedException) { return; }
            catch (SocketException) { if (istemci != c) return; continue; }  // kapatıldı ya da yenisi açıldı
            lock (kilit) bekleyenMesaj = Encoding.UTF8.GetString(veri);
        }
    }

    void Update()
    {
        string mesaj;
        lock (kilit) { mesaj = bekleyenMesaj; bekleyenMesaj = null; }
        if (mesaj != null && Oku(mesaj, out bool k, out float? e, out float? o, out bool d, out float? sol, out float? sag, out bool h))
        {
            kalibre = k; egim = e; one = o; donuk = d;
            solEl = sol; sagEl = sag;
            tut = h;
            Durum = Deger(mesaj, "durum", out string durum) && durum != "null" ? durum.Trim('"') : null;
            Kaynak = Deger(mesaj, "kaynak", out string kaynak) && kaynak != "null" ? kaynak.Trim('"') : null;
            Aci = Deger(mesaj, "aci", out string aci) && int.TryParse(aci, out int a) &&
                (a == 0 || a == 90 || a == 180 || a == 270) ? a : (int?)null;
            SonMesajNo++;
            sonMesajZamani = Time.realtimeSinceStartup;
        }

        if (Input.GetKeyDown(KeyCode.K)) KlavyeModu = !KlavyeModu;
        if (KlavyeModu)
        {
            float yatay = Input.GetAxisRaw("Horizontal");  // oklar / A-D
            float dikey = Input.GetAxisRaw("Vertical");    // oklar / W-S
            klavyeEgim = Mathf.MoveTowards(klavyeEgim, yatay * 20f, 60f * Time.deltaTime);
            klavyeOne = Mathf.MoveTowards(klavyeOne, dikey * 0.04f, 0.12f * Time.deltaTime);
        }
    }

    void OnDisable()
    {
        var i = istemci;
        istemci = null;
        i?.Close();  // Receive'i ObjectDisposedException/SocketException ile bitirir
        dinleyici?.Join(500);
    }

    // Küçük ve bilinen biçimde bir mesaj: JsonUtility null'ı 0 diye okurdu (0 = dik dur demek),
    // bu yüzden "değer yok" ile "sıfır" ayrı kalsın diye elle okunuyor.
    static bool Oku(string json, out bool kalibre, out float? egim, out float? one, out bool donuk, out float? solEl, out float? sagEl, out bool tut)
    {
        kalibre = false; egim = null; one = null; solEl = null; sagEl = null;
        donuk = Deger(json, "donuk", out string dn) && dn == "true";
        tut = Deger(json, "tut", out string h) && h == "true";
        if (!Deger(json, "kalibre", out string k)) return false;
        kalibre = k == "true";
        if (Deger(json, "egim", out string e)) egim = Sayi(e);
        if (Deger(json, "one", out string o)) one = Sayi(o);
        if (Deger(json, "sol_el", out string sol)) solEl = Sayi(sol);
        if (Deger(json, "sag_el", out string sag)) sagEl = Sayi(sag);
        return true;
    }

    // NaN/Infinity da "değer yok" sayılır: motorun konumuna NaN girerse oyun bozulur.
    static float? Sayi(string s) =>
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) && float.IsFinite(f) ? f : (float?)null;

    static bool Deger(string json, string anahtar, out string deger)
    {
        deger = null;
        int i = json.IndexOf("\"" + anahtar + "\"", StringComparison.Ordinal);
        if (i < 0) return false;
        i = json.IndexOf(':', i);
        if (i < 0) return false;
        int bas = i + 1;
        while (bas < json.Length && json[bas] == ' ') bas++;
        int son = bas;
        while (son < json.Length && json[son] != ',' && json[son] != '}') son++;
        deger = json.Substring(bas, son - bas).Trim();
        return true;
    }
}
