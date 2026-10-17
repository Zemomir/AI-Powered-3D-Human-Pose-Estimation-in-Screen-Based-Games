using System.Collections.Generic;
using UnityEngine;

// Aynı yol, aynı hedefler: üç sol, üç orta, üç sağ.
public class Yildizlar : MonoBehaviour
{
    public Yol yol;
    public Motor motor;
    public Ses ses;
    public Material malzeme;

    public int Toplanan { get; private set; }
    public int Toplam => yildizlar.Count;
    public int SolToplanan { get; private set; }
    public int OrtaToplanan { get; private set; }
    public int SagToplanan { get; private set; }
    public int SolToplam { get; private set; }
    public int OrtaToplam { get; private set; }
    public int SagToplam { get; private set; }
    public string KareOlayi { get; private set; } = "";

    class Yildiz
    {
        public Transform gorsel;
        public Vector3 konum;
        public int taraf;
        public bool toplandi;
        public float toplamaZamani;
    }
    class Parca
    {
        public Transform transform;
        public Vector3 hiz;
    }
    const int EfektSayisi = 4;
    const int ParcaSayisi = 6;
    readonly Parca[][] parcalar = new Parca[EfektSayisi][];
    readonly float[] efektZamanlari = new float[EfektSayisi];
    readonly bool[] efektAktif = new bool[EfektSayisi];
    readonly List<Yildiz> yildizlar = new List<Yildiz>();
    Mesh yildizMesh;
    Material yildizMalzemesi;
    int sonrakiEfekt;

    void OnEnable() { if (motor != null) motor.Sifirlandi += Sifirla; }
    void OnDisable() { if (motor != null) motor.Sifirlandi -= Sifirla; }

    void Start()
    {
        if (yol == null || yol.Noktalar == null || yol.Noktalar.Count < 2 || malzeme == null) return;
        yildizMesh = YildizMeshiOlustur();
        yildizMalzemesi = new Material(malzeme);
        yildizMalzemesi.color = new Color(1f, 0.80f, 0.22f);
        if (yildizMalzemesi.HasProperty("_Metallic")) yildizMalzemesi.SetFloat("_Metallic", 0.7f);
        if (yildizMalzemesi.HasProperty("_Glossiness")) yildizMalzemesi.SetFloat("_Glossiness", 0.8f);
        if (yildizMalzemesi.HasProperty("_EmissionColor"))
        {
            yildizMalzemesi.EnableKeyword("_EMISSION");
            yildizMalzemesi.SetColor("_EmissionColor", new Color(0.30f, 0.21f, 0.03f));
        }
        int sira = 0;
        int ilk = yol.BaslangicIndeksi + 30, son = yol.Noktalar.Count - 15;
        // Desen 9 yıldızda bir tamamlanır (3 sol, 3 orta, 3 sağ); yalnız tam desenler konur ki
        // sol ve sağ yıldız sayısı eşit olsun: taraf karşılaştırması fizyoterapist için veri.
        int adet = Mathf.Max(0, (son - ilk + 11) / 12) / 9 * 9;
        for (int i = ilk; i < son && sira < adet; i += 12)
        {
            int taraf = (sira / 3) % 3;
            // Cross(up, ileri) sağdır; negatif değer sürücünün soludur.
            Vector3 yan = Vector3.Cross(Vector3.up, yol.Yon(i)).normalized;
            Vector3 konum = yol.Noktalar[i] + yan * ((taraf - 1) * 2.2f) + Vector3.up * 1.2f;
            var go = new GameObject("Yildiz_" + sira);
            go.name = "Yildiz_" + sira;
            go.transform.SetParent(transform, false);
            go.transform.position = konum;
            go.transform.localScale = Vector3.one;
            go.transform.rotation = Quaternion.identity;
            var filtre = go.AddComponent<MeshFilter>();
            filtre.sharedMesh = yildizMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = yildizMalzemesi;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = false;
            yildizlar.Add(new Yildiz { gorsel = go.transform, konum = konum, taraf = taraf });
            if (taraf == 0) SolToplam++;
            else if (taraf == 1) OrtaToplam++;
            else SagToplam++;
            sira++;
        }
        for (int e = 0; e < EfektSayisi; e++)
        {
            parcalar[e] = new Parca[ParcaSayisi];
            for (int p = 0; p < ParcaSayisi; p++)
            {
                var go = new GameObject("Yildiz_Parcasi");
                go.transform.SetParent(transform, false);
                go.SetActive(false);
                var filtre = go.AddComponent<MeshFilter>();
                filtre.sharedMesh = yildizMesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = yildizMalzemesi;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = false;
                parcalar[e][p] = new Parca { transform = go.transform };
            }
        }
    }

