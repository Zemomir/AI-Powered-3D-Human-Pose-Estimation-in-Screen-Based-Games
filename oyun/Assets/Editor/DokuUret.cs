using System.IO;
using UnityEditor;
using UnityEngine;

// Sabit tohumlu, kenarlarda kesintisiz editör dokuları.
public static class DokuUret
{
    public static Texture2D Uret(string ad, int boy, int tur)
    {
        Directory.CreateDirectory("Assets/Dokular");
        var doku = new Texture2D(boy, boy, TextureFormat.RGB24, false);
        var renkler = new Color[boy * boy];
        var r = new System.Random(4200 + tur);
        for (int y = 0; y < boy; y++)
        for (int x = 0; x < boy; x++)
        {
            float u = x / (float)boy, v = y / (float)boy;
            float buyuk = Gurultu(u, v, 4, 31 + tur * 19);
            float kucuk = Gurultu(u, v, 32, 71 + tur * 13);
            float tane = (float)r.NextDouble() - 0.5f;
            Color c;
            if (tur == 0)
                c = Color.Lerp(new Color(.19f, .20f, .21f), new Color(.28f, .29f, .30f), buyuk) + Color.white * (tane * .055f + (kucuk - .5f) * .035f);
            else if (tur == 1)
                c = Color.Lerp(new Color(.23f, .32f, .13f), new Color(.40f, .48f, .24f), buyuk * .7f + kucuk * .3f) + Color.white * tane * .035f;
            else if (tur == 2)
                c = Color.Lerp(new Color(.35f, .27f, .18f), new Color(.58f, .48f, .33f), buyuk * .6f + kucuk * .4f) + Color.white * tane * .05f;
            else
            {
                float cizgi = .5f + .5f * Mathf.Sin(u * Mathf.PI * 48 + Gurultu(u, v, 4, 98) * 5);
                c = Color.Lerp(new Color(.19f, .12f, .07f), new Color(.43f, .30f, .18f), cizgi * .65f + kucuk * .35f) + Color.white * tane * .025f;
            }
            renkler[y * boy + x] = c;
        }
        doku.SetPixels(renkler);
        doku.Apply();
        string yol = "Assets/Dokular/" + ad + ".png";
        File.WriteAllBytes(yol, doku.EncodeToPNG());
        Object.DestroyImmediate(doku);
        AssetDatabase.ImportAsset(yol, ImportAssetOptions.ForceSynchronousImport);
        var aktarici = (TextureImporter)AssetImporter.GetAtPath(yol);
        aktarici.wrapMode = TextureWrapMode.Repeat;
        aktarici.mipmapEnabled = true;
        aktarici.filterMode = FilterMode.Trilinear;
        aktarici.anisoLevel = 4;
        aktarici.textureCompression = TextureImporterCompression.Uncompressed;
        aktarici.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(yol);
    }

    static float Gurultu(float u, float v, float olcek, float tohum)
    {
        // Dört Perlin alanını karıştırarak iki eksende tekrar sağlar.
        float a = Mathf.PerlinNoise(tohum + u * olcek, tohum + v * olcek);
        float b = Mathf.PerlinNoise(tohum + (u - 1) * olcek, tohum + v * olcek);
        float c = Mathf.PerlinNoise(tohum + u * olcek, tohum + (v - 1) * olcek);
        float d = Mathf.PerlinNoise(tohum + (u - 1) * olcek, tohum + (v - 1) * olcek);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }
}
