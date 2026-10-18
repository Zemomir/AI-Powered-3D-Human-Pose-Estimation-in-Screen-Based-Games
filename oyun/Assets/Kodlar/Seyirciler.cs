using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

// Kontrol hesabından bağımsız el tekrarı ve görsel geri bildirim. Kayıttan önce çalışır.
[DefaultExecutionOrder(50)]
public class Seyirciler : MonoBehaviour
{
    public Yol yol;
    public Motor motor;
    public HareketAlici alici;
    public Ses ses;
    public Material ten, pantolon, sapka;
    public Material[] tisortler;
    public Material belirginTisort;
    public Transform solKolKok, sagKolKok;
    public int tarafBasina = 3;
    public float elEsik = -0.3f;
    const int cakOnce = 13, cakSonra = 0;

    public bool SolKalkik { get; private set; }
    public bool SagKalkik { get; private set; }
    public int SolCak { get; private set; }
    public int SagCak { get; private set; }
    public int SolToplam { get; private set; }
    public int SagToplam { get; private set; }
    public string KareOlayi { get; private set; } = "";

    class Seyirci
    {
        public Transform kok, solKol, sagKol;
        public Vector3 konum;
        public int indeks, taraf;
        public bool cakildi, kacti;
        public float sevincBasla = -1f;
        public bool izleyici;
        public int hedef = -1;
        public Quaternion baslangicDonusu;
        public float salinimFazi, sevincGecikmesi;
    }

    readonly List<Seyirci> kisiler = new List<Seyirci>();
    readonly List<Vector3> direkler = new List<Vector3>();
    bool solHazir = true, sagHazir = true;
    float solNullBasla = -1f, sagNullBasla = -1f;
    int ipucu;
    float solPoz, sagPoz;

    void OnEnable() { if (motor != null) motor.Sifirlandi += Sifirla; }
    void OnDisable() { if (motor != null) motor.Sifirlandi -= Sifirla; }

    void Start()
    {
        if (yol == null || yol.Noktalar == null || yol.Noktalar.Count < 2 ||
            ten == null || pantolon == null || tisortler == null || tisortler.Length == 0) return;
        ipucu = yol.BaslangicIndeksi;
        DirekleriOlc();
        int ilk = yol.BaslangicIndeksi + 40, son = yol.Noktalar.Count - 1 - 25;
        int adet = Mathf.Max(0, tarafBasina) * 2;
        if (son < ilk || adet == 0) return;
        var indeksler = DuzIndeksleri(ilk, son, adet);
        var rastgele = new System.Random(5065);
        for (int sira = 0; sira < indeksler.Count; sira++)
        {
            int i = indeksler[sira];
            int taraf = sira % 2 == 0 ? -1 : 1;
            Vector3 yan = Vector3.Cross(Vector3.up, yol.Yon(i)).normalized;
            Vector3 p = yol.Noktalar[i] + yan * taraf * (yol.genislik / 2 + 1.6f);
            // YolSusu'nun gerçek yay mesafesiyle hesaplanan direklerine en az 1,35 m bırak.
            // Dışa kaydırma en çok 1,4 m: Cevre'de bütün yol noktalarından bırakılan
            // kenar + 7 m boşluğun içinde, kollar dahil ağaç/kayalardan uzakta kalır.
            foreach (Vector3 direk in direkler)
            {
                Vector3 fark = p - direk; fark.y = 0;
                if (fark.sqrMagnitude < 1.35f * 1.35f)
                {
                    p += yan * taraf * 1.4f;
                    break;
                }
            }
            var arazi = Terrain.activeTerrain;
            p.y = arazi != null ? arazi.SampleHeight(p) + arazi.transform.position.y : yol.Noktalar[i].y;
            var kisi = KisiKur(sira, rastgele, false, belirginTisort);
            kisi.baslangicDonusu = Quaternion.LookRotation(-yan * taraf);
            kisi.indeks = i; kisi.taraf = taraf; kisi.konum = p;
            kisi.kok.position = p;
            kisi.kok.rotation = kisi.baslangicDonusu;
            kisiler.Add(kisi);
            int anaIndeks = kisiler.Count - 1;
            if (taraf < 0) SolToplam++; else SagToplam++;
            for (int izleyiciNo = 0; izleyiciNo < 4; izleyiciNo++)
            {
                Vector3 izleyiciP = p;
                bool uygun = false;
                for (int deneme = 0; deneme < 12 && !uygun; deneme++)
                {
                    float boyuna = (izleyiciNo % 2 == 0 ? -1 : 1) * (.55f + (float)rastgele.NextDouble() * .85f);
                    float disa = .6f + (float)rastgele.NextDouble() * 1.4f;
                    izleyiciP = yol.Noktalar[i] + yan * taraf * (yol.genislik / 2 + 1.6f + disa) + yol.Yon(i) * boyuna;
                    izleyiciP.y = arazi != null ? arazi.SampleHeight(izleyiciP) + arazi.transform.position.y : yol.Noktalar[i].y;
                    uygun = KonumUygun(izleyiciP, kisiler);
                }
                if (!uygun) continue;
                var izleyici = KisiKur(12 + sira * 4 + izleyiciNo, rastgele, true);
                izleyici.indeks = i; izleyici.taraf = taraf; izleyici.konum = izleyiciP;
                izleyici.hedef = anaIndeks;
                izleyici.sevincGecikmesi = (float)rastgele.NextDouble() * .25f;
                izleyici.kok.position = izleyiciP;
                izleyici.kok.rotation = Quaternion.LookRotation(-yan * taraf);
                kisiler.Add(izleyici);
            }
        }
    }

