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
    public bool HudGizli => !atlandi && (ekran == Ekran.Menu || ekran == Ekran.Yerles || ekran == Ekran.Kalibre || ekran == Ekran.Ayar || ekran == Ekran.Duraklat);

    enum Ekran { Menu, Yerles, Kalibre, Ayar, Oyun, Duraklat, Bitis }
    Ekran ekran;
    bool atlandi, telefon, onizleme, kalibrasyonGoruldu;
    int kameraIndeksi, laptopAci, telefonAci, kalite;
    string adres;
    float yerlesme, yerlesSon, kalibreSon;
    int kalibreMesajNo;
    Font font;
    Sprite yuvarlak;
    Texture2D yuvarlakDoku;
    GameObject canvasGo, menu, hazirlik, duraklat, bitis;
    Text durum, nokta, sayim, aciklama, hizEtiket, donusEtiket, sureEtiket, rekor, secimBilgisi;
    Button basla, laptopDugme, telefonDugme;
    Button[] indeksler, acilar, kaliteler;
    Slider hiz, donus, sure;
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
        telefon = PlayerPrefs.GetInt("KameraTelefon", 0) != 0;
        kameraIndeksi = Mathf.Clamp(PlayerPrefs.GetInt("KameraIndeksi", 0), 0, 2);
        laptopAci = AciOku("LaptopAci", 0);
        telefonAci = AciOku("TelefonAci", 90);
        adres = PlayerPrefs.GetString("TelefonAdresi", "192.168.1.144:4747");
        onizleme = PlayerPrefs.GetInt("KameraOnizleme", 0) != 0;
        yerlesme = PlayerPrefs.GetFloat("YerlesmeSuresi", 8f);
        if (!float.IsFinite(yerlesme)) yerlesme = 8f;
        yerlesme = Mathf.Clamp(Mathf.Round(yerlesme), 3, 15);
        kalite = Mathf.Clamp(PlayerPrefs.GetInt("GrafikKalitesi", 0), 0, 2);
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        SpriteHazirla();
        Kur();
        Goster(Ekran.Menu);
        KameraUygula();
    }

    static int AciOku(string anahtar, int varsayilan)
    {
        int a = PlayerPrefs.GetInt(anahtar, varsayilan);
        return a == 0 || a == 90 || a == 180 || a == 270 ? a : varsayilan;
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

        menu = Katman("Ana menü", true);
        var alan = Alan(menu.transform, 1760, 1000);
        Yazi(alan, "Fizyo-Oyun", 50, 22, 1500, 80, 66, FontStyle.Bold);
        Yazi(alan, "Gövdenle yön ver, elini kaldır, birlikte yola çık.", 55, 108, 1600, 44, 28);
        var sol = Kutu(alan, "Kamera", 40, 180, 810, 550);
        var sag = Kutu(alan, "Ayarlar", 880, 180, 840, 550);
        Yazi(sol, "01   KAMERANI SEÇ", 30, 18, 750, 45, 25, FontStyle.Bold);
        laptopDugme = Dugme(sol, "Laptop kamerası", 30, 78, 360, 76, () => KameraSec(false));
        telefonDugme = Dugme(sol, "Telefon (DroidCam)", 410, 78, 370, 76, () => KameraSec(true));
        Yazi(sol, "Laptop kamera indeksi", 30, 175, 400, 38, 25);
        indeksler = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            int n = i;
            indeksler[i] = Dugme(sol, i.ToString(), 440 + i * 114, 170, 104, 54,
                () => { kameraIndeksi = n; KameraUygula(); });
        }
        Yazi(sol, "Telefon IP:port", 30, 253, 300, 38, 25);
        ip = Girdi(sol, adres, 320, 245, 460, 60);
        ip.onEndEdit.AddListener(s => { adres = s.Trim(); KameraUygula(); });
        Yazi(sol, "Görüntüyü döndür", 30, 335, 300, 38, 25);
        acilar = new Button[4];
        for (int i = 0; i < 4; i++)
        {
            int a = i * 90;
            acilar[i] = Dugme(sol, a + "°", 320 + i * 117, 328, 108, 56, () =>
            {
                if (telefon) telefonAci = a; else laptopAci = a;
                KameraUygula();
            });
        }
        var onizlemeAnahtari = Anahtar(sol, "Kamera görüntüsünü göster", 30, 420, 745, onizleme);
        onizlemeAnahtari.onValueChanged.AddListener(v => { onizleme = v; KameraUygula(); });
        secimBilgisi = Yazi(sol, "", 30, 482, 510, 56, 21);
        Dugme(sol, "Yeniden dene", 556, 485, 224, 48, KameraYenidenDene);

        hizEtiket = Yazi(sag, "", 30, 18, 780, 42, 26, FontStyle.Bold);
        hiz = Kaydirici(sag, 30, 75, 775, .3f, 2f, PlayerPrefs.GetFloat("HizHassasiyeti", Motor.VARSAYILAN_HIZ_HASSASIYETI));
        hiz.onValueChanged.AddListener(v => motor.HizHassasiyetiAyarla(v));
        donusEtiket = Yazi(sag, "", 30, 128, 780, 42, 26, FontStyle.Bold);
        donus = Kaydirici(sag, 30, 185, 775, .3f, 2f, PlayerPrefs.GetFloat("DonusHassasiyeti", Motor.VARSAYILAN_DONUS_HASSASIYETI));
        donus.onValueChanged.AddListener(v => motor.DonusHassasiyetiAyarla(v));
        sureEtiket = Yazi(sag, "", 30, 238, 780, 42, 26, FontStyle.Bold);
        sure = Kaydirici(sag, 30, 295, 775, 3, 15, yerlesme);
        sure.wholeNumbers = true;
        sure.onValueChanged.AddListener(v => { yerlesme = v; PlayerPrefs.SetFloat("YerlesmeSuresi", v); PlayerPrefs.Save(); });
        sesAnahtari = Anahtar(sag, "Ses açık", 30, 360, 775, ses.SesAcik);
        sesAnahtari.onValueChanged.AddListener(v => ses.SesAyarla(v));
        Yazi(sag, "Grafik kalitesi", 30, 438, 270, 50, 25);
        kaliteler = new Button[3];
        string[] adlar = { "Yüksek", "Orta", "Düşük" };
        for (int i = 0; i < 3; i++)
        {
            int n = i;
            kaliteler[i] = Dugme(sag, adlar[i], 310 + i * 164, 435, 154, 58,
                () => { kalite = n; Grafik.Ayarla(n); SecimleriGuncelle(); });
        }

        var durumKutu = Kutu(alan, "Kamera durumu", 40, 752, 1680, 108);
        nokta = Yazi(durumKutu, "●", 24, 24, 45, 50, 32);
        durum = Yazi(durumKutu, "Köprü başlıyor…", 84, 12, 1570, 84, 25);
        durum.supportRichText = false;
        rekor = Yazi(alan, "", 50, 890, 650, 60, 27);
        Dugme(alan, "Çıkış", 1010, 884, 250, 82, Cikis);
        basla = Dugme(alan, "Başla", 1290, 878, 430, 94, Baslat, true);

        hazirlik = Katman("Yerine geçme ve ayar", true);
        var hazirAlan = Alan(hazirlik.transform, 1660, 940);
        Yazi(hazirAlan, "Birazdan yola çıkıyoruz", 30, 100, 1600, 100, 55, FontStyle.Bold, TextAnchor.MiddleCenter);
        aciklama = Yazi(hazirAlan, "", 60, 260, 1540, 190, 38, FontStyle.Normal, TextAnchor.MiddleCenter);
        sayim = Yazi(hazirAlan, "", 130, 480, 1400, 140, 70, FontStyle.Bold, TextAnchor.MiddleCenter);
        Dugme(hazirAlan, "Ana menü", 580, 740, 500, 88, AnaMenuyeDon);

        duraklat = Katman("Duraklatma", true);
        var durakAlan = Alan(duraklat.transform, 800, 850);
        var durakKutu = Kutu(durakAlan, "Duraklatıldı", 0, 0, 800, 850);
        Yazi(durakKutu, "Kısa bir mola", 60, 60, 680, 100, 52, FontStyle.Bold, TextAnchor.MiddleCenter);
        Dugme(durakKutu, "Devam", 100, 220, 600, 90, Devam, true);
        Dugme(durakKutu, "Baştan", 100, 350, 600, 90, Baslat);
        Dugme(durakKutu, "Ana menü", 100, 480, 600, 90, AnaMenuyeDon);
        Dugme(durakKutu, "Çıkış", 100, 610, 600, 90, Cikis);

        bitis = Katman("Bitiş düğmeleri", false);
        var bitisAlan = Alan(bitis.transform, 1760, 1000);
        Dugme(bitisAlan, "Ana menü", 400, 865, 420, 85, AnaMenuyeDon);
        Dugme(bitisAlan, "Tekrar (Enter)", 900, 865, 460, 85, Baslat, true);
    }

    void KameraSec(bool secim) { telefon = secim; KameraUygula(); }

    void KameraYenidenDene() { KameraUygula(); if (KaynakOku(out _)) kopru.YenidenDene(); }

    bool KaynakOku(out string kaynak)
    {
        kaynak = kameraIndeksi.ToString();
        if (!telefon) return true;
        // Serbest URL yerine menüde gösterilen IP:port sözleşmesini uygula.
        if (!Uri.TryCreate("http://" + adres + "/video", UriKind.Absolute, out Uri uri) ||
            string.IsNullOrEmpty(uri.Host) || uri.AbsolutePath != "/video" ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) return false;
        kaynak = uri.AbsoluteUri;
        return true;
    }

    void KameraUygula()
    {
        PlayerPrefs.SetInt("KameraTelefon", telefon ? 1 : 0);
        PlayerPrefs.SetInt("KameraIndeksi", kameraIndeksi);
        PlayerPrefs.SetInt("LaptopAci", laptopAci);
        PlayerPrefs.SetInt("TelefonAci", telefonAci);
        PlayerPrefs.SetString("TelefonAdresi", adres);
        PlayerPrefs.SetInt("KameraOnizleme", onizleme ? 1 : 0);
        PlayerPrefs.Save();
        if (KaynakOku(out string kaynak)) kopru.KameraAyarla(kaynak, telefon ? telefonAci : laptopAci, onizleme);
        else kopru.Durdur();
        SecimleriGuncelle();
    }

    void SecimleriGuncelle()
    {
        Renkle(laptopDugme, !telefon); Renkle(telefonDugme, telefon);
        for (int i = 0; i < indeksler.Length; i++)
        { indeksler[i].interactable = !telefon; Renkle(indeksler[i], kameraIndeksi == i); }
        ip.interactable = telefon;
        for (int i = 0; i < acilar.Length; i++) Renkle(acilar[i], i * 90 == (telefon ? telefonAci : laptopAci));
        for (int i = 0; i < kaliteler.Length; i++) Renkle(kaliteler[i], kalite == i);
        secimBilgisi.text = telefon ? "DroidCam açık ve aynı Wi-Fi ağına bağlı olsun." : "Dahili kamera genellikle 0 numaralı kameradır.";
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
            else if (ekran == Ekran.Oyun || ekran == Ekran.Ayar) Goster(Ekran.Duraklat);
            else if (ekran == Ekran.Yerles || ekran == Ekran.Kalibre) AnaMenuyeDon();
        }
        if (ekran == Ekran.Bitis && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))) Baslat();
        if (ekran == Ekran.Yerles && Time.realtimeSinceStartup >= yerlesSon)
        {
            kalibrasyonGoruldu = false;
            kalibreMesajNo = alici.SonMesajNo;
            kalibreSon = Time.realtimeSinceStartup;
            kopru.KalibreEt();
            Goster(Ekran.Kalibre);
        }
        if (ekran == Ekran.Kalibre)
        {
            if (alici.SonMesajNo > kalibreMesajNo && alici.Bagli &&
                (alici.Durum == "kalibrasyon" || !alici.Kalibre)) kalibrasyonGoruldu = true;
            if (kalibrasyonGoruldu && alici.Bagli && alici.Durum == "hazir")
            { motor.BaslamaIzni = true; Goster(Ekran.Ayar); }
            // UDP komutu kaybolmuşsa hazır durumdaki köprüye yeniden gönder.
            else if (!kalibrasyonGoruldu && Time.realtimeSinceStartup - kalibreSon > 2f)
            { kopru.KalibreEt(); kalibreSon = Time.realtimeSinceStartup; }
        }
        if (ekran == Ekran.Ayar && motor.Hal == Motor.Durum.Suruyor) Goster(Ekran.Oyun);
        if (ekran == Ekran.Oyun && motor.Hal == Motor.Durum.Bitti) Goster(Ekran.Bitis);
        // R mevcut Motor.Sifirla yolunu kullanır; bitiş düğmelerini kaldır.
        if (ekran == Ekran.Bitis && Input.GetKeyDown(KeyCode.R)) Goster(Ekran.Oyun);
    }

    void LateUpdate()
    {
        if (atlandi || menu == null) return;
        hiz.SetValueWithoutNotify(motor.HizHassasiyeti);
        donus.SetValueWithoutNotify(motor.DonusHassasiyeti);
        sesAnahtari.SetIsOnWithoutNotify(ses.SesAcik);
        hizEtiket.text = $"Hız hassasiyeti   %{motor.HizHassasiyeti * 100:0}   (+ / −)";
        donusEtiket.text = $"Dönüş hassasiyeti   %{motor.DonusHassasiyeti * 100:0}   (1 / 2)";
        sureEtiket.text = $"Yerine geçme süresi   {yerlesme:0} sn";
        rekor.text = ses.EnIyiSure > 0 ? "En iyi süre: " + SureYazi(ses.EnIyiSure) : "İlk yolculuğuna hazır mısın?";
        durum.text = DurumYazisi();
        bool hazir = alici.Bagli && alici.Durum == "hazir";
        nokta.color = kopru.Hata != null || !kopru.KurulumTamam ? new Color(1f, .35f, .3f) : hazir ? new Color(.35f, .9f, .6f) : vurgu;
        basla.interactable = kopru.KurulumTamam && kopru.Hazir && KaynakOku(out _) && kopru.Hata == null && alici.PortHatasi == null;
        if (ekran == Ekran.Yerles)
        {
            aciklama.text = "Yerine geç: kameradan 2-3 m uzakta,\nomuzların ve kalçaların görünsün";
            sayim.text = Mathf.CeilToInt(Mathf.Max(0, yerlesSon - Time.realtimeSinceStartup)).ToString();
            if (!hazir) sayim.text += "\n<size=30>" + Guvenli(DurumYazisi()) + "</size>";
        }
        else if (ekran == Ekran.Kalibre)
        {
            aciklama.text = "Oynayacağın gibi dur, kıpırdama.\nNötr duruşun alınıyor…";
            sayim.text = "<size=34>" + Guvenli(DurumYazisi()) + "</size>";
        }
        else if (ekran == Ekran.Ayar)
        {
            aciklama.text = "Oynayacağın gibi dur, kıpırdama";
            sayim.text = motor.Hal == Motor.Durum.Ayarlaniyor ?
                (motor.AyarOlcuyor ? "<size=45>Ölçülüyor…</size>" : Mathf.CeilToInt(motor.AyarKalanSn).ToString()) :
                "<size=34>" + Guvenli(DurumYazisi()) + "</size>";
        }
    }

    string DurumYazisi()
    {
        if (KopruYonetici.MenuTestModu) return "Test modu: köprü kapalı";
        if (kopru.YenidenBaglaniyor) return "Kamera yeniden bağlanıyor…";
        if (kopru.Hata != null) return kopru.Hata;
        if (kopru.KopruKlasoru == null) return "Köprü klasörü bulunamadı. Oyunu köprüyle birlikte yerleştir veya -kopru <yol> kullan.";
        if (!kopru.KurulumTamam) return "Kurulum gerekli: " + System.IO.Path.Combine(kopru.KopruKlasoru, "kurulum.bat") + " dosyasını çalıştır";
        if (!KaynakOku(out _)) return "Telefon adresini IP:port olarak yaz (ör. 192.168.1.144:4747).";
        if (alici.PortHatasi != null) return alici.PortHatasi;
        string ek = kopru.Harici ? " · harici köprü" : "";
        if (!alici.Bagli) return (kopru.Baslatiliyor ? "Köprü başlıyor…" : "Kameradan mesaj bekleniyor…") + ek;
        switch (alici.Durum)
        {
            case "hazir": return "Hazır" + ek;
            case "kalibrasyon": return "Nötr duruş ayarlanıyor; kıpırdama…" + ek;
            case "kisi_yok": return "Kişi görünmüyor" + ek;
            case "kalca_yok": return "Kalçaların görünmüyor: kameradan biraz uzaklaş" + ek;
            case "donuk": return "Kameradan kare gelmiyor; bağlantıyı kontrol et" + ek;
            default: return "Köprü durum bilgisi bekleniyor; hareket_yayini.py güncel olmalı" + ek;
        }
    }

    void Baslat()
    {
        if (!kopru.KurulumTamam || !kopru.Hazir || kopru.Hata != null || alici.PortHatasi != null || !KaynakOku(out _))
        { AnaMenuyeDon(); return; }
        motor.BaslamaIzni = false;
        Goster(Ekran.Yerles);
        motor.Sifirla();
        yerlesSon = Time.realtimeSinceStartup + yerlesme;
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

    void Goster(Ekran yeni)
    {
        if (yeni == Ekran.Duraklat) moladanOnce = ekran;
        ekran = yeni;
        bool mola = yeni == Ekran.Duraklat;
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
        hazirlik.SetActive(yeni == Ekran.Yerles || yeni == Ekran.Kalibre || yeni == Ekran.Ayar);
        duraklat.SetActive(mola);
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
        if (ses != null) ses.Kis(false);
        if (canvasGo != null) Destroy(canvasGo);
        if (yuvarlak != null) Destroy(yuvarlak);
        if (yuvarlakDoku != null) Destroy(yuvarlakDoku);
    }
}
