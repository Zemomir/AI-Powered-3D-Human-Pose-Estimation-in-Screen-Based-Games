using UnityEngine;

// Motosiklet: yan eğim → dönüş, öne/geriye eğilme → hız.
// Fizik yok, düz kinematik: çocuğun hareketi doğrudan ve tahmin edilebilir sonuç versin.
public class Motor : MonoBehaviour
{
    public HareketAlici alici;
    public Yol yol;
    public Transform govde;  // görsel olarak yana yatan kısım

    [Header("Dönüş")]
    public float egimOluBolge = 3f;      // derece; dik otururken motor kendi kendine dönmesin
    public float donusKatsayisi = 2.5f;  // (derece/sn) / derece: 20° eğim → 42°/sn dönüş
    public float azamiDonus = 60f;       // derece/sn
    public float azamiGorselYatma = 35f;

    [Header("Hız")]
    public float sifirTakipSn = 10f;          // rahat duruşa yavaş yaklaşma süresi, saniye
    // Bu yana eğimin üstünde sıfır takibi durur, derece. Yan eğim "öne"yi ~0,1 cm/° kaydırıyor
    // (yan ölçümü); 3°'de en fazla ~0,3 cm sıfıra karışabilir.
    public float sifirTakipAzamiEgim = 3f;
    public float tabanHiz = 8f;          // m/sn, dik otururken
    public float oneOluBolge = 0.015f;   // metre; 2026-10-17 ölçümünde 20° eğilme ~4 cm verdi
    // Yana eğilirken "öne" sinyali kayıyor (2026-10-17 yan ölçümü: sağa ~11° → ~1 cm, sola ~0,4 cm,
    // titreşim de artıyor). Kayma hep öne doğru olduğu için yalnız ÖNE tarafın ölü bölgesi yan eğimle
    // genişler (10° yana → 1,5 + 1 = 2,5 cm); geriye eğilip yavaşlamak dönerken de aynı kalır.
    // Üst sınır: keskin virajda da öne eğilmek hız verebilsin (tam öne eğilme ~4 cm).
    public float yanEgimOluBolgeEki = 0.001f;  // metre / derece
    public float azamiOneOluBolge = 0.03f;     // metre
    // Öne sinyali ayrıca yumuşatılır (zaman sabiti, sn): kare kare oynama hıza geçmesin.
    // Büyütmek titremeyi azaltır ama hız öne eğilmeye geç tepki verir.
    public float oneYumusatmaSn = 0.25f;
    public float hizKatsayisi = 150f;    // (m/sn) / metre: +4 cm → yaklaşık +4 m/sn (%100 hassasiyette)
    // Hassasiyet öne/geri ekseninin tamamına uygulanır (geriye eğilip yavaşlamaya da), tek eksen tek ayar.
    // Oyun içinden +/- ile %10 adımla değişir, kapatınca hatırlanır (fizyoterapist çocuğa göre ayarlasın).
    // Varsayılan %90: 2026-10-17 ilk denemede Batın öne eğilmeyi biraz fazla hassas buldu.
    public const float VARSAYILAN_HIZ_HASSASIYETI = 0.9f;
    const string HASSASIYET_ANAHTARI = "HizHassasiyeti";
    public float HizHassasiyeti { get; private set; }
    // 2026-10-17 ikinci denemede Batın dönüşü biraz fazla hassas buldu.
    public const float VARSAYILAN_DONUS_HASSASIYETI = 0.8f;
    const string DONUS_HASSASIYETI_ANAHTARI = "DonusHassasiyeti";
    public float DonusHassasiyeti { get; private set; }
    // Batın gecikmesiz denemek istedi; PlayerPrefs'e yazılmaz.
    public bool YumusatmaAcik { get; private set; } = false;
    public float enAzHiz = 3f, enCokHiz = 15f;
    public float yolDisiHiz = 4f;        // yoldan çıkınca (çim) üst sınır
    public float ivme = 6f;              // m/sn²

    public enum Durum { Bekliyor, Suruyor, SinyalYok, Bitti }
    public Durum Hal { get; private set; } = Durum.Bekliyor;
    public float Hiz { get; private set; }
    public float HedefHiz { get; private set; }
    public float Sure { get; private set; }
    public float OneSifir { get; private set; } = 0f;
    public float EgimSifir { get; private set; } = 0f;
    public float? DuzeltilmisEgim { get; private set; }
    public float? DuzeltilmisOne { get; private set; }
    public float Ilerleme => Mathf.Clamp01((yolIndeksi - yol.BaslangicIndeksi) / (float)(yol.Noktalar.Count - 1 - yol.BaslangicIndeksi));
    public bool YolDisinda { get; private set; }