    void Update()
    {
        KareOlayi = "";
        for (int i = 0; i < yildizlar.Count; i++)
        {
            var y = yildizlar[i];
            if (y.gorsel == null) continue;
            if (y.toplandi)
            {
                float t = Time.time - y.toplamaZamani;
                if (t < 0.12f)
                {
                    float q = t / 0.12f;
                    y.gorsel.position = y.konum + Vector3.up * (0.12f * Mathf.Sin(y.toplamaZamani * 1.7f + i) + 0.4f * q);
                    y.gorsel.localScale = Vector3.one * (1f + 0.5f * q);
                }
                else if (t < 0.35f)
                {
                    float q = (t - 0.12f) / 0.23f;
                    y.gorsel.position = y.konum + Vector3.up * (0.12f * Mathf.Sin(y.toplamaZamani * 1.7f + i) + 0.4f);
                    y.gorsel.localScale = Vector3.one * (1.5f * (1f - q));
                }
                else goYildizGizle(y);
                continue;
            }
            y.gorsel.position = y.konum + Vector3.up * (0.12f * Mathf.Sin(Time.time * 1.7f + i));
            y.gorsel.Rotate(Vector3.up, 90f * Time.deltaTime, Space.World);
            if (motor == null || motor.Hal != Motor.Durum.Suruyor) continue;
            Vector3 fark = motor.transform.position - y.konum;
            fark.y = 0;
            if (fark.sqrMagnitude >= 1.4f * 1.4f) continue;
            y.toplandi = true;
            y.toplamaZamani = Time.time;
            EfektBaslat(y.gorsel.position);
            Toplanan++;
            string olay;
            if (y.taraf == 0) { SolToplanan++; olay = "yildiz_sol"; }
            else if (y.taraf == 1) { OrtaToplanan++; olay = "yildiz_orta"; }
            else { SagToplanan++; olay = "yildiz_sag"; }
            KareOlayi += (KareOlayi.Length == 0 ? "" : " ") + olay;
            if (ses != null) ses.Topla();
        }
        for (int e = 0; e < EfektSayisi; e++)
        {
            if (!efektAktif[e]) continue;
            float t = Time.time - efektZamanlari[e];
            if (t >= 0.5f) { efektAktif[e] = false; foreach (var p in parcalar[e]) p.transform.gameObject.SetActive(false); continue; }
            for (int p = 0; p < ParcaSayisi; p++)
            {
                var parca = parcalar[e][p];
                parca.transform.position += parca.hiz * Time.deltaTime;
                parca.hiz += Vector3.down * (1.5f * Time.deltaTime);
                parca.transform.localScale = Vector3.one * (0.25f * (1f - t / 0.5f));
            }
        }
    }

    void Sifirla()
    {
        Toplanan = SolToplanan = OrtaToplanan = SagToplanan = 0;
        KareOlayi = "";
        foreach (var y in yildizlar)
        {
            y.toplandi = false;
            if (y.gorsel == null) continue;
            y.gorsel.gameObject.SetActive(true);
            y.gorsel.position = y.konum;
            y.gorsel.rotation = Quaternion.identity;
            y.gorsel.localScale = Vector3.one;
            y.toplamaZamani = 0f;
        }
        for (int e = 0; e < EfektSayisi; e++)
        {
            efektAktif[e] = false;
            if (parcalar[e] != null) foreach (var p in parcalar[e]) p.transform.gameObject.SetActive(false);
        }
        sonrakiEfekt = 0;
    }

    void EfektBaslat(Vector3 konum)
    {
        int e = sonrakiEfekt;
        sonrakiEfekt = (sonrakiEfekt + 1) % EfektSayisi;
        efektAktif[e] = true;
        efektZamanlari[e] = Time.time;
        for (int p = 0; p < ParcaSayisi; p++)
        {
            float aci = (360f * p / ParcaSayisi + Random.Range(-12f, 12f)) * Mathf.Deg2Rad;
            Vector3 yon = new Vector3(Mathf.Cos(aci), Random.Range(0.25f, 0.8f), Mathf.Sin(aci)).normalized;
            var parca = parcalar[e][p];
            parca.transform.position = konum;
            parca.transform.rotation = Quaternion.identity;
            parca.transform.localScale = Vector3.one * 0.25f;
            parca.hiz = yon * Random.Range(1.5f, 2.5f);
            parca.transform.gameObject.SetActive(true);
        }
    }

    static void goYildizGizle(Yildiz y)
    {
        y.gorsel.localScale = Vector3.zero;
        y.gorsel.gameObject.SetActive(false);
    }

    static Mesh YildizMeshiOlustur()
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        Vector3[] dis = new Vector3[10];
        Vector3[] on = new Vector3[10];
        Vector3[] arka = new Vector3[10];
        for (int i = 0; i < 10; i++)
        {
            float a = (90f - i * 36f) * Mathf.Deg2Rad;
            float r = i % 2 == 0 ? 0.42f : 0.18f;
            dis[i] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
            on[i] = new Vector3(dis[i].x, dis[i].y, 0.035f);
            arka[i] = new Vector3(dis[i].x, dis[i].y, -0.035f);
        }
        Vector3 onMerkez = new Vector3(0f, 0f, 0.13f), arkaMerkez = new Vector3(0f, 0f, -0.13f);
        for (int i = 0; i < 10; i++)
        {
            int n = (i + 1) % 10;
            UcgenEkle(vertices, triangles, onMerkez, on[i], on[n], Vector3.forward);
            UcgenEkle(vertices, triangles, arkaMerkez, arka[n], arka[i], Vector3.back);
            Vector3 disYon = new Vector3(dis[i].x + dis[n].x, dis[i].y + dis[n].y, 0f).normalized;
            UcgenEkle(vertices, triangles, on[i], arka[i], arka[n], disYon);
            UcgenEkle(vertices, triangles, on[i], arka[n], on[n], disYon);
        }
        var mesh = new Mesh { name = "BesKoseYildiz" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void UcgenEkle(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 beklenenNormal)
    {
        if (Vector3.Dot(Vector3.Cross(b - a, c - a), beklenenNormal) < 0f) { var gecici = b; b = c; c = gecici; }
        int ilk = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c);
        triangles.Add(ilk); triangles.Add(ilk + 1); triangles.Add(ilk + 2);
    }

    void OnDestroy()
    {
        if (yildizMesh != null) Destroy(yildizMesh);
        if (yildizMalzemesi != null) Destroy(yildizMalzemesi);
    }
}