    List<int> DuzIndeksleri(int ilk, int son, int adet)
    {
        const float pencere = 15f, duzEsik = 1f;  // ±15 m'de toplam en çok 1 derece: kol kaldırırken dönüş isteme.
        var mesafeler = new float[yol.Noktalar.Count];
        for (int i = 1; i < mesafeler.Length; i++)
            mesafeler[i] = mesafeler[i - 1] + Vector3.Distance(yol.Noktalar[i - 1], yol.Noktalar[i]);
        var degisimler = new float[yol.Noktalar.Count];
        var adaylar = new List<int>();
        var kalanlar = new List<int>();
        for (int i = ilk; i <= son; i++)
        {
            int a = i, b = i;
            while (a > 0 && mesafeler[i] - mesafeler[a] < pencere) a--;
            while (b < mesafeler.Length - 1 && mesafeler[b] - mesafeler[i] < pencere) b++;
            // Mutlak açıları topla; S biçimli bir pencerede zıt dönüşler birbirini silmesin.
            for (int j = a + 1; j <= b; j++)
                degisimler[i] += Vector3.Angle(yol.Yon(j - 1), yol.Yon(j));
            if (degisimler[i] <= duzEsik) adaylar.Add(i);
            else kalanlar.Add(i);
        }
        if (BolumlereYay(adaylar, mesafeler, adet, out var sonuc)) return sonuc;
        Debug.LogWarning("Düz bölüm/8 m kenar payı yetersiz; en düşük eğrilikli noktalarla yedek dağılım kullanılacak.", this);
        if (adaylar.Count < adet)
        {
            kalanlar.Sort((a, b) => {
                int fark = degisimler[a].CompareTo(degisimler[b]);
                return fark != 0 ? fark : a.CompareTo(b);
            });
            for (int i = 0; i < kalanlar.Count && adaylar.Count < adet; i++) adaylar.Add(kalanlar[i]);
            adaylar.Sort();
        }
        int sayi = Mathf.Min(adet, adaylar.Count);
        sayi -= sayi % 2;
        if (sayi < adet) Debug.LogWarning("Yol kenar payları içinde seyirci sayısı tamamlanamadı.", this);
        sonuc = new List<int>();
        // Yetersizlikte önceki uyarılı yedek kural: düz adaylar, ardından en düşük eğrilikler.
        for (int sira = 0; sira < sayi; sira++)
        {
            int yer = sayi == 1 ? adaylar.Count / 2 : Mathf.RoundToInt(sira * (adaylar.Count - 1f) / (sayi - 1));
            sonuc.Add(adaylar[yer]);
        }
        return sonuc;
    }

