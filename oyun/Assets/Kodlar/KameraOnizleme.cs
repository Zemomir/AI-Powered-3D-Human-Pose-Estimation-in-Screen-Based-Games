using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

// Görüntü kanalı hareket hesabından ve menünün görünürlüğünden bağımsızdır.
public class KameraOnizleme : MonoBehaviour
{
    public HareketAlici alici;
    const string AYAR = "KameraOnizlemeAcik";
    static KameraOnizleme etkin;
    readonly object kilit = new object();
    UdpClient istemci;
    Thread dinleyici;
    volatile bool dur;
    byte[] bekleyen;
    long bekleyenZamani, sonKareZamani;
    bool ilkKare, acik;
    float ayarOkuSon;
    GameObject canvasGo, panel;
    Image cerceve;
    RawImage goruntu;
    AspectRatioFitter oran;
    Text altMetin, beklemeMetni;
    Texture2D doku, yuvarlakDoku;
    Sprite yuvarlak;
    Font font;

    void Awake()
    {
        etkin = this;
        acik = PlayerPrefs.GetInt(AYAR, 1) != 0;
        ArayuzKur();
    }

    void OnEnable()
    {
        dur = false;
        try
        {
            istemci = new UdpClient(AddressFamily.InterNetwork);
            istemci.ExclusiveAddressUse = true;
            istemci.Client.Bind(new IPEndPoint(IPAddress.Loopback, 5067));
            istemci.Client.ReceiveTimeout = 200;
        }
        catch (SocketException)
        {
            istemci?.Close();
            istemci = null;
            return; // Port doluysa sessizce devre dışı kal.
        }
        var yerel = istemci;
        dinleyici = new Thread(() => Dinle(yerel)) { IsBackground = true, Name = "KameraOnizleme" };
        dinleyici.Start();
    }

    void Dinle(UdpClient kanal)
    {
        var uzak = new IPEndPoint(IPAddress.Any, 0);
        while (!dur)
        {
            try
            {
                byte[] veri = kanal.Receive(ref uzak);
                if (veri.Length < 4 || veri.Length > 60000 || veri[0] != 0xff || veri[1] != 0xd8)
                    continue;
                lock (kilit)
                {
                    bekleyen = veri; // Kuyruk birikmez: yalnız en yeni kare saklanır.
                    bekleyenZamani = Stopwatch.GetTimestamp();
                }
            }
            catch (ObjectDisposedException) { return; }
            catch (SocketException e)
            {
                if (dur || e.SocketErrorCode != SocketError.TimedOut) return;
            }
        }
    }

    void Update()
    {
        if (Time.realtimeSinceStartup >= ayarOkuSon)
        {
            acik = PlayerPrefs.GetInt(AYAR, 1) != 0;
            ayarOkuSon = Time.realtimeSinceStartup + .5f;
        }
        byte[] veri;
        long zaman;
        lock (kilit)
        {
            veri = bekleyen; zaman = bekleyenZamani; bekleyen = null;
        }
        if (veri != null)
        {
            try
            {
                // LoadImage aynı nesnenin boyutunu/içeriğini değiştirir; yeni doku yaratılmaz.
                if (doku.LoadImage(veri, false))
                {
                    oran.aspectRatio = (float)doku.width / doku.height;
                    sonKareZamani = zaman;
                    ilkKare = true;
                }
            }
            catch (UnityException) { }
            catch (ArgumentException) { }
        }
        GorunurlukUygula();
        if (!ilkKare) return;

        string durum = alici != null && alici.Bagli ? alici.Durum : "araniyor";
        bool bayat = (Stopwatch.GetTimestamp() - sonKareZamani) / (double)Stopwatch.Frequency > 1.0;
        string metin;
        Color renk;
        switch (durum)
        {
            case "hazir": renk = new Color(.35f, .9f, .6f); metin = "Hazır"; break;
            case "kalibrasyon": renk = new Color(1f, .72f, .05f); metin = "Kıpırdama"; break;
            case "kalca_yok": renk = new Color(1f, .48f, .12f); metin = "Kalçaların görünmüyor, biraz uzaklaş"; break;
            case "kisi_yok": renk = new Color(1f, .35f, .3f); metin = "Seni göremiyorum"; break;
            case "donuk": renk = Color.gray; metin = "Kamera donmuş"; break;
            default: renk = Color.gray; metin = "Kamera aranıyor…"; break;
        }
        cerceve.color = renk;
        altMetin.color = renk;
        altMetin.text = metin;
        goruntu.enabled = !bayat;
        beklemeMetni.gameObject.SetActive(bayat);
        // Çerçeve hareket durumunu korur; eski fotoğraf canlı gibi sunulmaz.
        beklemeMetni.text = bayat && (durum == "hazir" || durum == "kalibrasyon") ? "Kamera donmuş" : metin;
    }

