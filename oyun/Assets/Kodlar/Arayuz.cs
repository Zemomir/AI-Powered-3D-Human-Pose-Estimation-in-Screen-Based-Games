using UnityEngine;

// Oyuncu ekranı sade kalır; fizyoterapist ayrıntıları H ile açar.
public class Arayuz : MonoBehaviour
{
    public HareketAlici alici;
    public Motor motor;
    public Yildizlar yildizlar;
    public Seyirciler seyirciler;
    public Ses ses;
    public AnaMenu anaMenu;

    GUIStyle kucuk, buyuk, sayac, sag, orta;
    Texture2D doku;
    bool ayrintiGoster;
    Motor.Durum oncekiHal;
    float baslaSon = -1f;

    void Awake() { ayrintiGoster = PlayerPrefs.GetInt("AyrintiGoster", 0) != 0; }
    void OnEnable()
    {
        if (motor == null) return;
        motor.Sifirlandi += Sifirla;
        oncekiHal = motor.Hal;
    }
    void OnDisable() { if (motor != null) motor.Sifirlandi -= Sifirla; }
    void Sifirla() { baslaSon = -1f; oncekiHal = Motor.Durum.Bekliyor; }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.H)) return;
        ayrintiGoster = !ayrintiGoster;
        PlayerPrefs.SetInt("AyrintiGoster", ayrintiGoster ? 1 : 0);
        PlayerPrefs.Save();
    }

    void LateUpdate()
    {
        if (motor == null) return;
        if (oncekiHal != Motor.Durum.Suruyor && motor.Hal == Motor.Durum.Suruyor && motor.Sure < 0.1f)
            baslaSon = Time.unscaledTime + 1f;
        oncekiHal = motor.Hal;
    }

    void StilleriHazirla()
    {
        if (kucuk != null) return;
        kucuk = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
        buyuk = new GUIStyle(GUI.skin.label) { fontSize = 46, alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold, wordWrap = true };
        sayac = new GUIStyle(GUI.skin.label) { fontSize = 38, fontStyle = FontStyle.Bold };
        sag = new GUIStyle(sayac) { alignment = TextAnchor.UpperRight };
        orta = new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        doku = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        doku.SetPixel(0, 0, Color.white);
        doku.Apply();
    }

    void OnGUI()
    {
        if (anaMenu != null && anaMenu.HudGizli) return;
        if (motor == null) return;
        StilleriHazirla();
        // Dar pencerelerde de bütün öğeler görünür; oran korunur.
        float olcek = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        if (olcek <= 0) return;
        Matrix4x4 eskiMatris = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280f * olcek) / 2,
            (Screen.height - 720f * olcek) / 2, 0), Quaternion.identity, Vector3.one * olcek);
        Golgeli(new Rect(24, 18, 600, 60), $"★ {(yildizlar != null ? yildizlar.Toplanan : 0)} / {(yildizlar != null ? yildizlar.Toplam : 0)}", sayac);
        Golgeli(new Rect(300, 18, 530, 60), $"Çak {(seyirciler != null ? seyirciler.SolCak + seyirciler.SagCak : 0)}/{(seyirciler != null ? seyirciler.SolToplam + seyirciler.SagToplam : 0)}", sayac);
        Golgeli(new Rect(850, 18, 406, 60), SureYazi(motor.Sure), sag);

        Golgeli(new Rect(480, 610, 320, 44), $"{motor.Hiz * 3.6f:0} km/sa", orta);
        Kutu(new Rect(480, 658, 320, 14), new Color(0, 0, 0, 0.5f));
        float oran = Mathf.Clamp01(motor.Hiz / Mathf.Max(0.01f, motor.enCokHiz));
        Kutu(new Rect(483, 661, 314 * oran, 8), new Color(0.95f, 0.78f, 0.3f));

        if (ayrintiGoster)
        {
            Ayrintilar();
            Golgeli(new Rect(24, 690, 1232, 28), "R: baştan   K: klavye   + / -: hız   1 / 2: dönüş   Y: yumuşatma   C: sıfırla   H: ayrıntılar   M: ses", kucuk);
        }
        else Golgeli(new Rect(24, 686, 300, 30), "H: ayrıntılar", kucuk);

        if (motor.Hal == Motor.Durum.Bitti) BitisPaneli();
        else
        {
            string mesaj = DurumMesaji();
            if (mesaj == null && Time.unscaledTime < baslaSon) mesaj = "Başla!";
            if (mesaj != null)
                Golgeli(new Rect(ayrintiGoster ? 450 : 0, 220, ayrintiGoster ? 810 : 1280, 300), mesaj, buyuk);
        }
        GUI.matrix = eskiMatris;
    }

    void Ayrintilar()
    {
        string kaynak = alici == null ? "Kamera bağlı değil" : alici.KlavyeModu ? "Klavye modu (K: kapat)" : (alici.Bagli ? "Kamera bağlı" : "Kamera bağlı değil");
        float ilerleme = motor.yol != null && motor.yol.Noktalar != null && motor.yol.Noktalar.Count > 1 ? motor.Ilerleme : 0;
        string sol = $"{kaynak}\nHız: {motor.Hiz * 3.6f:0} km/sa\nSüre: {SureYazi(motor.Sure)}\nYol: %{ilerleme * 100:0}\nHız hassasiyeti: %{motor.HizHassasiyeti * 100:0}\nDönüş hassasiyeti: %{motor.DonusHassasiyeti * 100:0}\nYumuşatma: {(motor.YumusatmaAcik ? "açık" : "kapalı")}";
        if (motor.DuzeltilmisEgim.HasValue)
            sol += $"\nEğim: {motor.DuzeltilmisEgim.Value:+0;-0;0}°" + (motor.DuzeltilmisOne.HasValue ? $"   Öne: {motor.DuzeltilmisOne.Value * 100:+0.0;-0.0;0.0} cm" : "");
        if (alici != null && !alici.KlavyeModu)
            sol += $"\nSıfır: öne {motor.OneSifir * 100:+0.0;-0.0;0.0} cm, eğim {motor.EgimSifir:+0;-0;0}°";
        if (alici != null && alici.HizTutuluyor) sol += "\nHız tutuluyor (el)";
        if (ses != null) sol += $"\nSes: {(ses.SesAcik ? "açık" : "kapalı")}";
        sol += $"\nsol_el: {ElYazi(alici != null ? alici.SolEl : null)} · {(seyirciler != null && seyirciler.SolKalkik ? "kalkık" : "indi")}";
        sol += $"\nsag_el: {ElYazi(alici != null ? alici.SagEl : null)} · {(seyirciler != null && seyirciler.SagKalkik ? "kalkık" : "indi")}";
        Kutu(new Rect(20, 92, 420, 390), new Color(0.06f, 0.09f, 0.09f, 0.78f));
        Golgeli(new Rect(32, 102, 396, 370), sol, kucuk);
    }

    string DurumMesaji()
    {
        if (alici == null) return "Kamera bağlı değil\nhareket_yayini.py çalışıyor mu?";
        if (alici.PortHatasi != null && !alici.KlavyeModu) return alici.PortHatasi;
        if (!alici.Bagli) return "Kamera bağlı değil\nhareket_yayini.py çalışıyor mu?";
        if (alici.GoruntuDonuk) return "Telefondan görüntü gelmiyor\nDroidCam açık mı?";
        if (!alici.Kalibre) return "Oynayacağın gibi dur\nayarlanıyor...";
        if (!alici.Egim.HasValue) return "Kamera seni göremiyor\nomuzların ve kalçaların görünsün";
        if (motor.Hal == Motor.Durum.Ayarlaniyor) return motor.AyarOlcuyor ? "Ölçülüyor... kıpırdama" : $"Oynayacağın gibi dur, kıpırdama\n{Mathf.CeilToInt(motor.AyarKalanSn)}";
        if (motor.YolDisinda) return "Yola dön";
        return null;
    }

    void BitisPaneli()
    {
        float x = ayrintiGoster ? 470 : 290;
        const float genislik = 700;
        Kutu(new Rect(x, 120, genislik, 430), new Color(0.04f, 0.07f, 0.10f, 0.88f));
        Golgeli(new Rect(x, 135, genislik, 75), "Bitti!", buyuk);
        Golgeli(new Rect(x + 20, 220, genislik - 40, 45), "Süre: " + SureYazi(motor.Sure), orta);
        Golgeli(new Rect(x + 20, 275, genislik - 40, 45), $"★ {(yildizlar != null ? yildizlar.Toplanan : 0)} / {(yildizlar != null ? yildizlar.Toplam : 0)}", orta);
        string taraflar = yildizlar != null ? $"Sol {yildizlar.SolToplanan}/{yildizlar.SolToplam} · Orta {yildizlar.OrtaToplanan}/{yildizlar.OrtaToplam} · Sağ {yildizlar.SagToplanan}/{yildizlar.SagToplam}" : "Sol 0/0 · Orta 0/0 · Sağ 0/0";
        Golgeli(new Rect(x + 20, 330, genislik - 40, 50), taraflar, orta);
        string caklar = seyirciler != null ? $"Çak — Sol {seyirciler.SolCak}/{seyirciler.SolToplam} · Sağ {seyirciler.SagCak}/{seyirciler.SagToplam}" : "Çak — Sol 0/0 · Sağ 0/0";
        Golgeli(new Rect(x + 20, 380, genislik - 40, 45), caklar, orta);
        string rekor = ses == null || ses.EnIyiSure <= 0 ? "En iyi süre: —" : ses.YeniRekor ? "Yeni rekor!" : "En iyi süre: " + SureYazi(ses.EnIyiSure);
        Golgeli(new Rect(x + 20, 425, genislik - 40, 45), rekor, orta);
        Golgeli(new Rect(x + 20, 475, genislik - 40, 45), "R ile tekrar", orta);
    }

    void Kutu(Rect r, Color renk)
    {
        Color eski = GUI.color;
        GUI.color = renk;
        GUI.DrawTexture(r, doku);
        GUI.color = eski;
    }

    static string ElYazi(float? el) => el.HasValue ? el.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : "null";

    static string SureYazi(float sn)
    {
        int ondalik = Mathf.RoundToInt(sn * 10);  // Önce yuvarla; 00:60.0 görünmesin.
        return $"{ondalik / 600:00}:{ondalik % 600 / 10:00}.{ondalik % 10}";
    }

    static void Golgeli(Rect r, string metin, GUIStyle stil)
    {
        var renk = stil.normal.textColor;
        stil.normal.textColor = new Color(0, 0, 0, 0.6f);
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), metin, stil);
        stil.normal.textColor = Color.white;
        GUI.Label(r, metin, stil);
        stil.normal.textColor = renk;
    }

    void OnDestroy() { if (doku != null) Destroy(doku); }
}