    bool BolumlereYay(List<int> adaylar, float[] mesafeler, int adet, out List<int> sonuc)
    {
        sonuc = new List<int>();
        var bolumler = new List<List<int>>();
        for (int bas = 0; bas < adaylar.Count;)
        {
            int son = bas;
            while (son + 1 < adaylar.Count && adaylar[son + 1] == adaylar[son] + 1) son++;
            // Her ardışık düz bölümün iki ucundan gerçek yay mesafesiyle en az 8 m çıkar.
            float ilkMesafe = mesafeler[adaylar[bas]] + 8f;
            float sonMesafe = mesafeler[adaylar[son]] - 8f;
            var bolum = new List<int>();
            for (int j = bas; j <= son; j++)
                if (mesafeler[adaylar[j]] >= ilkMesafe && mesafeler[adaylar[j]] <= sonMesafe)
                    bolum.Add(adaylar[j]);
            if (bolum.Count == 0)
                Debug.LogWarning("8 m kenar payı sonrası kısa düz parça atlandı.", this);
            else
                bolumler.Add(bolum);
            bas = son + 1;
        }
        if (bolumler.Count == 0 || bolumler.Count > adet) return false;

        var uzunluklar = new float[bolumler.Count];
        var paylar = new int[bolumler.Count];
        var kotalar = new float[bolumler.Count];
        float toplam = 0f;
        int kapasite = 0;
        for (int b = 0; b < bolumler.Count; b++)
        {
            var bolum = bolumler[b];
            uzunluklar[b] = mesafeler[bolum[bolum.Count - 1]] - mesafeler[bolum[0]];
            toplam += uzunluklar[b];
            kapasite += bolum.Count;
        }
        if (kapasite < adet) return false;
        // Önce her bölüme bir kişi; kalanları kullanılabilir uzunluğa orantılı,
        // en büyük kalan yöntemiyle paylaştır. Aynı yol örneğini iki kez kullanma.
        int kalan = adet - bolumler.Count, dagitilan = 0;
        for (int b = 0; b < bolumler.Count; b++)
        {
            kotalar[b] = toplam > 0f ? kalan * uzunluklar[b] / toplam : 0f;
            paylar[b] = 1 + Mathf.Min(bolumler[b].Count - 1, Mathf.FloorToInt(kotalar[b]));
            dagitilan += paylar[b];
        }
        while (dagitilan < adet)
        {
            int secilen = -1;
            for (int b = 0; b < bolumler.Count; b++)
                if (paylar[b] < bolumler[b].Count &&
                    (secilen < 0 || kotalar[b] - (paylar[b] - 1) > kotalar[secilen] - (paylar[secilen] - 1)))
                    secilen = b;
            if (secilen < 0) return false;
            paylar[secilen]++;
            dagitilan++;
        }
        for (int b = 0; b < bolumler.Count; b++)
        {
            var bolum = bolumler[b];
            int onceki = -1;
            for (int sira = 0; sira < paylar[b]; sira++)
            {
                float oran = paylar[b] == 1 ? .5f : sira / (float)(paylar[b] - 1);
                float hedef = Mathf.Lerp(mesafeler[bolum[0]], mesafeler[bolum[bolum.Count - 1]], oran);
                int yer = onceki + 1, sonYer = bolum.Count - (paylar[b] - sira);
                while (yer < sonYer &&
                    Mathf.Abs(mesafeler[bolum[yer + 1]] - hedef) < Mathf.Abs(mesafeler[bolum[yer]] - hedef))
                    yer++;
                sonuc.Add(bolum[yer]);
                onceki = yer;
            }
        }
        // Bölüm ve indeks sırası korunur: Start'taki sol/sağ dönüşümü tam adet/2 + adet/2 verir.
        return true;
    }

