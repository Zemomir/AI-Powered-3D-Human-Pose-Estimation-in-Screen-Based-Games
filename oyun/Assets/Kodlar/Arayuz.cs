using UnityEngine;
using UnityEngine.UI;

// Oyuncu ekranı sade kalır; fizyoterapist ayrıntıları H ile açar.
public class Arayuz : MonoBehaviour
{
    public HareketAlici alici;
    public Motor motor;
    public Yildizlar yildizlar;
    public Seyirciler seyirciler;
    public Ses ses;
    public AnaMenu anaMenu;

    const int HUD_ORDER = 50;
    readonly Color vurgu = new Color(1f, .72f, .05f);
    readonly Color panelRengi = new Color(.035f, .065f, .13f, .88f);
    readonly Color panelSoluk = new Color(.035f, .065f, .13f, .78f);
    Font font;
    Sprite yuvarlak;
    Texture2D yuvarlakDoku;
    GameObject canvasGo, hizPanel, detayPanel, bitisPanel;
    Text yildizMetni, cakMetni, sureMetni, hizMetni, durumMetni, detayMetni, yardimMetni;
    Text bitisBaslik, bitisSure, bitisYildiz, bitisTaraflar, bitisCak, bitisRekor, bitisTekrar;
    Image hizDolgu;
    bool ayrintiGoster;
    bool motorTakipte;
    Motor.Durum oncekiHal;
    float baslaSon = -1f;

    void Awake()
    {
        ayrintiGoster = PlayerPrefs.GetInt("AyrintiGoster", 0) != 0;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        SpriteHazirla();
        ArayuzKur();
    }

    void OnEnable()
    {
        MotoraBaglan();
    }

