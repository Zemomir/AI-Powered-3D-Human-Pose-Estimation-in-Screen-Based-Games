using UnityEngine;

// Sadece başlangıçta kurulur; bütün yol noktalarına mesafe bırakır.
public class Cevre : MonoBehaviour
{
    public Yol yol;
    public Material agacGovde, yaprak, kaya;
    public Material[] yaprakTonlari;
    CevreMesh birlesim;
    Mesh silindir, kure, koni;

    void Start()
    {
        if (yol == null || yol.Noktalar == null || yol.Noktalar.Count < 2) return;
        birlesim = gameObject.AddComponent<CevreMesh>();
        silindir = CevreMesh.Ilkel(PrimitiveType.Cylinder);
        kure = CevreMesh.Ilkel(PrimitiveType.Sphere);
        koni = Koni();
        var rastgele = new System.Random(42);
        Yerlestir(rastgele, 180, true);
        Yerlestir(rastgele, 50, false);
        birlesim.Bitir();
        Destroy(koni);
    }

    void Yerlestir(System.Random r, int adet, bool agac)
    {
        if (agac ? agacGovde == null || yaprak == null : kaya == null) return;
        var arazi = Terrain.activeTerrain;
        int konan = 0;
        for (int deneme = 0; deneme < adet * 100 && konan < adet; deneme++)
        {
            int i = r.Next(yol.Noktalar.Count);
            float taraf = r.Next(2) == 0 ? -1f : 1f;
            float cap = agac ? Aralik(r, 3, 4.8f) : Aralik(r, 1.2f, 2.5f);
            Vector3 p = yol.Noktalar[i] + Vector3.Cross(Vector3.up, yol.Yon(i)).normalized *
                (taraf * (yol.genislik / 2 + Aralik(r, 7 + cap / 2, 60 - cap / 2)));
            float bosluk = yol.genislik / 2 + 7 + cap / 2;
            bool uygun = true;
            foreach (Vector3 nokta in yol.Noktalar)
            {
                Vector3 fark = nokta - p; fark.y = 0;
                if (fark.sqrMagnitude < bosluk * bosluk) { uygun = false; break; }
            }
            if (!uygun) continue;
            p.y = arazi != null ? arazi.SampleHeight(p) + arazi.transform.position.y : 0;
            if (agac)
            {
                float boy = Aralik(r, 4.5f, 7.5f);
                var ton = yaprak;
                if (yaprakTonlari != null && yaprakTonlari.Length > 0)
                {
                    var secilenTon = yaprakTonlari[r.Next(yaprakTonlari.Length)];
                    ton = secilenTon != null ? secilenTon : yaprak;
                }
                bool cam = r.Next(2) == 0;
                float govdeBoy = cam ? boy * .72f : boy * .65f;
                Ekle(silindir, agacGovde, p + Vector3.up * (govdeBoy / 2 - .2f), new Vector3(.38f, govdeBoy / 2 + .2f, .38f));
                if (cam)
                {
                    for (int k = 0; k < 3; k++)
                        Ekle(koni, ton, p + Vector3.up * (boy * (.28f + k * .19f)),
                            new Vector3(cap * (.5f - k * .09f), boy * .4f, cap * (.5f - k * .09f)));
                }
                else
                {
                    int tac = r.Next(3, 6);
                    for (int k = 0; k < tac; k++)
                    {
                        float aci = k * Mathf.PI * 2 / tac;
                        Vector3 kayma = new Vector3(Mathf.Cos(aci) * cap * .18f, boy * .68f + Aralik(r, -.3f, .6f), Mathf.Sin(aci) * cap * .18f);
                        Ekle(kure, ton, p + kayma, new Vector3(cap * .62f, cap * .7f, cap * .62f));
                    }
                }
            }
            else
            {
                float boy = Aralik(r, .7f, 1.4f);
                birlesim.Ekle(kure, kaya, p + Vector3.up * boy * .15f, Quaternion.Euler(0, Aralik(r, 0, 360), 0), new Vector3(cap, boy, cap * .85f));
            }
            konan++;
        }
    }

    void Ekle(Mesh mesh, Material m, Vector3 p, Vector3 olcek) => birlesim.Ekle(mesh, m, p, Quaternion.identity, olcek);

    static Mesh Koni()
    {
        const int dilim = 12;
        var v = new Vector3[(dilim + 1) * 2 + dilim + 2];
        var uv = new Vector2[v.Length];
        var t = new int[dilim * 6];
        for (int i = 0; i <= dilim; i++)
        {
            float aci = i * Mathf.PI * 2 / dilim;
            v[i * 2] = new Vector3(Mathf.Cos(aci), 0, Mathf.Sin(aci));
            v[i * 2 + 1] = Vector3.up;
            uv[i * 2] = new Vector2(i / (float)dilim, 0);
            uv[i * 2 + 1] = new Vector2(i / (float)dilim, 1);
            int kapak = (dilim + 1) * 2;
            v[kapak + i] = v[i * 2];
            if (i == dilim) continue;
            int k = i * 6;
            t[k] = i * 2; t[k + 1] = i * 2 + 1; t[k + 2] = i * 2 + 2;
            t[k + 3] = kapak + dilim + 1; t[k + 4] = kapak + i; t[k + 5] = kapak + i + 1;
        }
        v[(dilim + 1) * 2 + dilim + 1] = Vector3.zero;
        var mesh = new Mesh { name = "CamKoni", vertices = v, uv = uv, triangles = t };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static float Aralik(System.Random r, float az, float cok) => az + (float)r.NextDouble() * (cok - az);
}