    int yolIndeksi;
    float gorselYatma;
    float? duzOne;  // yumuşatılmış öne sinyali; sinyal kopunca sıfırlanır, eski değer geri gelmesin
    float oneYokSure;  // kalça bir an görünmeyince yumuşatma baştan başlamasın, ancak uzun kesintide

    void Start()
    {
        float kayitli = PlayerPrefs.GetFloat(HASSASIYET_ANAHTARI, VARSAYILAN_HIZ_HASSASIYETI);
        if (!float.IsFinite(kayitli)) kayitli = VARSAYILAN_HIZ_HASSASIYETI;  // Clamp NaN'ı süzmez
        HizHassasiyeti = Mathf.Clamp(kayitli, 0.3f, 2f);
        float kayitliDonus = PlayerPrefs.GetFloat(DONUS_HASSASIYETI_ANAHTARI, VARSAYILAN_DONUS_HASSASIYETI);
        if (!float.IsFinite(kayitliDonus)) kayitliDonus = VARSAYILAN_DONUS_HASSASIYETI;
        DonusHassasiyeti = Mathf.Clamp(kayitliDonus, 0.3f, 2f);
        Sifirla();
    }

    // inputString yazılan karakteri verir: Türkçe klavyede de, sayısal tuş takımında da + ve - çalışır.
    void HassasiyetTuslari()
    {
        float yeni = HizHassasiyeti;
        foreach (char c in Input.inputString)
        {
            if (c == '+') yeni += 0.1f;
            else if (c == '-') yeni -= 0.1f;
        }
        yeni = Mathf.Clamp(Mathf.Round(yeni * 10f) / 10f, 0.3f, 2f);
        if (yeni != HizHassasiyeti)  // erken return yok: aşağıdaki dönüş ayarı da her karede işlensin
        {
            HizHassasiyeti = yeni;
            PlayerPrefs.SetFloat(HASSASIYET_ANAHTARI, yeni);
            PlayerPrefs.Save();
        }

        float yeniDonus = DonusHassasiyeti;
        foreach (char c in Input.inputString)
        {
            if (c == '2') yeniDonus += 0.1f;
            else if (c == '1') yeniDonus -= 0.1f;
        }
        yeniDonus = Mathf.Clamp(Mathf.Round(yeniDonus * 10f) / 10f, 0.3f, 2f);
        if (yeniDonus != DonusHassasiyeti)
        {
            DonusHassasiyeti = yeniDonus;
            PlayerPrefs.SetFloat(DONUS_HASSASIYETI_ANAHTARI, yeniDonus);
            PlayerPrefs.Save();
        }
    }