    // Birleşik çevre meshlerinin kaba bounds'u yerine YolSusu ile aynı direk geometrisi.
    void DirekleriOlc()
    {
        float mesafe = 0, sonraki = 12.5f;
        float toplam = 0;
        for (int i = 1; i < yol.Noktalar.Count; i++)
            toplam += Vector3.Distance(yol.Noktalar[i - 1], yol.Noktalar[i]);
        for (int i = 1; i < yol.Noktalar.Count; i++)
        {
            float uzunluk = Vector3.Distance(yol.Noktalar[i - 1], yol.Noktalar[i]);
            while (sonraki <= mesafe + uzunluk && sonraki < toplam - 5)
            {
                Vector3 p = Vector3.Lerp(yol.Noktalar[i - 1], yol.Noktalar[i],
                    uzunluk > 0 ? (sonraki - mesafe) / uzunluk : 0);
                // Direk yönü YolSusu.Yon gibi s +/- 0,5 örneklerinden alınır.
                Vector3 yon = (YayKonumu(sonraki + .5f) - YayKonumu(sonraki - .5f)).normalized;
                Vector3 yan = Vector3.Cross(Vector3.up, yon);
                direkler.Add(p - yan * (yol.genislik / 2 + 1.5f));
                direkler.Add(p + yan * (yol.genislik / 2 + 1.5f));
                sonraki += 25;
            }
            mesafe += uzunluk;
        }
    }

    Vector3 YayKonumu(float s)
    {
        for (int i = 1; i < yol.Noktalar.Count; i++)
        {
            float uzunluk = Vector3.Distance(yol.Noktalar[i - 1], yol.Noktalar[i]);
            if (s <= uzunluk) return Vector3.Lerp(yol.Noktalar[i - 1], yol.Noktalar[i], uzunluk > 0 ? s / uzunluk : 0);
            s -= uzunluk;
        }
        return yol.Noktalar[yol.Noktalar.Count - 1];
    }

    void Update()
    {
        KareOlayi = "";
        float? solEl = alici != null ? alici.SolEl : null;
        float? sagEl = alici != null ? alici.SagEl : null;
        SolKalkik = ElKalkik(solEl, SolKalkik);
        SagKalkik = ElKalkik(sagEl, SagKalkik);
        HazirligiGuncelle(solEl, ref solHazir, ref solNullBasla);
        HazirligiGuncelle(sagEl, ref sagHazir, ref sagNullBasla);
        if (motor == null || yol == null || yol.Noktalar == null || yol.Noktalar.Count < 2) return;
        if (motor.Hal == Motor.Durum.Bekliyor || motor.Hal == Motor.Durum.Bitti)
            ipucu = yol.BaslangicIndeksi;
        else ipucu = yol.EnYakin(motor.transform.position, ipucu, out _);
        // Ayarlaniyor ya da sinyal kopmasinda geçilen seyircinin kacti olayi Suruyor'a dönüste yazilir; sayim dogru kalir.
        if (motor.Hal != Motor.Durum.Suruyor) return;

        foreach (var kisi in kisiler)
        {
            if (kisi.izleyici || kisi.cakildi || kisi.kacti) continue;
            if (ipucu > kisi.indeks)
            {
                kisi.kacti = true;
                Olay(kisi.taraf < 0 ? "kacti_sol" : "kacti_sag");
            }
            else if (ipucu >= kisi.indeks - cakOnce && ipucu <= kisi.indeks + cakSonra &&
                (kisi.taraf < 0 ? SolKalkik && solHazir : SagKalkik && sagHazir))
            {
                kisi.cakildi = true;
                kisi.sevincBasla = Time.time;
                foreach (var izleyici in kisiler)
                    if (izleyici.izleyici && izleyici.hedef == kisiler.IndexOf(kisi))
                        izleyici.sevincBasla = Time.time + izleyici.sevincGecikmesi;
                if (kisi.taraf < 0) { SolCak++; solHazir = false; Olay("cak_sol"); }
                else { SagCak++; sagHazir = false; Olay("cak_sag"); }
                if (ses != null) ses.Cak();
            }
        }
    }

    bool ElKalkik(float? el, bool onceki)
    {
        if (!el.HasValue || !float.IsFinite(el.Value)) return false;
        return onceki ? el.Value >= elEsik - .3f : el.Value > elEsik;
    }

    void HazirligiGuncelle(float? el, ref bool hazir, ref float nullBasla)
    {
        if (!el.HasValue || !float.IsFinite(el.Value))
        {
            if (nullBasla < 0f) nullBasla = Time.time;
            else if (Time.time - nullBasla >= .3f) hazir = true;
            return;
        }
        nullBasla = -1f;
        if (el.Value < elEsik - .3f) hazir = true;
    }

