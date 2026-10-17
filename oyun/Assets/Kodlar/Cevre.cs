using UnityEngine;

// Sadece başlangıçta kurulur; yolun bütün kıvrımlarına mesafe bırakır.
public class Cevre : MonoBehaviour
{
    public Yol yol;
    public Material agacGovde, yaprak, kaya;

    void Start()
    {
        if (yol == null || yol.Noktalar == null || yol.Noktalar.Count < 2) return;
        var rastgele = new System.Random(42);
        Yerlestir(rastgele, 120, true);
        Yerlestir(rastgele, 40, false);
    }

    void Yerlestir(System.Random r, int adet, bool agac)
    {
        if (agac ? agacGovde == null || yaprak == null : kaya == null) return;
        int konan = 0;
        for (int deneme = 0; deneme < adet * 100 && konan < adet; deneme++)
        {
            int i = r.Next(yol.Noktalar.Count);
            float taraf = r.Next(2) == 0 ? -1f : 1f;
            Vector3 p = yol.Noktalar[i] + Vector3.Cross(Vector3.up, yol.Yon(i)).normalized *
                (taraf * Aralik(r, 7f, 30f));
            float cap = agac ? Aralik(r, 2.2f, 3.4f) : Aralik(r, 1.2f, 2.5f);
            // Taç ve kaya kenarları da güvenli aralıkta kalsın.
            float bosluk = yol.genislik / 2f + 3f + cap / 2f;
            bool uygun = true;
            foreach (Vector3 nokta in yol.Noktalar)
            {
                Vector3 fark = nokta - p; fark.y = 0;
                if (fark.sqrMagnitude < bosluk * bosluk) { uygun = false; break; }
            }
            if (!uygun) continue;
            if (agac)
            {
                float boy = Aralik(r, 2.6f, 3.8f);
                Parca(PrimitiveType.Cylinder, "AgacGovde", p + Vector3.up * boy / 2,
                    new Vector3(0.4f, boy / 2, 0.4f), agacGovde);
                Parca(PrimitiveType.Sphere, "Tac", p + Vector3.up * boy,
                    new Vector3(cap, cap * 0.9f, cap), yaprak);
            }
            else
            {
                float boy = Aralik(r, 0.5f, 1f);
                var tas = Parca(PrimitiveType.Sphere, "Kaya", p + Vector3.up * boy / 2,
                    new Vector3(cap, boy, cap * 0.85f), kaya);
                tas.Rotate(0, Aralik(r, 0, 360), 0);
            }
            konan++;
        }
    }

    Transform Parca(PrimitiveType tip, string ad, Vector3 p, Vector3 olcek, Material m)
    {
        var go = GameObject.CreatePrimitive(tip);
        go.name = ad;
        var carpisma = go.GetComponent<Collider>();
        carpisma.enabled = false;
        Destroy(carpisma);
        go.transform.SetParent(transform, false);
        go.transform.position = p;
        go.transform.localScale = olcek;
        go.GetComponent<MeshRenderer>().sharedMaterial = m;
        return go.transform;
    }

    static float Aralik(System.Random r, float az, float cok) => az + (float)r.NextDouble() * (cok - az);
}
