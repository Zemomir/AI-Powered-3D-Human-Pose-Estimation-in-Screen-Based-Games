using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DefaultExecutionOrder(-50)]
public class AnaMenu : MonoBehaviour
{
    public Motor motor;
    public HareketAlici alici;
    public KopruYonetici kopru;
    public Arayuz arayuz;
    public Ses ses;
    public bool HudGizli => !atlandi && (ekran == Ekran.Menu || ekran == Ekran.MenuAyarlar || ekran == Ekran.Kalibre || ekran == Ekran.Duraklat || ekran == Ekran.DuraklatAyarlar);

    enum Ekran { Menu, MenuAyarlar, Kalibre, Oyun, Duraklat, DuraklatAyarlar, Bitis }
    Ekran ekran;
    bool atlandi, onizleme, kalibrasyonGoruldu;
    int kalite;
    string adres;
    float kalibreSon;
    int kalibreMesajNo;
    bool yenidenAraBekleniyor;
    float yenidenAraSon;
    int yenidenAraMesajNo;
    Font font;
    Sprite yuvarlak;
    Texture2D yuvarlakDoku;
    GameObject canvasGo, menu, ayarlarEkrani, hazirlik, duraklat, bitis;
    Text durum, kameraDurumu, nokta, sayim, aciklama, hazirlikAlt, hizEtiket, donusEtiket, rekor, secimBilgisi;
    Button basla;
    Button geriAyarlar;
    Button[] kaliteler;
    Slider hiz, donus;
    Toggle sesAnahtari;
    InputField ip;
    OyunKaydi kayit;
    readonly Color vurgu = new Color(1f, .72f, .05f);
    readonly Color panel = new Color(.035f, .065f, .13f, .95f);
    readonly Color dugme = new Color(.11f, .17f, .26f, 1f);

    void Awake()
    {
        atlandi = KopruYonetici.MenuAtlanir;
        if (atlandi) { motor.BaslamaIzni = true; enabled = false; return; }
        motor.BaslamaIzni = false;
    }

    void Start()
    {
        if (atlandi) return;
        kayit = GetComponent<OyunKaydi>();
        adres = PlayerPrefs.GetString("TelefonAdresi", "192.168.1.144:4747");
        onizleme = PlayerPrefs.GetInt("KameraOnizlemeAcik", 1) != 0;
        kalite = Mathf.Clamp(PlayerPrefs.GetInt("GrafikKalitesi", 0), 0, 2);
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        SpriteHazirla();
        Kur();
        Goster(Ekran.Menu);
        KameraUygula();
    }

