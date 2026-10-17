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
        var noktalar = Yol.Ornekle(yol.kontrol, yol.adim);
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var nokta in noktalar) { minX = Mathf.Min(minX, nokta.x); maxX = Mathf.Max(maxX, nokta.x); minZ = Mathf.Min(minZ, nokta.z); maxZ = Mathf.Max(maxZ, nokta.z); }
        float kenar = Mathf.Max(maxX - minX, maxZ - minZ) + 440f;
        var bas = new Vector3((minX + maxX - kenar) / 2, -.15f, (minZ + maxZ - kenar) / 2);
        var veri = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/Arazi/Vadi.asset");
        if (veri == null)
        {
            veri = new TerrainData();
            AssetDatabase.CreateAsset(veri, "Assets/Arazi/Vadi.asset");
        }
        veri.heightmapResolution = coz;
        veri.size = new Vector3(kenar, 100, kenar);
        veri.alphamapResolution = 512;
        veri.terrainLayers = new[] { Katman("Cim", cim, 12), Katman("Toprak", toprak, 8) };
        var yukseklik = new float[coz, coz];
        for (int z = 0; z < coz; z++)
        for (int x = 0; x < coz; x++)
        {
            Vector3 p = bas + new Vector3(x * kenar / (coz - 1), 0, z * kenar / (coz - 1));
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
        var boyaMesafesi = new float[a, a];
        for (int z = 0; z < a; z++)
        for (int x = 0; x < a; x++)
        {
            float u = x / (float)(a - 1), v = z / (float)(a - 1);
            boyaMesafesi[z, x] = Uzaklik(bas + new Vector3(u * kenar, 0, v * kenar), noktalar) - yol.genislik / 2;
        }
        for (int z = 0; z < a; z++)
        for (int x = 0; x < a; x++)
        {
            float u = x / (float)(a - 1), v = z / (float)(a - 1);
            float mesafe = boyaMesafesi[z, x];
            float omuz = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1, 3, mesafe));
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

}
