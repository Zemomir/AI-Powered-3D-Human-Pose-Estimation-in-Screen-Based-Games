using System.Collections.Generic;
using UnityEngine;

// Elle çizilmiş sabit yol. Kontrol noktaları arasından Catmull-Rom eğrisiyle geçer.
// İlk aşamada yol çocuğa göre üretilmiyor (Batın, 2026-10-17: "şimdilik sadece hareket odaklı").
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Yol : MonoBehaviour
{
    public float genislik = 8f;
    public float adim = 1f;  // örnekleme aralığı, metre
    public Material kenarMalzemesi;
    public Material bitisMalzemesi;

    // Yumuşak virajlar: yarıçaplar ~40 m ve üstü, 8-12 m/s hızda rahat dönülür.
    public Vector3[] kontrol =
    {
        new Vector3(0, 0, -40), new Vector3(0, 0, -20), new Vector3(0, 0, 0), new Vector3(0, 0, 40), new Vector3(15, 0, 80),
        new Vector3(40, 0, 105), new Vector3(58, 0, 145), new Vector3(48, 0, 190), new Vector3(18, 0, 222),
        new Vector3(8, 0, 270), new Vector3(28, 0, 318), new Vector3(30, 0, 360), new Vector3(30, 0, 380),
    };

    public float baslangicMesafesi = 20f;  // yol motorun arkasında da devam etsin, kamera boşluğa bakmasın

    public List<Vector3> Noktalar { get; private set; }
    public int BaslangicIndeksi { get; private set; }
    public float ToplamUzunluk { get; private set; }

    void Awake()
    {
        Noktalar = Ornekle(kontrol, adim);
        ToplamUzunluk = 0;
        for (int i = 1; i < Noktalar.Count; i++) ToplamUzunluk += Vector3.Distance(Noktalar[i - 1], Noktalar[i]);
        BaslangicIndeksi = Mathf.Clamp(Mathf.RoundToInt(baslangicMesafesi / adim), 0, Noktalar.Count - 2);
        GetComponent<MeshFilter>().mesh = SeritMesh(Noktalar, -genislik / 2, genislik / 2, 0.01f);
        if (kenarMalzemesi != null)
        {
            Serit("SolKenar", -genislik / 2 + 0.2f, -genislik / 2 + 0.45f, kenarMalzemesi);
            Serit("SagKenar", genislik / 2 - 0.45f, genislik / 2 - 0.2f, kenarMalzemesi);
        }
        if (bitisMalzemesi != null) BitisCizgisi();
    }

    public static List<Vector3> Ornekle(Vector3[] kontrol, float adim)
    {
        // Uç noktalar eğrinin yönünü belirler, yolun kendisi 1. ile sondan 2. nokta arasıdır.
        var sonuc = new List<Vector3>();
        for (int i = 1; i < kontrol.Length - 2; i++)
        {
            Vector3 p0 = kontrol[i - 1], p1 = kontrol[i], p2 = kontrol[i + 1], p3 = kontrol[i + 2];
            int parca = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(p1, p2) / adim));
            for (int j = 0; j < parca; j++) sonuc.Add(CatmullRom(p0, p1, p2, p3, j / (float)parca));
        }
        sonuc.Add(kontrol[kontrol.Length - 2]);
        return sonuc;
    }

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    public Vector3 Yon(int i)
    {
        int a = Mathf.Clamp(i, 0, Noktalar.Count - 2);
        return (Noktalar[a + 1] - Noktalar[a]).normalized;
    }

    // p'ye en yakın yol noktası (indeks) ve yol ortasına yatay uzaklık.
    // ipucu: bir önceki karedeki indeks; sadece çevresine bakılır, motor yolda geri sıçramasın.
    public int EnYakin(Vector3 p, int ipucu, out float uzaklik)
    {
        int bas = Mathf.Max(0, ipucu - 30), son = Mathf.Min(Noktalar.Count - 1, ipucu + 30);
        int enIyi = ipucu;
        float enKucuk = float.MaxValue;
        for (int i = bas; i <= son; i++)
        {
            Vector3 d = Noktalar[i] - p; d.y = 0;
            float m = d.sqrMagnitude;
            if (m < enKucuk) { enKucuk = m; enIyi = i; }
        }
        uzaklik = Mathf.Sqrt(enKucuk);
        return enIyi;
    }

    Mesh SeritMesh(List<Vector3> n, float sol, float sag, float yukseklik)
    {
        var koseler = new Vector3[n.Count * 2];
        var uv = new Vector2[n.Count * 2];
        var ucgen = new int[(n.Count - 1) * 6];
        float yolBoyu = 0;
        for (int i = 0; i < n.Count; i++)
        {
            if (i > 0) yolBoyu += Vector3.Distance(n[i - 1], n[i]);
            Vector3 yan = Vector3.Cross(Vector3.up, Yon(i)).normalized;
            koseler[2 * i] = n[i] + yan * sol + Vector3.up * yukseklik;
            koseler[2 * i + 1] = n[i] + yan * sag + Vector3.up * yukseklik;
            uv[2 * i] = new Vector2(0, yolBoyu / 4f);
            uv[2 * i + 1] = new Vector2(1, yolBoyu / 4f);
        }
        for (int i = 0; i < n.Count - 1; i++)
        {
            int k = i * 6, a = 2 * i;
            ucgen[k] = a; ucgen[k + 1] = a + 2; ucgen[k + 2] = a + 1;
            ucgen[k + 3] = a + 1; ucgen[k + 4] = a + 2; ucgen[k + 5] = a + 3;
        }
        var mesh = new Mesh { vertices = koseler, uv = uv, triangles = ucgen };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    void Serit(string ad, float sol, float sag, Material malzeme)
    {
        var go = new GameObject(ad, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(transform, false);
        go.GetComponent<MeshFilter>().mesh = SeritMesh(Noktalar, sol, sag, 0.02f);
        go.GetComponent<MeshRenderer>().sharedMaterial = malzeme;
    }

    void BitisCizgisi()
    {
        int i = Noktalar.Count - 1;
        Vector3 p = Noktalar[i], yon = Yon(i);
        var cizgi = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cizgi.name = "Bitis";
        Destroy(cizgi.GetComponent<Collider>());
        cizgi.transform.SetParent(transform, false);
        cizgi.transform.position = p + Vector3.up * 0.03f;
        cizgi.transform.rotation = Quaternion.LookRotation(yon);
        cizgi.transform.localScale = new Vector3(genislik, 0.02f, 1.5f);
        cizgi.GetComponent<MeshRenderer>().sharedMaterial = bitisMalzemesi;
        foreach (float taraf in new[] { -1f, 1f })
        {
            var direk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(direk.GetComponent<Collider>());
            direk.transform.SetParent(cizgi.transform.parent, false);
            direk.transform.position = p + Vector3.Cross(Vector3.up, yon) * taraf * (genislik / 2 + 0.6f) + Vector3.up * 1.5f;
            direk.transform.localScale = new Vector3(0.3f, 1.5f, 0.3f);
            direk.GetComponent<MeshRenderer>().sharedMaterial = bitisMalzemesi;
        }
    }
}