    void Kur()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("MenuOlaylari", typeof(EventSystem), typeof(StandaloneInputModule));
        canvasGo = new GameObject("AnaMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;

        menu = Katman("Ana menu", true);
        var alan = Alan(menu.transform, 1760, 1000);
        Yazi(alan, "Fizyo-Oyun", 110, 100, 1000, 100, 72, FontStyle.Bold);
        Yazi(alan, "G\u00f6vdenle y\u00f6n ver, elini kald\u0131r, birlikte yola \u00e7\u0131k.", 114, 205, 1100, 52, 28);
        basla = Dugme(alan, "Ba\u015fla", 120, 330, 540, 100, Baslat, true);
        Dugme(alan, "Ayarlar", 120, 455, 540, 82, AyarlariMenuAc);
        Dugme(alan, "\u00c7\u0131k\u0131\u015f", 120, 560, 540, 82, Cikis);
        nokta = Yazi(alan, "\u25cf", 122, 710, 44, 50, 29, FontStyle.Bold);
        durum = Yazi(alan, "K\u00f6pr\u00fc ba\u015fl\u0131yor\u2026", 172, 704, 800, 58, 25);
        durum.supportRichText = false;
        rekor = Yazi(alan, "", 122, 775, 800, 58, 26);

        ayarlarEkrani = Katman("Ayarlar", true);
        var ayarAlan = Alan(ayarlarEkrani.transform, 1760, 1000);
        Yazi(ayarAlan, "Ayarlar", 50, 28, 1100, 76, 52, FontStyle.Bold);
        geriAyarlar = Dugme(ayarAlan, "Geri", 1450, 28, 250, 70, AyarlardanDon);
        var kamera = Kutu(ayarAlan, "Kamera", 40, 125, 800, 820);
        var oyun = Kutu(ayarAlan, "Oyun", 880, 125, 840, 820);
        Yazi(kamera, "KAMERA", 30, 18, 730, 46, 27, FontStyle.Bold);
        kameraDurumu = Yazi(kamera, "Kamera aran\u0131yor\u2026", 30, 75, 735, 105, 31, FontStyle.Bold);
        kameraDurumu.supportRichText = false;
        kameraDurumu.resizeTextForBestFit = true;
        kameraDurumu.resizeTextMinSize = 22;
        kameraDurumu.resizeTextMaxSize = 31;
        secimBilgisi = Yazi(kamera, "", 30, 184, 735, 42, 21);
        secimBilgisi.supportRichText = false;
        Dugme(kamera, "Yeniden ara", 30, 246, 330, 68, YenidenAra);
        var onizlemeAnahtari = Anahtar(kamera, "Kamera g\u00f6r\u00fcnt\u00fcs\u00fc (k\u00f6\u015fede)", 30, 345, 735, onizleme);
        onizlemeAnahtari.onValueChanged.AddListener(v =>
        {
            onizleme = v;
            PlayerPrefs.SetInt("KameraOnizlemeAcik", onizleme ? 1 : 0);
            PlayerPrefs.Save();
        });
        Yazi(kamera, "Telefon adresi (iste\u011fe ba\u011fl\u0131)", 30, 445, 735, 42, 23);
        ip = Girdi(kamera, adres, 30, 500, 430, 58);
        ip.onEndEdit.AddListener(s => { adres = s.Trim(); KameraUygula(); });
        Yazi(kamera, "Laptop bulunamazsa kullan\u0131l\u0131r.", 480, 500, 280, 58, 19);

        Yazi(oyun, "OYUN", 30, 18, 780, 42, 27, FontStyle.Bold);
        hizEtiket = Yazi(oyun, "", 30, 75, 780, 42, 25, FontStyle.Bold);
        hiz = Kaydirici(oyun, 30, 125, 775, .3f, 2f, PlayerPrefs.GetFloat("HizHassasiyeti", Motor.VARSAYILAN_HIZ_HASSASIYETI));
        hiz.onValueChanged.AddListener(v => motor.HizHassasiyetiAyarla(v));
        donusEtiket = Yazi(oyun, "", 30, 185, 780, 42, 25, FontStyle.Bold);
        donus = Kaydirici(oyun, 30, 235, 775, .3f, 2f, PlayerPrefs.GetFloat("DonusHassasiyeti", Motor.VARSAYILAN_DONUS_HASSASIYETI));
        donus.onValueChanged.AddListener(v => motor.DonusHassasiyetiAyarla(v));
        sesAnahtari = Anahtar(oyun, "Ses a\u00e7\u0131k", 30, 305, 775, ses.SesAcik);
        sesAnahtari.onValueChanged.AddListener(v => ses.SesAyarla(v));
        Yazi(oyun, "Grafik kalitesi", 30, 400, 270, 50, 25);
        kaliteler = new Button[3];
        string[] adlar = { "Y\u00fcksek", "Orta", "D\u00fc\u015f\u00fck" };
        for (int i = 0; i < 3; i++)
        {
            int n = i;
            kaliteler[i] = Dugme(oyun, adlar[i], 310 + i * 164, 395, 154, 58,
                () => { kalite = n; Grafik.Ayarla(n); SecimleriGuncelle(); });
        }

        hazirlik = Katman("Hazırlık", true);
        var hazirAlan = Alan(hazirlik.transform, 1760, 1000);
        aciklama = Yazi(hazirAlan, "", 0, 0, 1760, 100, 38, FontStyle.Bold, TextAnchor.MiddleCenter);
        sayim = Yazi(hazirAlan, "", 0, 115, 1760, 90, 80, FontStyle.Bold, TextAnchor.MiddleCenter);
        hazirlikAlt = Yazi(hazirAlan, "", 0, 800, 1760, 55, 27, FontStyle.Normal, TextAnchor.MiddleCenter);
        Dugme(hazirAlan, "Ana men\u00fc", 630, 880, 500, 88, AnaMenuyeDon);

        duraklat = Katman("Duraklatma", true);
        var durakAlan = Alan(duraklat.transform, 800, 900);
        var durakKutu = Kutu(durakAlan, "Duraklat\u0131ld\u0131", 0, 0, 800, 900);
        Yazi(durakKutu, "K\u0131sa bir mola", 60, 45, 680, 100, 52, FontStyle.Bold, TextAnchor.MiddleCenter);
        Dugme(durakKutu, "Devam", 100, 190, 600, 82, Devam, true);
        Dugme(durakKutu, "Ba\u015ftan", 100, 300, 600, 82, Baslat);
        Dugme(durakKutu, "Ayarlar", 100, 410, 600, 82, AyarlariDuraklatmadanAc);
        Dugme(durakKutu, "Ana men\u00fc", 100, 520, 600, 82, AnaMenuyeDon);
        Dugme(durakKutu, "\u00c7\u0131k\u0131\u015f", 100, 630, 600, 82, Cikis);

        bitis = Katman("Bitis dugmeleri", false);
        var bitisAlan = Alan(bitis.transform, 1760, 1000);
        Dugme(bitisAlan, "Ana men\u00fc", 400, 865, 420, 85, AnaMenuyeDon);
        Dugme(bitisAlan, "Tekrar (Enter)", 900, 865, 460, 85, Baslat, true);
    }
    bool KaynakOku(out string kaynak)
    {
        kaynak = "";
        if (string.IsNullOrWhiteSpace(adres)) return true;
        // Serbest URL yerine menüde gösterilen IP:port sözleşmesini uygula.
        if (!Uri.TryCreate("http://" + adres + "/video", UriKind.Absolute, out Uri uri) ||
            string.IsNullOrEmpty(uri.Host) || uri.AbsolutePath != "/video" ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) return false;
        kaynak = uri.AbsoluteUri;
        return true;
    }

