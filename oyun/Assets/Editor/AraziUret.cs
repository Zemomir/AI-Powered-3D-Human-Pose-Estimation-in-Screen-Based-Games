using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AraziUret
{
    public static Terrain Kur(Yol yol, Texture2D cim, Texture2D toprak)
    {
        Directory.CreateDirectory("Assets/Arazi");
        AssetDatabase.Refresh();
        const int coz = 513;
        var noktalar = Ornekle(yol.kontrol, yol.adim);
        var bas = new Vector3(30 - 350, -.05f, 170 - 350);
        var veri = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/Arazi/Vadi.asset");
        if (veri == null)
        {
            veri = new TerrainData();
            AssetDatabase.CreateAsset(veri, "Assets/Arazi/Vadi.asset");
        }
        veri.heightmapResolution = coz;
        veri.size = new Vector3(700, 100, 700);
        veri.alphamapResolution = 512;
        veri.terrainLayers = new[] { Katman("Cim", cim, 12), Katman("Toprak", toprak, 8) };
        var yukseklik = new float[coz, coz];
        for (int z = 0; z < coz; z++)
        for (int x = 0; x < coz; x++)
        {
            Vector3 p = bas + new Vector3(x * 700f / (coz - 1), 0, z * 700f / (coz - 1));
            float kenardan = Uzaklik(p, noktalar) - yol.genislik / 2;
            float gecis = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(17, 45, kenardan));
            float dag = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(45, 230, kenardan));
            float dalga = Mathf.PerlinNoise(42 + p.x * .006f, 83 + p.z * .006f);
            float ince = Mathf.PerlinNoise(91 + p.x * .017f, 28 + p.z * .017f);
            float metre = gecis * (3 + dalga * 13 + ince * 3 + dag * (35 + dalga * 25));
            yukseklik[z, x] = metre / 100f;
        }
        veri.SetHeights(0, 0, yukseklik);
        int a = veri.alphamapResolution;
        var boya = new float[a, a, 2];
        for (int z = 0; z < a; z++)
        for (int x = 0; x < a; x++)
        {
            float u = x / (float)(a - 1), v = z / (float)(a - 1);
            float kenar = Uzaklik(bas + new Vector3(u * 700, 0, v * 700), noktalar) - yol.genislik / 2;
            float omuz = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1, 3, kenar));
            float egim = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(22, 40, veri.GetSteepness(u, v)));
            float oran = Mathf.Max(omuz, egim);
            boya[z, x, 0] = 1 - oran;
            boya[z, x, 1] = oran;
        }
        veri.SetAlphamaps(0, 0, boya);
        EditorUtility.SetDirty(veri);
        var go = Terrain.CreateTerrainGameObject(veri);
        go.name = "Vadi";
        go.transform.position = bas;
        var arazi = go.GetComponent<Terrain>();
        arazi.heightmapPixelError = 5;
        arazi.basemapDistance = 350;
        arazi.drawTreesAndFoliage = false;
        return arazi;
    }

    static TerrainLayer Katman(string ad, Texture2D doku, float boy)
    {
        string yol = "Assets/Arazi/" + ad + ".terrainlayer";
        var katman = AssetDatabase.LoadAssetAtPath<TerrainLayer>(yol);
        if (katman == null)
        {
            katman = new TerrainLayer();
            AssetDatabase.CreateAsset(katman, yol);
        }
        katman.diffuseTexture = doku;
        katman.tileSize = new Vector2(boy, boy);
        katman.metallic = 0;
        katman.smoothness = 0;
        EditorUtility.SetDirty(katman);
        return katman;
    }

    static float Uzaklik(Vector3 p, List<Vector3> noktalar)
    {
        float en = float.MaxValue;
        for (int i = 1; i < noktalar.Count; i++)
        {
            Vector3 a = noktalar[i - 1], d = noktalar[i] - a;
            a.y = p.y;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, d) / Mathf.Max(.0001f, d.sqrMagnitude));
            en = Mathf.Min(en, (p - a - d * t).sqrMagnitude);
        }
        return Mathf.Sqrt(en);
    }

    // Yol.Ornekle ile aynı hesap; çalışma zamanı yoluna müdahale etmez.
    static List<Vector3> Ornekle(Vector3[] kontrol, float adim)
    {
        var sonuc = new List<Vector3>();
        for (int i = 1; i < kontrol.Length - 2; i++)
        {
            Vector3 p0 = kontrol[i - 1], p1 = kontrol[i], p2 = kontrol[i + 1], p3 = kontrol[i + 2];
            int parca = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(p1, p2) / adim));
            for (int j = 0; j < parca; j++)
            {
                float t = j / (float)parca, t2 = t * t, t3 = t2 * t;
                sonuc.Add(.5f * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3));
            }
        }
        sonuc.Add(kontrol[kontrol.Length - 2]);
        return sonuc;
    }
}
