using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Bölge ve malzemeye göre birleştirir; gölgeler ve bölgesel eleme korunur.
public class CevreMesh : MonoBehaviour
{
    class Grup
    {
        public Material malzeme;
        public readonly List<CombineInstance> parcalar = new List<CombineInstance>();
    }
    readonly Dictionary<string, Grup> gruplar = new Dictionary<string, Grup>();
    readonly List<Mesh> uretilen = new List<Mesh>();

    public static Mesh Ilkel(PrimitiveType tur)
    {
        var go = GameObject.CreatePrimitive(tur);
        var mesh = go.GetComponent<MeshFilter>().sharedMesh;
        go.SetActive(false);
        Object.Destroy(go);
        return mesh;
    }

    public void Ekle(Mesh mesh, Material m, Vector3 p, Quaternion donus, Vector3 olcek)
    {
        if (mesh == null || m == null) return;
        string anahtar = m.GetInstanceID() + ":" + Mathf.FloorToInt(p.x / 60) + ":" + Mathf.FloorToInt(p.z / 60);
        if (!gruplar.TryGetValue(anahtar, out var grup))
        {
            grup = new Grup { malzeme = m };
            gruplar.Add(anahtar, grup);
        }
        grup.parcalar.Add(new CombineInstance { mesh = mesh, transform = transform.worldToLocalMatrix * Matrix4x4.TRS(p, donus, olcek) });
    }

    public void Bitir()
    {
        foreach (var grup in gruplar.Values)
        {
            var mesh = new Mesh { name = "CevreBirlesik", indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(grup.parcalar.ToArray(), true, true);
            uretilen.Add(mesh);
            var go = new GameObject("Birlesik_" + grup.malzeme.name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var cizici = go.GetComponent<MeshRenderer>();
            cizici.sharedMaterial = grup.malzeme;
            cizici.shadowCastingMode = ShadowCastingMode.On;
            cizici.receiveShadows = true;
        }
        gruplar.Clear();
    }

    void OnDestroy()
    {
        foreach (var mesh in uretilen) if (mesh != null) Destroy(mesh);
    }
}