    void KameraUygula()
    {
        PlayerPrefs.SetString("TelefonAdresi", adres);
        PlayerPrefs.Save();
        if (KaynakOku(out string kaynak)) kopru.KameraAyarla(kaynak, false);
        else kopru.Durdur();
        SecimleriGuncelle();
    }

    void SecimleriGuncelle()
    {
        for (int i = 0; i < kaliteler.Length; i++) Renkle(kaliteler[i], kalite == i);
    }

    void Renkle(Button b, bool secili)
    {
        b.GetComponent<Image>().color = secili ? vurgu : dugme;
        b.GetComponentInChildren<Text>().color = secili ? panel : Color.white;
    }

    void Update()
    {
        if (atlandi || menu == null) return;
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (ekran == Ekran.Duraklat) Devam();
            else if (ekran == Ekran.MenuAyarlar || ekran == Ekran.DuraklatAyarlar) AyarlardanDon();
            else if (ekran == Ekran.Oyun) Goster(Ekran.Duraklat);
            else if (ekran == Ekran.Kalibre) AnaMenuyeDon();
        }
        if (ekran == Ekran.Bitis && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))) Baslat();
        var secili = EventSystem.current?.currentSelectedGameObject;
        bool yaziyor = secili != null && secili.GetComponent<InputField>()?.isFocused == true;
        if (!yaziyor && Input.GetKeyDown(KeyCode.R) &&
            (ekran == Ekran.Oyun || ekran == Ekran.Bitis || ekran == Ekran.Duraklat || ekran == Ekran.Kalibre))
        { motor.Sifirla(); HazirligaGec(); }
        else if (!yaziyor && Input.GetKeyDown(KeyCode.C) && ekran == Ekran.Oyun && motor.Hal != Motor.Durum.Bitti)
            HazirligaGec();
        if (ekran == Ekran.Kalibre)
        {
            if (alici.SonMesajNo > kalibreMesajNo && alici.Bagli &&
                (alici.Durum == "kalibrasyon" || !alici.Kalibre)) kalibrasyonGoruldu = true;
            if (kalibrasyonGoruldu && alici.Bagli && alici.Durum == "hazir")
            { motor.PythonKalibrasyonuylaBaslat(); Goster(Ekran.Oyun); }
            // UDP komutu kaybolmuşsa hazır durumdaki köprüye yeniden gönder.
            else if (!kalibrasyonGoruldu && Time.realtimeSinceStartup - kalibreSon > 2f)
            { kopru.KalibreEt(); kalibreSon = Time.realtimeSinceStartup; }
        }
        if (ekran == Ekran.Oyun && motor.Hal == Motor.Durum.Bitti) Goster(Ekran.Bitis);
    }

    void LateUpdate()
    {
        if (atlandi || menu == null) return;
        hiz.SetValueWithoutNotify(motor.HizHassasiyeti);
        donus.SetValueWithoutNotify(motor.DonusHassasiyeti);
        sesAnahtari.SetIsOnWithoutNotify(ses.SesAcik);
        hizEtiket.text = $"Hız hassasiyeti   %{motor.HizHassasiyeti * 100:0}   (+ / −)";
        donusEtiket.text = $"Dönüş hassasiyeti   %{motor.DonusHassasiyeti * 100:0}   (1 / 2)";
        rekor.text = ses.EnIyiSure > 0 ? "En iyi s\u00fcre: " + SureYazi(ses.EnIyiSure) : "En iyi s\u00fcre: \u2014";
        durum.text = KameraDurumYazisi();
        kameraDurumu.text = KameraDurumYazisi();
        secimBilgisi.text = alici.Bagli && alici.Durum != "araniyor" && alici.Kaynak != null ?
            alici.Kaynak + (alici.Aci.HasValue ? " · " + alici.Aci.Value + "°" : "") : "";
        bool hazir = alici.Bagli && alici.Durum == "hazir";
        nokta.color = kopru.Hata != null || !kopru.KurulumTamam ? new Color(1f, .35f, .3f) : hazir ? new Color(.35f, .9f, .6f) : vurgu;
        basla.interactable = !AramaOnayiBekleniyor() && kopru.KurulumTamam && kopru.Hazir && KaynakOku(out _) && kopru.Hata == null && alici.PortHatasi == null;
        if (ekran == Ekran.Kalibre)
        {
            bool olcuyor = kalibrasyonGoruldu && alici.Bagli && alici.Durum == "kalibrasyon";
            aciklama.text = olcuyor ? "Kıpırdama" : "Kameranın önüne geç — omuzların ve kalçaların görünsün";
            sayim.text = olcuyor && alici.KalibrasyonKalan.HasValue ?
                Mathf.CeilToInt(Mathf.Max(0f, alici.KalibrasyonKalan.Value)).ToString() :
                "<size=28>" + Guvenli(DurumYazisi()) + "</size>";
            hazirlikAlt.text = olcuyor && alici.KalibrasyonDeneme > 1 ? "Biraz kıpırdadın, yeniden ölçüyorum" : "";
        }
    }

    string DurumYazisi()
    {
        if (KopruYonetici.MenuTestModu) return "Test modu: köprü kapalı";
        if (kopru.Hata != null) return kopru.Hata;
        if (kopru.YenidenBaglaniyor) return "Kamera aranıyor…";
        if (kopru.KopruKlasoru == null) return "Köprü klasörü bulunamadı. Oyunu köprüyle birlikte yerleştir veya -kopru <yol> kullan.";
        if (!kopru.KurulumTamam) return "Kurulum gerekli: " + System.IO.Path.Combine(kopru.KopruKlasoru, "kurulum.bat") + " dosyasını çalıştır";
        if (!KaynakOku(out _)) return "Telefon adresini IP:port olarak yaz (ör. 192.168.1.144:4747).";
        if (alici.PortHatasi != null) return alici.PortHatasi;
        string ek = kopru.Harici ? " · harici köprü" : "";
        if (!alici.Bagli) return (kopru.Baslatiliyor ? "Köprü başlıyor…" : "Kameradan mesaj bekleniyor…") + ek;
        switch (alici.Durum)
        {
            case "araniyor": return "Kamera aranıyor…" + ek;
            case "hazir": return "Hazır" + ek;
            case "kalibrasyon": return "Nötr duruş ayarlanıyor; kıpırdama…" + ek;
            case "kisi_yok": return "Kişi görünmüyor" + ek;
            case "kalca_yok": return "Kalçaların görünmüyor: kameradan biraz uzaklaş" + ek;
            case "donuk": return "Kameradan kare gelmiyor; bağlantıyı kontrol et" + ek;
            default: return "Köprü durum bilgisi bekleniyor; hareket_yayini.py güncel olmalı" + ek;
        }
    }

    string KameraDurumYazisi()
    {
        if (kopru.Hata != null || !kopru.KurulumTamam || alici.PortHatasi != null ||
            !KaynakOku(out _) || KopruYonetici.MenuTestModu) return DurumYazisi();
        if (kopru.YenidenBaglaniyor || !alici.Bagli || alici.Durum == "araniyor") return "Kamera aranıyor…";
        if (alici.Kaynak == "telefon") return "Telefon kamerası bulundu";
        if (alici.Kaynak != null && alici.Kaynak.StartsWith("laptop:", StringComparison.Ordinal)) return "Laptop kamerası bulundu";
        return DurumYazisi();
    }

    void YenidenAra()
    {
        yenidenAraBekleniyor = true;
        yenidenAraSon = Time.realtimeSinceStartup + 1f;
        yenidenAraMesajNo = alici.SonMesajNo;
        basla.interactable = false;
        kopru.YenidenAra();
    }

    bool AramaOnayiBekleniyor()
    {
        if (yenidenAraBekleniyor && (Time.realtimeSinceStartup >= yenidenAraSon ||
            (alici.SonMesajNo > yenidenAraMesajNo && alici.Bagli && alici.Durum == "araniyor")))
            yenidenAraBekleniyor = false;
        return yenidenAraBekleniyor;
    }

    void Baslat()
    {
        if (AramaOnayiBekleniyor()) return;
        if (!kopru.KurulumTamam || !kopru.Hazir || kopru.Hata != null || alici.PortHatasi != null || !KaynakOku(out _))
        { AnaMenuyeDon(); return; }
        motor.Sifirla();
        HazirligaGec();
    }

    void HazirligaGec()
    {
        motor.PythonKalibrasyonunuBekle();
        kalibrasyonGoruldu = false;
        kalibreMesajNo = alici.SonMesajNo;
        kalibreSon = Time.realtimeSinceStartup;
        kopru.KalibreEt();
        Goster(Ekran.Kalibre);
    }

    public void AnaMenuyeDon()
    {
        motor.BaslamaIzni = false;
        Goster(Ekran.Menu);
        motor.Sifirla();
        KameraUygula();
    }

    Ekran moladanOnce;
    void Devam() { Goster(moladanOnce); }

    bool ayarlarMoladan;
    void AyarlariMenuAc()
    {
        ayarlarMoladan = false;
        Goster(Ekran.MenuAyarlar);
    }

    void AyarlariDuraklatmadanAc()
    {
        ayarlarMoladan = true;
        Goster(Ekran.DuraklatAyarlar);
    }

    void AyarlardanDon()
    {
        Goster(ayarlarMoladan ? Ekran.Duraklat : Ekran.Menu);
    }

    void Goster(Ekran yeni)
    {
        if (yeni == Ekran.Duraklat && ekran != Ekran.DuraklatAyarlar) moladanOnce = ekran;
        ekran = yeni;
        bool mola = yeni == Ekran.Kalibre || yeni == Ekran.Duraklat || yeni == Ekran.DuraklatAyarlar;
        // Gerçek saatle ölçülen ayara mola süresi karışmasın.
        if (!mola && !motor.enabled && motor.Hal == Motor.Durum.Ayarlaniyor) motor.YerindeAyarla();
        Time.timeScale = mola ? 0f : 1f;
        motor.enabled = !mola;
        // Update, zaman ölçeği sıfırken de çağrılır; molada çak/yıldız sayılmasın.
        if (arayuz != null && arayuz.seyirciler != null) arayuz.seyirciler.enabled = !mola;
        if (arayuz != null && arayuz.yildizlar != null) arayuz.yildizlar.enabled = !mola;
        // Duran bileşenlerin son kare olaylarını CSV'ye tekrar tekrar yazma.
        if (kayit != null) kayit.enabled = !mola;
        ses.Kis(mola);
        menu.SetActive(yeni == Ekran.Menu);
        ayarlarEkrani.SetActive(yeni == Ekran.MenuAyarlar || yeni == Ekran.DuraklatAyarlar);
        hazirlik.SetActive(yeni == Ekran.Kalibre);
        KameraOnizleme.BuyukGoster(yeni == Ekran.Kalibre);
        duraklat.SetActive(yeni == Ekran.Duraklat);
        bitis.SetActive(yeni == Ekran.Bitis);
        Cursor.visible = yeni != Ekran.Oyun;
        Cursor.lockState = CursorLockMode.None;
        EventSystem.current?.SetSelectedGameObject(null);
    }

    static void Cikis() { Application.Quit(); }
    static string SureYazi(float sn) { int d = Mathf.RoundToInt(sn * 10); return $"{d / 600:00}:{d % 600 / 10:00}.{d % 10}"; }
    static string Guvenli(string s) => s.Replace("<", "‹").Replace(">", "›");

    GameObject Katman(string ad, bool karart)
    {
        var g = new GameObject(ad, typeof(RectTransform));
        g.transform.SetParent(canvasGo.transform, false);
        Esnet(g.GetComponent<RectTransform>());
        if (karart)
        {
            var im = g.AddComponent<Image>();
            im.color = new Color(.01f, .025f, .055f, .48f);
        }
        return g;
    }

    RectTransform Alan(Transform parent, float w, float h)
    {
        var r = YeniRect(parent, "İçerik");
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
        r.sizeDelta = new Vector2(w, h);
        return r;
    }

    RectTransform Kutu(Transform parent, string ad, float x, float y, float w, float h)
    {
        var r = Konum(parent, ad, x, y, w, h);
        var im = r.gameObject.AddComponent<Image>();
        im.sprite = yuvarlak; im.type = Image.Type.Sliced; im.color = panel;
        return r;
    }

    Text Yazi(Transform parent, string metin, float x, float y, float w, float h, int boyut,
        FontStyle stil = FontStyle.Normal, TextAnchor hizalama = TextAnchor.MiddleLeft)
    {
        var r = Konum(parent, "Metin", x, y, w, h);
        var t = r.gameObject.AddComponent<Text>();
        t.font = font; t.text = metin; t.fontSize = boyut; t.fontStyle = stil;
        t.color = Color.white; t.alignment = hizalama;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false;
        return t;
    }

    Button Dugme(Transform parent, string metin, float x, float y, float w, float h, UnityEngine.Events.UnityAction tikla, bool vurgulu = false)
    {
        var r = Kutu(parent, metin, x, y, w, h);
        r.GetComponent<Image>().color = vurgulu ? vurgu : dugme;
        var b = r.gameObject.AddComponent<Button>();
        b.targetGraphic = r.GetComponent<Image>();
        var renkler = b.colors;
        renkler.highlightedColor = new Color(1f, .9f, .7f);
        renkler.pressedColor = new Color(.75f, .75f, .75f);
        renkler.selectedColor = new Color(1f, .9f, .7f);
        renkler.disabledColor = new Color(.45f, .45f, .45f, .6f);
        b.colors = renkler;
        b.onClick.AddListener(tikla);
        var t = Yazi(r, metin, 12, 0, w - 24, h, vurgulu ? 36 : 27, FontStyle.Bold, TextAnchor.MiddleCenter);
        t.color = vurgulu ? panel : Color.white;
        return b;
    }

    Slider Kaydirici(Transform parent, float x, float y, float w, float min, float max, float deger)
    {
        var r = Konum(parent, "Kaydırıcı", x, y, w, 40);
        var s = r.gameObject.AddComponent<Slider>();
        var arka = Kutu(r, "Ray", 0, 14, w, 12);
        arka.GetComponent<Image>().color = dugme;
        var dolguAlan = Konum(r, "Dolgu alanı", 18, 14, w - 36, 12);
        var dolgu = Kutu(dolguAlan, "Dolgu", 0, 0, w - 36, 12);
        dolgu.sizeDelta = Vector2.zero;
        dolgu.GetComponent<Image>().color = vurgu;
        var tutAlan = Konum(r, "Tutamak alanı", 18, 0, w - 36, 40);
        var tut = Kutu(tutAlan, "Tutamak", 0, 0, 36, 40);
        tut.GetComponent<Image>().color = Color.white;
        tut.pivot = new Vector2(.5f, .5f);
        tut.sizeDelta = new Vector2(36, 0);
        tut.anchoredPosition = Vector2.zero;
        s.fillRect = dolgu; s.handleRect = tut; s.targetGraphic = tut.GetComponent<Image>();
        s.minValue = min; s.maxValue = max;
        s.value = float.IsFinite(deger) ? deger : min;
        return s;
    }

    Toggle Anahtar(Transform parent, string etiket, float x, float y, float w, bool acik)
    {
        var r = Konum(parent, etiket, x, y, w, 56);
        var t = r.gameObject.AddComponent<Toggle>();
        var arka = Kutu(r, "Anahtar", 0, 4, 54, 48);
        arka.GetComponent<Image>().color = dugme;
        var isaret = Kutu(arka, "Açık", 9, 9, 36, 30);
        isaret.GetComponent<Image>().color = vurgu;
        t.targetGraphic = arka.GetComponent<Image>(); t.graphic = isaret.GetComponent<Image>();
        t.isOn = acik;
        Yazi(r, etiket, 76, 0, w - 80, 56, 27);
        // Yazı alanına tıklamak da anahtarı değiştirir.
        var hit = r.gameObject.AddComponent<Image>(); hit.color = Color.clear;
        return t;
    }

    InputField Girdi(Transform parent, string metin, float x, float y, float w, float h)
    {
        var r = Kutu(parent, "Telefon adresi", x, y, w, h);
        r.GetComponent<Image>().color = dugme;
        var giris = r.gameObject.AddComponent<InputField>();
        giris.targetGraphic = r.GetComponent<Image>();
        giris.textComponent = Yazi(r, "", 18, 0, w - 36, h, 27);
        giris.textComponent.supportRichText = false;
        giris.characterLimit = 100;
        giris.lineType = InputField.LineType.SingleLine;
        giris.text = metin;
        return giris;
    }

    static RectTransform YeniRect(Transform parent, string ad)
    {
        var r = new GameObject(ad, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false); return r;
    }

    static RectTransform Konum(Transform parent, string ad, float x, float y, float w, float h)
    {
        var r = YeniRect(parent, ad);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h);
        return r;
    }

    static void Esnet(RectTransform r)
    { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

    void SpriteHazirla()
    {
        const int boyut = 64;
        const float yaricap = 16f;
        yuvarlakDoku = new Texture2D(boyut, boyut, TextureFormat.RGBA32, false);
        yuvarlakDoku.wrapMode = TextureWrapMode.Clamp;
        yuvarlakDoku.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < boyut; y++)
            for (int x = 0; x < boyut; x++)
            {
                float dx = Mathf.Max(0, Mathf.Abs(x - 31.5f) - (32f - yaricap));
                float dy = Mathf.Max(0, Mathf.Abs(y - 31.5f) - (32f - yaricap));
                float a = Mathf.Clamp01(yaricap - Mathf.Sqrt(dx * dx + dy * dy));
                yuvarlakDoku.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        yuvarlakDoku.Apply();
        yuvarlak = Sprite.Create(yuvarlakDoku, new Rect(0, 0, boyut, boyut), new Vector2(.5f, .5f), 100,
            0, SpriteMeshType.FullRect, new Vector4(18, 18, 18, 18));
    }

    void OnDestroy()
    {
        if (atlandi) return;
        Time.timeScale = 1f;
        KameraOnizleme.BuyukGoster(false);
        if (ses != null) ses.Kis(false);
        if (canvasGo != null) Destroy(canvasGo);
        if (yuvarlak != null) Destroy(yuvarlak);
        if (yuvarlakDoku != null) Destroy(yuvarlakDoku);
    }
}