    void Olay(string ad)
    {
        Vector3 yan = Vector3.Cross(Vector3.up, yol.Yon(ipucu)).normalized;
        float ofset = Vector3.Dot(motor.transform.position - yol.Noktalar[ipucu], yan);
        KareOlayi += (KareOlayi.Length == 0 ? "" : " ") + ad +
            " yan_ofset=" + ofset.ToString("F3", CultureInfo.InvariantCulture);
    }

    public bool BekleyenSeyirci(bool sag, int motorIndeksi)
    {
        foreach (var kisi in kisiler)
            if (!kisi.izleyici && !kisi.cakildi && !kisi.kacti && (kisi.taraf > 0) == sag &&
                motorIndeksi >= kisi.indeks - cakOnce && motorIndeksi <= kisi.indeks + cakSonra) return true;
        return false;
    }

    void LateUpdate()
    {
        KolGoster(solKolKok, -1, SolKalkik, ref solPoz);
        KolGoster(sagKolKok, 1, SagKalkik, ref sagPoz);
        for (int i = 0; i < kisiler.Count; i++)
        {
            var kisi = kisiler[i];
            if (kisi.izleyici)
            {
                if (motor == null || Vector3.Distance(motor.transform.position, kisi.konum) > 80f) continue;
                Vector3 yon = motor.transform.position - kisi.kok.position; yon.y = 0;
                if (yon.sqrMagnitude > .001f)
                    kisi.kok.rotation = Quaternion.RotateTowards(kisi.kok.rotation, Quaternion.LookRotation(yon), 18f * Time.deltaTime);
                float sway = Mathf.Sin(Time.time * 1.15f + kisi.salinimFazi) * 5f;
                kisi.solKol.localRotation = Quaternion.Euler(sway, 0, -8f);
                kisi.sagKol.localRotation = Quaternion.Euler(-sway, 0, 8f);
                float gecikmeli = kisi.sevincBasla < 0 ? 2f : Time.time - kisi.sevincBasla;
                bool alkis = gecikmeli >= 0 && gecikmeli < 1.5f;
                kisi.solKol.localRotation = Quaternion.Euler(alkis ? -165f : sway, 0, alkis ? -15f : -8f);
                kisi.sagKol.localRotation = Quaternion.Euler(alkis ? -165f : -sway, 0, alkis ? 15f : 8f);
                kisi.kok.position = kisi.konum;
                continue;
            }
            float gecen = kisi.sevincBasla < 0 ? 2f : Time.time - kisi.sevincBasla;
            bool sevinc = gecen < 1.5f;
            bool bekliyor = motor != null && motor.Hal == Motor.Durum.Suruyor &&
                !kisi.cakildi && !kisi.kacti && ipucu >= kisi.indeks - 40 && ipucu <= kisi.indeks + cakSonra;
            // Yerel +Z yola bakar; yol tarafındaki sağ kol öne/yukarı uzanır, avuç yola dönüktür.
            float salinim = Mathf.Sin(Time.time * 2f + i) * 4f;
            kisi.solKol.localRotation = Quaternion.Euler(sevinc ? -165f : salinim, 0, sevinc ? -15f : -8f);
            kisi.sagKol.localRotation = Quaternion.Euler(sevinc ? -165f : bekliyor ? -145f + salinim : -salinim,
                0, sevinc ? 15f : 8f);
            float zipla = sevinc && gecen < .4f ? Mathf.Sin(gecen / .4f * Mathf.PI) * .18f : 0;
            kisi.kok.position = kisi.konum + Vector3.up * zipla;
        }
    }

