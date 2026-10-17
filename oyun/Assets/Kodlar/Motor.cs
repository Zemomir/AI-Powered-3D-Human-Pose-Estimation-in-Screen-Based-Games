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
    public float tabanHiz = 8f;          // m/sn, dik otururken
    public float oneOluBolge = 0.015f;   // metre; 2026-10-17 ölçümünde 20° eğilme ~4 cm verdi
    public float hizKatsayisi = 150f;    // (m/sn) / metre: +4 cm → yaklaşık +4 m/sn (%100 hassasiyette)
    // Hassasiyet öne/geri ekseninin tamamına uygulanır (geriye eğilip yavaşlamaya da), tek eksen tek ayar.
    // Oyun içinden +/- ile %10 adımla değişir, kapatınca hatırlanır (fizyoterapist çocuğa göre ayarlasın).
    // Varsayılan %90: 2026-10-17 ilk denemede Batın öne eğilmeyi biraz fazla hassas buldu.
    public const float VARSAYILAN_HIZ_HASSASIYETI = 0.9f;
    const string HASSASIYET_ANAHTARI = "HizHassasiyeti";
    public float HizHassasiyeti { get; private set; }
    public float enAzHiz = 3f, enCokHiz = 15f;
    public float yolDisiHiz = 4f;        // yoldan çıkınca (çim) üst sınır
    public float ivme = 6f;              // m/sn²

    public enum Durum { Bekliyor, Suruyor, SinyalYok, Bitti }
    public Durum Hal { get; private set; } = Durum.Bekliyor;
    public float Hiz { get; private set; }
    public float Sure { get; private set; }
    public float Ilerleme => Mathf.Clamp01((yolIndeksi - yol.BaslangicIndeksi) / (float)(yol.Noktalar.Count - 1 - yol.BaslangicIndeksi));
    public bool YolDisinda { get; private set; }

    int yolIndeksi;
    float gorselYatma;

    void Start()
    {
        float kayitli = PlayerPrefs.GetFloat(HASSASIYET_ANAHTARI, VARSAYILAN_HIZ_HASSASIYETI);
        if (!float.IsFinite(kayitli)) kayitli = VARSAYILAN_HIZ_HASSASIYETI;  // Clamp NaN'ı süzmez
        HizHassasiyeti = Mathf.Clamp(kayitli, 0.3f, 2f);
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
        if (yeni == HizHassasiyeti) return;
        HizHassasiyeti = yeni;
        PlayerPrefs.SetFloat(HASSASIYET_ANAHTARI, yeni);
        PlayerPrefs.Save();
    }

    public void Sifirla()
    {
        yolIndeksi = yol.BaslangicIndeksi;
        transform.position = yol.Noktalar[yolIndeksi];
        transform.rotation = Quaternion.LookRotation(yol.Yon(yolIndeksi));
        Hiz = 0; Sure = 0; gorselYatma = 0;
        if (govde != null) govde.localRotation = Quaternion.identity;
        Hal = Durum.Bekliyor;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R)) Sifirla();
        HassasiyetTuslari();
        if (Hal == Durum.Bitti) { Hiz = Mathf.MoveTowards(Hiz, 0, ivme * 2 * Time.deltaTime); Ilerlet(0); return; }

        float? egim = alici.Egim, one = alici.One;
        bool hazir = alici.Bagli && alici.Kalibre && egim.HasValue;

        if (!hazir)
        {
            // Kamera kişiyi görmüyorsa motor durur: kimse sürmüyorken oyun ilerlemesin.
            if (Hal == Durum.Suruyor) Hal = Durum.SinyalYok;
            Hiz = Mathf.MoveTowards(Hiz, 0, ivme * 2 * Time.deltaTime);
            Ilerlet(0);
            Yatir(0);
            return;
        }
        Hal = Durum.Suruyor;
        Sure += Time.deltaTime;

        float e = OluBolge(egim.Value, egimOluBolge);
        float donus = Mathf.Clamp(e * donusKatsayisi, -azamiDonus, azamiDonus);

        float hedef = tabanHiz;
        if (one.HasValue) hedef += OluBolge(one.Value, oneOluBolge) * hizKatsayisi * HizHassasiyeti;
        hedef = Mathf.Clamp(hedef, enAzHiz, enCokHiz);
        if (YolDisinda) hedef = Mathf.Min(hedef, yolDisiHiz);
        Hiz = Mathf.MoveTowards(Hiz, hedef, ivme * Time.deltaTime);

        Ilerlet(donus);
        Yatir(e);

        if (yolIndeksi >= yol.Noktalar.Count - 2) Hal = Durum.Bitti;
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