    public void Sifirla()
    {
        yolIndeksi = yol.BaslangicIndeksi;
        transform.position = yol.Noktalar[yolIndeksi];
        transform.rotation = Quaternion.LookRotation(yol.Yon(yolIndeksi));
        Hiz = 0; Sure = 0; gorselYatma = 0; duzOne = null; oneYokSure = 0;
        if (govde != null) govde.localRotation = Quaternion.identity;
        Hal = Durum.Bekliyor;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R)) Sifirla();
        if (Input.GetKeyDown(KeyCode.Y)) YumusatmaAcik = !YumusatmaAcik;
        HassasiyetTuslari();
        // Bağlıyken "kalibre değil" = Python yeniden kalibre ediyor, eski düzeltme kalmasın.
        // Bağlantı kısa kopunca (Bagli false) ofset korunur: C ile ayarlanan sıfır kaybolmasın.
        bool ofsetSil = alici.KlavyeModu || (alici.Bagli && !alici.Kalibre);
        if (ofsetSil && (OneSifir != 0f || EgimSifir != 0f)) { OneSifir = 0f; EgimSifir = 0f; duzOne = null; }
        if (Input.GetKeyDown(KeyCode.C) && !alici.KlavyeModu && alici.Kalibre)
        {
            // O anki duruş yeni sıfır olur; yumuşatma eski sıfıra göre kalmasın.
            if (alici.Egim.HasValue) EgimSifir = alici.Egim.Value;
            if (alici.One.HasValue) OneSifir = alici.One.Value;
            duzOne = null;
        }
        DuzeltilmisEgim = alici.Egim.HasValue ? alici.Egim.Value - (alici.KlavyeModu ? 0f : EgimSifir) : (float?)null;
        DuzeltilmisOne = alici.One.HasValue ? alici.One.Value - (alici.KlavyeModu ? 0f : OneSifir) : (float?)null;
        if (Hal == Durum.Bitti) { HedefHiz = 0; Hiz = Mathf.MoveTowards(Hiz, 0, ivme * 2 * Time.deltaTime); Ilerlet(0); return; }

        float? egim = DuzeltilmisEgim, one = DuzeltilmisOne;
        bool hazir = alici.Bagli && alici.Kalibre && egim.HasValue;

        if (!hazir)
        {
            HedefHiz = 0;
            // Kamera kişiyi görmüyorsa motor durur: kimse sürmüyorken oyun ilerlemesin.
            if (Hal == Durum.Suruyor) Hal = Durum.SinyalYok;
            Hiz = Mathf.MoveTowards(Hiz, 0, ivme * 2 * Time.deltaTime);
            Ilerlet(0);
            Yatir(0);
            duzOne = null; oneYokSure = 0;
            return;
        }
        Hal = Durum.Suruyor;
        Sure += Time.deltaTime;

        if (one.HasValue && !alici.KlavyeModu && Mathf.Abs(one.Value) < Mathf.Max(0f, oneOluBolge) && Mathf.Abs(egim.Value) < sifirTakipAzamiEgim)
        {
            // Yalnız ölü bölgedeki küçük duruş kaymasını düzelt; bilerek eğilmeyi emme.
            float a = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(sifirTakipSn, 0.001f));
            OneSifir += one.Value * a;
            one = DuzeltilmisOne = alici.One.HasValue ? alici.One.Value - OneSifir : (float?)null;
        }

        float e = OluBolge(egim.Value, egimOluBolge);
        float donus = Mathf.Clamp(e * donusKatsayisi * DonusHassasiyeti, -azamiDonus, azamiDonus);

        float hedef = tabanHiz;
        if (one.HasValue)
        {
            // Kare hızından bağımsız üstel yumuşatma.
            if (YumusatmaAcik)
            {
                float a = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(oneYumusatmaSn, 0.001f));
                duzOne = duzOne.HasValue ? Mathf.Lerp(duzOne.Value, one.Value, a) : one.Value;
            }
            else duzOne = one.Value;
            oneYokSure = 0;
        }
        else if ((oneYokSure += Time.unscaledDeltaTime) > HareketAlici.ZAMAN_ASIMI_SN) duzOne = null;

        if (duzOne.HasValue)  // kısa kesintide son yumuşatılmış değer kullanılır, uzunda katkı yok
        {
            float temelOneEsik = Mathf.Max(0f, oneOluBolge);
            // Klavye modunda yana eğimden kaynaklanan kamera kayması olmadığı için eşik genişlemez.
            float oneEsik = alici.KlavyeModu ? temelOneEsik :
                Mathf.Min(temelOneEsik + Mathf.Max(0f, yanEgimOluBolgeEki) * Mathf.Abs(egim.Value), Mathf.Max(azamiOneOluBolge, temelOneEsik));
            float x = duzOne.Value;
            float katki = x > oneEsik ? x - oneEsik : (x < -temelOneEsik ? x + temelOneEsik : 0f);
            hedef += katki * hizKatsayisi * HizHassasiyeti;
        }
        hedef = Mathf.Clamp(hedef, enAzHiz, enCokHiz);
        if (YolDisinda) hedef = Mathf.Min(hedef, yolDisiHiz);
        HedefHiz = hedef;
        Hiz = Mathf.MoveTowards(Hiz, hedef, ivme * Time.deltaTime);

        Ilerlet(donus);
        Yatir(e);

        if (yolIndeksi >= yol.Noktalar.Count - 2) { Hal = Durum.Bitti; HedefHiz = 0; }
    }

    void Yatir(float egim)
    {
        gorselYatma = Mathf.Lerp(gorselYatma, Mathf.Clamp(egim, -azamiGorselYatma, azamiGorselYatma), 8f * Time.deltaTime);
        if (govde != null) govde.localRotation = Quaternion.Euler(0, 0, -gorselYatma);
    }

    void Ilerlet(float donus)
    {
        transform.Rotate(0, donus * Time.deltaTime, 0, Space.World);
        transform.position += transform.forward * Hiz * Time.deltaTime;
        yolIndeksi = yol.EnYakin(transform.position, yolIndeksi, out float uzaklik);
        YolDisinda = uzaklik > yol.genislik / 2 + 0.5f;
    }

    // Ölü bölgenin içi 0; dışı, sınırdan itibaren sayılır (sıçrama olmasın).
    static float OluBolge(float x, float esik) => Mathf.Abs(x) <= esik ? 0f : x - Mathf.Sign(x) * esik;
}