    void Start() { MotoraBaglan(); }
    void OnDisable()
    {
        if (motorTakipte && motor != null) motor.Sifirlandi -= Sifirla;
        motorTakipte = false;
    }
    void MotoraBaglan()
    {
        if (motor == null || motorTakipte) return;
        motor.Sifirlandi += Sifirla;
        oncekiHal = motor.Hal;
        motorTakipte = true;
    }
    void Sifirla() { baslaSon = -1f; oncekiHal = Motor.Durum.Bekliyor; }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            ayrintiGoster = !ayrintiGoster;
            PlayerPrefs.SetInt("AyrintiGoster", ayrintiGoster ? 1 : 0);
            PlayerPrefs.Save();
            if (durumMetni != null)
            {
                var r = durumMetni.rectTransform;
                r.anchoredPosition = ayrintiGoster ? new Vector2(330, 5) : new Vector2(0, 5);
                r.sizeDelta = new Vector2(ayrintiGoster ? 1180 : 1680, 300);
            }
        }
        if (canvasGo == null || motor == null) return;
        bool gorunur = anaMenu == null || !anaMenu.HudGizli;
        if (canvasGo.activeSelf != gorunur) canvasGo.SetActive(gorunur);
        if (!gorunur) return;

        Metin(yildizMetni, $"★  { (yildizlar != null ? yildizlar.Toplanan : 0)} / { (yildizlar != null ? yildizlar.Toplam : 0)}");
        Metin(cakMetni, $"Çak  { (seyirciler != null ? seyirciler.SolCak + seyirciler.SagCak : 0)} / { (seyirciler != null ? seyirciler.SolToplam + seyirciler.SagToplam : 0)}");
        Metin(sureMetni, SureYazi(motor.Sure));
        Metin(hizMetni, $"{motor.Hiz * 3.6f:0} km/sa");
        float oran = Mathf.Clamp01(motor.Hiz / Mathf.Max(.01f, motor.enCokHiz));
        hizDolgu.rectTransform.anchorMax = new Vector2(oran, 1f);

        detayPanel.SetActive(ayrintiGoster);
        Metin(yardimMetni, ayrintiGoster
            ? "R: baştan   K: klavye   + / -: hız   1 / 2: dönüş   Y: yumuşatma   C: sıfırla   H: ayrıntılar   M: ses"
            : "H: ayrıntılar");
        if (ayrintiGoster) Metin(detayMetni, AyrintiYazisi());

        bool bitti = motor.Hal == Motor.Durum.Bitti;
        bitisPanel.SetActive(bitti);
        if (bitti) BitisMetinleriniGuncelle();
        else
        {
            string mesaj = DurumMesaji();
            if (mesaj == null && Time.unscaledTime < baslaSon) mesaj = "Başla!";
            Metin(durumMetni, mesaj ?? "");
        }
    }

    void LateUpdate()
    {
        if (motor == null) return;
        if (oncekiHal != Motor.Durum.Suruyor && motor.Hal == Motor.Durum.Suruyor && motor.Sure < .1f)
            baslaSon = Time.unscaledTime + 1f;
        oncekiHal = motor.Hal;
    }

    void ArayuzKur()
    {
        canvasGo = new GameObject("OyunHUDCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = HUD_ORDER;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;

        var root = canvasGo.transform;
        Kutu(root, "Yıldız hapı", 24, 18, 350, 76, panelRengi);
        yildizMetni = Yazi(root, "", 42, 18, 315, 76, 36, FontStyle.Bold, TextAnchor.MiddleLeft);
        Kutu(root, "Çak hapı", 400, 18, 370, 76, panelRengi);
        cakMetni = Yazi(root, "", 420, 18, 330, 76, 36, FontStyle.Bold, TextAnchor.MiddleCenter);
        Kutu(root, "Süre hapı", -374, 18, 350, 76, panelRengi, true);
        sureMetni = Yazi(root, "", -352, 18, 306, 76, 36, FontStyle.Bold, TextAnchor.MiddleRight, true);

        durumMetni = Yazi(root, "", 0, 5, 1180, 300, 58, FontStyle.Bold, TextAnchor.MiddleCenter, false, true);
        detayPanel = Kutu(root, "Ayrıntı paneli", 24, 112, 590, 520, panelSoluk).gameObject;
        detayPanel.GetComponent<Image>().raycastTarget = false;
        detayMetni = Yazi(detayPanel.transform, "", 24, 20, 542, 480, 23, FontStyle.Normal, TextAnchor.UpperLeft);

        hizPanel = Kutu(root, "Hız çubuğu", 0, 118, 480, 18, new Color(.01f, .02f, .04f, .64f), false, false, true).gameObject;
        var dolgu = Kutu(hizPanel.transform, "Hız dolgusu", 0, 0, 0, 18, vurgu);
        var dr = dolgu.GetComponent<RectTransform>();
        dr.anchorMin = new Vector2(0, 0); dr.anchorMax = new Vector2(0, 1);
        dr.pivot = new Vector2(0, .5f); dr.offsetMin = Vector2.zero; dr.offsetMax = Vector2.zero;
        hizDolgu = dolgu.GetComponent<Image>();
        hizMetni = Yazi(root, "", 0, 58, 420, 54, 30, FontStyle.Bold, TextAnchor.MiddleCenter, false, false, true);

        yardimMetni = Yazi(root, "", 24, 20, 1872, 48, 21, FontStyle.Normal, TextAnchor.MiddleCenter, false, false, true);
        bitisPanel = Kutu(root, "Bitiş paneli", 0, 0, 1050, 650, new Color(.035f, .065f, .13f, .94f), false, true).gameObject;
        bitisPanel.SetActive(false);
        bitisBaslik = Yazi(bitisPanel.transform, "Bitti!", 0, 25, 1000, 90, 58, FontStyle.Bold, TextAnchor.MiddleCenter);
        bitisSure = Yazi(bitisPanel.transform, "", 30, 125, 940, 54, 32, FontStyle.Normal, TextAnchor.MiddleCenter);
        bitisYildiz = Yazi(bitisPanel.transform, "", 30, 186, 940, 54, 32, FontStyle.Normal, TextAnchor.MiddleCenter);
        bitisTaraflar = Yazi(bitisPanel.transform, "", 25, 248, 950, 56, 29, FontStyle.Normal, TextAnchor.MiddleCenter);
        bitisCak = Yazi(bitisPanel.transform, "", 25, 312, 950, 54, 29, FontStyle.Normal, TextAnchor.MiddleCenter);
        bitisRekor = Yazi(bitisPanel.transform, "", 25, 372, 950, 54, 31, FontStyle.Bold, TextAnchor.MiddleCenter);
        bitisTekrar = Yazi(bitisPanel.transform, "R ile tekrar", 25, 440, 950, 54, 27, FontStyle.Normal, TextAnchor.MiddleCenter);
    }

    string AyrintiYazisi()
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
        return sol;
    }

    string DurumMesaji()
    {
        if (motor.kopru != null && motor.kopru.Hata != null) return "Kamera köprüsü kapandı: Esc → Ana menü";
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

    void BitisMetinleriniGuncelle()
    {
        Metin(durumMetni, "");
        Metin(bitisSure, "Süre: " + SureYazi(motor.Sure));
        Metin(bitisYildiz, $"★ { (yildizlar != null ? yildizlar.Toplanan : 0)} / { (yildizlar != null ? yildizlar.Toplam : 0)}");
        string taraflar = yildizlar != null ? $"Sol {yildizlar.SolToplanan}/{yildizlar.SolToplam} · Orta {yildizlar.OrtaToplanan}/{yildizlar.OrtaToplam} · Sağ {yildizlar.SagToplanan}/{yildizlar.SagToplam}" : "Sol 0/0 · Orta 0/0 · Sağ 0/0";
        Metin(bitisTaraflar, taraflar);
        string caklar = seyirciler != null ? $"Çak — Sol {seyirciler.SolCak}/{seyirciler.SolToplam} · Sağ {seyirciler.SagCak}/{seyirciler.SagToplam}" : "Çak — Sol 0/0 · Sağ 0/0";
        Metin(bitisCak, caklar);
        string rekor = ses == null || ses.EnIyiSure <= 0 ? "En iyi süre: —" : ses.YeniRekor ? "Yeni rekor!" : "En iyi süre: " + SureYazi(ses.EnIyiSure);
        Metin(bitisRekor, rekor);
    }

    RectTransform Kutu(Transform parent, string ad, float x, float y, float w, float h, Color renk,
        bool sagaYasla = false, bool merkez = false, bool altMerkez = false)
    {
        var r = new GameObject(ad, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        r.SetParent(parent, false);
        var anchor = merkez ? new Vector2(.5f, .5f) : altMerkez ? new Vector2(.5f, 0) : sagaYasla ? new Vector2(1, 1) : new Vector2(0, 1);
        r.anchorMin = r.anchorMax = r.pivot = anchor;
        r.anchoredPosition = merkez || altMerkez ? new Vector2(x, y) : new Vector2(sagaYasla ? -x : x, -y);
        r.sizeDelta = new Vector2(w, h);
        var im = r.GetComponent<Image>(); im.sprite = yuvarlak; im.type = Image.Type.Sliced; im.color = renk; im.raycastTarget = false;
        return r;
    }

    Text Yazi(Transform parent, string value, float x, float y, float w, float h, int boyut,
        FontStyle stil, TextAnchor hizalama, bool sagaYasla = false, bool merkez = false, bool altMerkez = false)
    {
        var r = new GameObject("Metin", typeof(RectTransform), typeof(Text), typeof(Shadow)).GetComponent<RectTransform>();
        r.SetParent(parent, false);
        var anchor = merkez ? new Vector2(.5f, .5f) : altMerkez ? new Vector2(.5f, 0) : sagaYasla ? new Vector2(1, 1) : new Vector2(0, 1);
        r.anchorMin = r.anchorMax = r.pivot = anchor;
        r.anchoredPosition = merkez || altMerkez ? new Vector2(x, y) : new Vector2(sagaYasla ? -x : x, -y);
        r.sizeDelta = new Vector2(w, h);
        var t = r.GetComponent<Text>();
        t.font = font; t.text = value; t.fontSize = boyut; t.fontStyle = stil; t.color = Color.white; t.alignment = hizalama;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
        t.supportRichText = false; t.raycastTarget = false;
        var shadow = r.GetComponent<Shadow>(); shadow.effectColor = new Color(0, 0, 0, .72f); shadow.effectDistance = new Vector2(2, -2);
        return t;
    }

    void SpriteHazirla()
    {
        const int boyut = 64; const float yaricap = 16f;
        yuvarlakDoku = new Texture2D(boyut, boyut, TextureFormat.RGBA32, false);
        yuvarlakDoku.wrapMode = TextureWrapMode.Clamp; yuvarlakDoku.filterMode = FilterMode.Bilinear;
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

    static void Metin(Text hedef, string deger) { if (hedef != null && hedef.text != deger) hedef.text = deger; }
    static string ElYazi(float? el) => el.HasValue ? el.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : "null";
    static string SureYazi(float sn)
    {
        int ondalik = Mathf.RoundToInt(sn * 10);
        return $"{ondalik / 600:00}:{ondalik % 600 / 10:00}.{ondalik % 10}";
    }

    void OnDestroy()
    {
        if (canvasGo != null) Destroy(canvasGo);
        if (yuvarlak != null) Destroy(yuvarlak);
        if (yuvarlakDoku != null) Destroy(yuvarlakDoku);
    }
}
