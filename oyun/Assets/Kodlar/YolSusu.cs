using System.Collections.Generic;
using UnityEngine;

public class YolSusu : MonoBehaviour
{
    public Yol yol;
    public Material beyaz, kirmizi, reflektor;
    readonly List<Mesh> meshler = new List<Mesh>();
    float[] mesafeler;

    void Start()
    {
        if (yol == null || yol.Noktalar == null || yol.Noktalar.Count < 2) return;
        mesafeler = new float[yol.Noktalar.Count];
        for (int i = 1; i < mesafeler.Length; i++)
            mesafeler[i] = mesafeler[i - 1] + Vector3.Distance(yol.Noktalar[i - 1], yol.Noktalar[i]);
        float son = mesafeler[mesafeler.Length - 1];
        var v = new List<Vector3>();
        var t = new List<int>();
        // Çizgiler örnek sınırlarında bölünür; virajı da takip eder.
        for (float bas = 0; bas < son - 2; bas += 6)
        {
            float bit = Mathf.Min(bas + 3, son - 2);
            float s = bas;
            while (s < bit - .0001f)
            {
                float e = Mathf.Min(bit, s + .5f);
                Vector3 a = Konum(s), b = Konum(e);
                Vector3 yan = Vector3.Cross(Vector3.up, (b - a).normalized) * .075f;
                int k = v.Count;
                v.Add(a - yan + Vector3.up * .02f); v.Add(a + yan + Vector3.up * .02f);
                v.Add(b - yan + Vector3.up * .02f); v.Add(b + yan + Vector3.up * .02f);
                t.Add(k); t.Add(k + 2); t.Add(k + 1); t.Add(k + 1); t.Add(k + 2); t.Add(k + 3);
                s = e;
            }
        }
        if (beyaz != null)
        {
            var mesh = new Mesh { name = "KesikliOrta", vertices = v.ToArray(), triangles = t.ToArray() };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); meshler.Add(mesh);
            var go = new GameObject("KesikliOrta", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = beyaz;
        }
        var birlesim = gameObject.AddComponent<CevreMesh>();
        var kup = CevreMesh.Ilkel(PrimitiveType.Cube);
        for (float s = 1; s < son - 2; s += 1)
        {
            Vector3 once = Yon(s - 3), sonra = Yon(s + 3);
            float donus = Vector3.SignedAngle(once, sonra, Vector3.up);
            if (Mathf.Abs(donus) < 3.5f) continue;
            Vector3 yon = Yon(s), yan = Vector3.Cross(Vector3.up, yon);
            Vector3 p = Konum(s) + yan * Mathf.Sign(donus) * (yol.genislik / 2 - .3f);
            birlesim.Ekle(kup, ((int)s % 2 == 0) ? beyaz : kirmizi,
                p + Vector3.up * .055f, Quaternion.LookRotation(yon), new Vector3(.6f, .07f, 1f));
        }
        var arazi = Terrain.activeTerrain;
        for (float s = 12.5f; s < son - 5; s += 25)
        foreach (float taraf in new[] { -1f, 1f })
        {
            Vector3 yon = Yon(s);
            Vector3 p = Konum(s) + Vector3.Cross(Vector3.up, yon) * taraf * (yol.genislik / 2 + 1.5f);
            p.y = arazi != null ? arazi.SampleHeight(p) + arazi.transform.position.y : 0;
            var donus = Quaternion.LookRotation(yon);
            birlesim.Ekle(kup, beyaz, p + Vector3.up * .55f, donus, new Vector3(.12f, 1.2f, .12f));
            birlesim.Ekle(kup, reflektor, p + Vector3.up * 1.02f, donus, new Vector3(.14f, .12f, .14f));
        }
        birlesim.Bitir();
    }

    Vector3 Konum(float s)
    {
        s = Mathf.Clamp(s, 0, mesafeler[mesafeler.Length - 1]);
        int a = 0, b = mesafeler.Length - 1;
        while (b - a > 1)
        {
            int orta = (a + b) / 2;
            if (mesafeler[orta] <= s) a = orta; else b = orta;
        }
        return Vector3.Lerp(yol.Noktalar[a], yol.Noktalar[b], Mathf.InverseLerp(mesafeler[a], mesafeler[b], s));
    }

    Vector3 Yon(float s) => (Konum(s + .5f) - Konum(s - .5f)).normalized;

    void OnDestroy()
    {
        foreach (var mesh in meshler) if (mesh != null) Destroy(mesh);
    }
}