    public static void AcikAyarla(bool deger)
    {
        PlayerPrefs.SetInt(AYAR, deger ? 1 : 0);
        PlayerPrefs.Save();
        if (etkin == null) return;
        etkin.acik = deger;
        etkin.GorunurlukUygula();
    }

    void GorunurlukUygula()
    {
        panel.SetActive(acik && ilkKare && istemci != null && isActiveAndEnabled);
    }

    void ArayuzKur()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        SpriteHazirla();
        doku = new Texture2D(2, 2, TextureFormat.RGB24, false);
        doku.wrapMode = TextureWrapMode.Clamp;
        doku.filterMode = FilterMode.Bilinear;
        canvasGo = new GameObject("Kamera Önizleme", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;

        var kok = Rect(canvasGo.transform, "Panel", 0, 0, 380, 285);
        kok.anchorMin = kok.anchorMax = kok.pivot = new Vector2(1, 0);
        kok.anchoredPosition = new Vector2(-24, 24);
        panel = kok.gameObject;
        cerceve = Kutu(kok);
        Kutu(Rect(kok, "İç", 3, 3, 374, 279)).color = new Color(.035f, .065f, .13f, .88f);
        Yazi(kok, "Kamera", 12, 6, 356, 26, 18).fontStyle = FontStyle.Bold;
        var alan = Rect(kok, "Görüntü alanı", 10, 36, 360, 198);
        var resim = Rect(alan, "Görüntü", 0, 0, 360, 198);
        // FitInParent pivot etrafında küçülür: ortada dursun, sola yapışmasın.
        resim.anchorMin = resim.anchorMax = resim.pivot = new Vector2(.5f, .5f);
        resim.anchoredPosition = Vector2.zero;
        goruntu = resim.gameObject.AddComponent<RawImage>();
        goruntu.texture = doku;
        goruntu.raycastTarget = false;
        oran = resim.gameObject.AddComponent<AspectRatioFitter>();
        oran.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        oran.aspectRatio = 4f / 3f;
        beklemeMetni = Yazi(alan, "", 8, 0, 344, 198, 22);
        altMetin = Yazi(kok, "", 12, 239, 356, 38, 17);
        panel.SetActive(false);
    }

    Image Kutu(RectTransform rect)
    {
        var im = rect.gameObject.AddComponent<Image>();
        im.sprite = yuvarlak;
        im.type = Image.Type.Sliced;
        im.raycastTarget = false;
        return im;
    }

    Text Yazi(Transform parent, string metin, float x, float y, float w, float h, int boyut)
    {
        var t = Rect(parent, "Metin", x, y, w, h).gameObject.AddComponent<Text>();
        t.font = font; t.text = metin; t.fontSize = boyut;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.supportRichText = false;
        t.raycastTarget = false;
        return t;
    }

    static RectTransform Rect(Transform parent, string ad, float x, float y, float w, float h)
    {
        var r = new GameObject(ad, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
        return r;
    }

    void SpriteHazirla()
    {
        const int boyut = 64;
        yuvarlakDoku = new Texture2D(boyut, boyut, TextureFormat.RGBA32, false);
        yuvarlakDoku.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < boyut; y++)
            for (int x = 0; x < boyut; x++)
            {
                float dx = Mathf.Max(0, Mathf.Abs(x - 31.5f) - 16f);
                float dy = Mathf.Max(0, Mathf.Abs(y - 31.5f) - 16f);
                float a = Mathf.Clamp01(16f - Mathf.Sqrt(dx * dx + dy * dy));
                yuvarlakDoku.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        yuvarlakDoku.Apply();
        yuvarlak = Sprite.Create(yuvarlakDoku, new UnityEngine.Rect(0, 0, boyut, boyut),
            new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(18, 18, 18, 18));
    }

    void Kapat()
    {
        dur = true;
        istemci?.Close();
        dinleyici?.Join(500);
        istemci = null;
        dinleyici = null;
        lock (kilit) bekleyen = null;
        if (panel != null) panel.SetActive(false);
    }

    void OnDisable() { Kapat(); }
    void OnApplicationQuit() { Kapat(); }
    void OnDestroy()
    {
        Kapat();
        if (etkin == this) etkin = null;
        if (canvasGo != null) Destroy(canvasGo);
        if (doku != null) Destroy(doku);
        if (yuvarlak != null) Destroy(yuvarlak);
        if (yuvarlakDoku != null) Destroy(yuvarlakDoku);
    }
}