    static void KolGoster(Transform kok, int taraf, bool kalkik, ref float poz)
    {
        poz = Mathf.MoveTowards(poz, kalkik ? 1f : 0f, Time.deltaTime / .15f);
        if (kok == null) return;
        Vector3 gidon = new Vector3(taraf * .17f, -.27f, .54f);
        Vector3 yukari = new Vector3(taraf * .26f, .55f, .09f);
        kok.localRotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(gidon, yukari), poz);
    }

    void Sifirla()
    {
        SolCak = SagCak = 0;
        SolKalkik = SagKalkik = false;
        solHazir = sagHazir = true;
        solNullBasla = sagNullBasla = -1f;
        solPoz = sagPoz = 0;
        KareOlayi = "";
        ipucu = yol != null ? yol.BaslangicIndeksi : 0;
        if (solKolKok != null) solKolKok.localRotation = Quaternion.identity;
        if (sagKolKok != null) sagKolKok.localRotation = Quaternion.identity;
        foreach (var kisi in kisiler)
        {
            kisi.cakildi = kisi.kacti = false;
            kisi.sevincBasla = -1;
            kisi.kok.position = kisi.konum;
            if (kisi.izleyici && kisi.hedef >= 0 && kisi.hedef < kisiler.Count)
                kisi.kok.rotation = kisiler[kisi.hedef].baslangicDonusu;
            kisi.solKol.localRotation = kisi.sagKol.localRotation = Quaternion.identity;
        }
    }

    bool KonumUygun(Vector3 p, List<Seyirci> mevcut)
    {
        foreach (Vector3 direk in direkler) { Vector3 d = p - direk; d.y = 0; if (d.sqrMagnitude < 1.35f * 1.35f) return false; }
        foreach (var kisi in mevcut) { Vector3 d = p - kisi.konum; d.y = 0; if (d.sqrMagnitude < .62f * .62f) return false; }
        return true;
    }

    Seyirci KisiKur(int sira, System.Random r, bool izleyici, Material sabitTisort = null)
    {
        var kok = new GameObject("Seyirci_" + sira).transform;
        kok.SetParent(transform, false);
        kok.localScale = Vector3.one * (izleyici ? .85f + (float)r.NextDouble() * .25f : .9f + (float)r.NextDouble() * .2f);
        Material tisort = sabitTisort != null ? sabitTisort : tisortler[r.Next(tisortler.Length)];
        Parca(kok, "Govde", PrimitiveType.Capsule, new Vector3(0, 1.04f, 0), new Vector3(.46f, .35f, .28f), tisort);
        Parca(kok, "Bas", PrimitiveType.Sphere, new Vector3(0, 1.57f, 0), new Vector3(.30f, .35f, .30f), ten);
        for (int taraf = -1; taraf <= 1; taraf += 2)
            Parca(kok, "Bacak", PrimitiveType.Capsule, new Vector3(taraf * .13f, .38f, 0), new Vector3(.18f, .38f, .20f), pantolon);
        var sol = SeyirciKolu(kok, -1, tisort);
        var sag = SeyirciKolu(kok, 1, tisort);
        if (izleyici && sira % 3 == 0 && sapka != null)
        {
            Parca(kok, "Sapka", PrimitiveType.Sphere, new Vector3(0, 1.73f, 0), new Vector3(.33f, .15f, .33f), sapka);
            Parca(kok, "Siper", PrimitiveType.Cube, new Vector3(0, 1.71f, .15f), new Vector3(.32f, .035f, .24f), sapka);
        }
        foreach (var parca in kok.GetComponentsInChildren<MeshRenderer>())
            if (izleyici && sira % 4 >= 2) parca.shadowCastingMode = ShadowCastingMode.Off;
        return new Seyirci { kok = kok, solKol = sol, sagKol = sag, izleyici = izleyici,
            salinimFazi = (float)r.NextDouble() * 6.28f };
    }

    Transform SeyirciKolu(Transform kisi, int taraf, Material tisort)
    {
        var kok = new GameObject(taraf < 0 ? "SolKol" : "SagKol").transform;
        kok.SetParent(kisi, false);
        kok.localPosition = new Vector3(taraf * .25f, 1.30f, 0);
        Parca(kok, "Kol", PrimitiveType.Capsule, new Vector3(0, -.25f, 0), new Vector3(.13f, .26f, .13f), tisort);
        Parca(kok, "Avuc", PrimitiveType.Sphere, new Vector3(0, -.55f, 0), new Vector3(.14f, .18f, .07f), ten);
        return kok;
    }

    static void Parca(Transform ust, string ad, PrimitiveType tip, Vector3 p, Vector3 olcek, Material m)
    {
        var go = GameObject.CreatePrimitive(tip);
        go.name = ad;
        var collider = go.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        go.transform.SetParent(ust, false);
        go.transform.localPosition = p;
        go.transform.localScale = olcek;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = m;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
    }
}
