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
    public Transform solKolKok, sagKolKok;
    public int tarafBasina = 6;
    public float elEsik = 0f;

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
    }

    readonly List<Seyirci> kisiler = new List<Seyirci>();
    readonly List<Vector3> direkler = new List<Vector3>();
    bool solHazir = true, sagHazir = true;
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
        var rastgele = new System.Random(5065);
        for (int sira = 0; sira < adet; sira++)
        {
            int i = Mathf.RoundToInt(Mathf.Lerp(ilk, son, sira / (float)(adet - 1)));
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
            var kisi = KisiKur(sira, rastgele);
            kisi.indeks = i; kisi.taraf = taraf; kisi.konum = p;
            kisi.kok.position = p;
            kisi.kok.rotation = Quaternion.LookRotation(-yan * taraf);
            kisiler.Add(kisi);
            if (taraf < 0) SolToplam++; else SagToplam++;
        }
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
        SolKalkik = ElKalkik(alici != null ? alici.SolEl : null, SolKalkik);
        SagKalkik = ElKalkik(alici != null ? alici.SagEl : null, SagKalkik);
        if (!SolKalkik) solHazir = true;
        if (!SagKalkik) sagHazir = true;
        if (motor == null || yol == null || yol.Noktalar == null || yol.Noktalar.Count < 2) return;
        if (motor.Hal == Motor.Durum.Bekliyor || motor.Hal == Motor.Durum.Bitti)
            ipucu = yol.BaslangicIndeksi;
        else ipucu = yol.EnYakin(motor.transform.position, ipucu, out _);
        if (motor.Hal != Motor.Durum.Suruyor) return;

        foreach (var kisi in kisiler)
        {
            if (kisi.cakildi || kisi.kacti) continue;
            if (ipucu > kisi.indeks + 3)
            {
                kisi.kacti = true;
                Olay(kisi.taraf < 0 ? "kacti_sol" : "kacti_sag");
            }
            else if (ipucu >= kisi.indeks - 15 &&
                (kisi.taraf < 0 ? SolKalkik && solHazir : SagKalkik && sagHazir))
            {
                kisi.cakildi = true;
                kisi.sevincBasla = Time.time;
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
            if (!kisi.cakildi && !kisi.kacti && (kisi.taraf > 0) == sag &&
                motorIndeksi >= kisi.indeks - 15 && motorIndeksi <= kisi.indeks + 3) return true;
        return false;
    }

    void LateUpdate()
    {
        KolGoster(solKolKok, -1, SolKalkik, ref solPoz);
        KolGoster(sagKolKok, 1, SagKalkik, ref sagPoz);
        for (int i = 0; i < kisiler.Count; i++)
        {
            var kisi = kisiler[i];
            float gecen = kisi.sevincBasla < 0 ? 2f : Time.time - kisi.sevincBasla;
            bool sevinc = gecen < 1.5f;
            bool bekliyor = motor != null && motor.Hal == Motor.Durum.Suruyor &&
                !kisi.cakildi && !kisi.kacti && ipucu >= kisi.indeks - 40 && ipucu <= kisi.indeks + 3;
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
            kisi.solKol.localRotation = kisi.sagKol.localRotation = Quaternion.identity;
        }
    }

    Seyirci KisiKur(int sira, System.Random r)
    {
        var kok = new GameObject("Seyirci_" + sira).transform;
        kok.SetParent(transform, false);
        kok.localScale = Vector3.one * (.9f + (float)r.NextDouble() * .2f);
        Material tisort = tisortler[r.Next(tisortler.Length)];
        Parca(kok, "Govde", PrimitiveType.Capsule, new Vector3(0, 1.04f, 0), new Vector3(.46f, .35f, .28f), tisort);
        Parca(kok, "Bas", PrimitiveType.Sphere, new Vector3(0, 1.57f, 0), new Vector3(.30f, .35f, .30f), ten);
        for (int taraf = -1; taraf <= 1; taraf += 2)
            Parca(kok, "Bacak", PrimitiveType.Capsule, new Vector3(taraf * .13f, .38f, 0), new Vector3(.18f, .38f, .20f), pantolon);
        var sol = SeyirciKolu(kok, -1, tisort);
        var sag = SeyirciKolu(kok, 1, tisort);
        if (sira % 3 == 0 && sapka != null)
        {
            Parca(kok, "Sapka", PrimitiveType.Sphere, new Vector3(0, 1.73f, 0), new Vector3(.33f, .15f, .33f), sapka);
            Parca(kok, "Siper", PrimitiveType.Cube, new Vector3(0, 1.71f, .15f), new Vector3(.32f, .035f, .24f), sapka);
        }
        return new Seyirci { kok = kok, solKol = sol, sagKol = sag };
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
