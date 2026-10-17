using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Sadece editörde kurulur; çalışma anında Motor yalnız gövdeyi ve iki aksı döndürür.
public static class MotorModeli
{
    public static void Kur(Transform govde, out Transform onTeker, out Transform arkaTeker)
    {
        var boya = Malzeme("Boya", new Color(.68f, .075f, .055f), .12f, .55f);
        var lastik = Malzeme("Lastik", new Color(.035f, .038f, .042f), 0, .08f);
        var siyah = Malzeme("Doseme", new Color(.055f, .06f, .068f), 0, .18f);
        var metal = Malzeme("Metal", new Color(.64f, .67f, .70f), .8f, .7f);
        var blok = Malzeme("Blok", new Color(.18f, .20f, .22f), .55f, .32f);
        var mont = Malzeme("Mont", new Color(.22f, .32f, .41f), 0, .22f);
        var pantolon = Malzeme("Pantolon", new Color(.12f, .16f, .21f), 0, .15f);
        var kask = Malzeme("Kask", new Color(.84f, .88f, .90f), .05f, .5f);
        var vizor = Malzeme("Vizor", new Color(.025f, .055f, .075f), .25f, .72f);
        var far = Malzeme("Far", new Color(.85f, .88f, .82f), 0, .35f,
            new Color(.10f, .105f, .09f));
        var stop = Malzeme("Stop", new Color(.65f, .025f, .018f), 0, .35f,
            new Color(.12f, .003f, .002f));

        // 1,4 m aks aralığı; toplam uzunluk 2,15 m, gidon genişliği 0,8 m.
        onTeker = Teker(govde, "OnTeker", .70f, lastik, metal, blok);
        arkaTeker = Teker(govde, "ArkaTeker", -.70f, lastik, metal, blok);

        Parca(PrimitiveType.Sphere, govde, "Depo", new Vector3(0, .92f, .18f), new Vector3(.43f, .34f, .64f), boya);
        Parca(PrimitiveType.Cylinder, govde, "DepoKapagi", new Vector3(0, 1.089f, .18f), new Vector3(.085f, .008f, .085f), metal);
        Parca(PrimitiveType.Sphere, govde, "Sele", new Vector3(0, .87f, -.31f), new Vector3(.37f, .14f, .66f), siyah);
        Parca(PrimitiveType.Cube, govde, "MotorBlogu", new Vector3(0, .52f, .02f), new Vector3(.36f, .34f, .43f), blok);
        Parca(PrimitiveType.Sphere, govde, "ArkaCamurluk", new Vector3(0, .86f, -.73f), new Vector3(.30f, .13f, .68f), boya);
        Parca(PrimitiveType.Sphere, govde, "OnCamurluk", new Vector3(0, .83f, .73f), new Vector3(.25f, .10f, .64f), boya);

        for (int taraf = -1; taraf <= 1; taraf += 2)
        {
            string ad = taraf < 0 ? "Sol" : "Sag";
            Cubuk(govde, ad + "Sasi", new Vector3(taraf * .17f, .40f, -.30f), new Vector3(taraf * .14f, .95f, .40f), .045f, blok);
            Cubuk(govde, ad + "ArkaMasa", new Vector3(taraf * .13f, .40f, -.70f), new Vector3(taraf * .17f, .48f, -.05f), .045f, blok);
            Cubuk(govde, ad + "OnCatal", new Vector3(taraf * .13f, .40f, .70f), new Vector3(taraf * .13f, 1.09f, .45f), .045f, metal);
            Cubuk(govde, ad + "Elcik", new Vector3(taraf * .27f, 1.13f, .50f), new Vector3(taraf * .40f, 1.13f, .50f), .045f, siyah);
            Cubuk(govde, ad + "Basamak", new Vector3(taraf * .18f, .36f, -.12f), new Vector3(taraf * .36f, .36f, -.12f), .045f, siyah);
        }
        Cubuk(govde, "Gidon", new Vector3(-.35f, 1.13f, .50f), new Vector3(.35f, 1.13f, .50f), .035f, metal);
        Parca(PrimitiveType.Sphere, govde, "FarYuvasi", new Vector3(0, 1.01f, .62f), new Vector3(.23f, .22f, .20f), blok);
        Parca(PrimitiveType.Sphere, govde, "OnFar", new Vector3(0, 1.01f, .706f), new Vector3(.19f, .18f, .045f), far);
        Parca(PrimitiveType.Cube, govde, "StopYuvasi", new Vector3(0, .83f, -1.015f), new Vector3(.23f, .12f, .08f), siyah);
        Parca(PrimitiveType.Cube, govde, "ArkaStop", new Vector3(0, .84f, -1.06f), new Vector3(.19f, .065f, .025f), stop);
        Cubuk(govde, "Egzoz", new Vector3(.29f, .43f, -.22f), new Vector3(.29f, .49f, -.91f), .12f, metal);
        Cubuk(govde, "EgzozAgzi", new Vector3(.29f, .49f, -.91f), new Vector3(.29f, .494f, -.94f), .075f, siyah);

        var surucu = new GameObject("Surucu").transform;
        surucu.SetParent(govde, false);
        Parca(PrimitiveType.Sphere, surucu, "Kalca", new Vector3(0, 1.00f, -.30f), new Vector3(.34f, .23f, .30f), pantolon);
        Uzuv(surucu, "MontGovde", new Vector3(0, 1.08f, -.26f), new Vector3(0, 1.39f, -.05f), .30f, mont);
        Uzuv(surucu, "Boyunluk", new Vector3(0, 1.47f, .015f), new Vector3(0, 1.55f, .04f), .12f, siyah);
        Parca(PrimitiveType.Sphere, surucu, "Kask", new Vector3(0, 1.69f, .055f), new Vector3(.35f, .39f, .36f), kask);
        Parca(PrimitiveType.Sphere, surucu, "Vizor", new Vector3(0, 1.71f, .205f), new Vector3(.30f, .17f, .09f), vizor);
        for (int taraf = -1; taraf <= 1; taraf += 2)
        {
            string ad = taraf < 0 ? "Sol" : "Sag";
            var omuz = new Vector3(taraf * .16f, 1.40f, -.04f);
            var dirsek = new Vector3(taraf * .28f, 1.22f, .18f);
            var el = new Vector3(taraf * .33f, 1.13f, .50f);
            Uzuv(surucu, ad + "UstKol", omuz, dirsek, .115f, mont);
            Uzuv(surucu, ad + "AltKol", dirsek, el, .095f, mont);
            Parca(PrimitiveType.Sphere, surucu, ad + "Eldiven", el, new Vector3(.10f, .085f, .12f), siyah);
            var kalca = new Vector3(taraf * .13f, 1.00f, -.30f);
            var diz = new Vector3(taraf * .265f, .73f, .12f);
            var bilek = new Vector3(taraf * .29f, .44f, -.18f);
            Uzuv(surucu, ad + "UstBacak", kalca, diz, .15f, pantolon);
            Uzuv(surucu, ad + "AltBacak", diz, bilek, .115f, pantolon);
            Parca(PrimitiveType.Capsule, surucu, ad + "Bot", new Vector3(taraf * .29f, .425f, -.10f), new Vector3(.13f, .14f, .13f), siyah, new Vector3(90, 0, 0));
        }
    }

    static Transform Teker(Transform govde, string ad, float z, Material lastik, Material metal, Material blok)
    {
        var kok = new GameObject(ad).transform;
        kok.SetParent(govde, false);
        kok.localPosition = new Vector3(0, .40f, z);
        // Ölçek kökte uygulanmaz. Yerel Y -> gövdenin -X'i: Motor.LateUpdate aksı.
        kok.localRotation = Quaternion.Euler(0, 0, 90);
        var lastikGo = new GameObject("Lastik", typeof(MeshFilter), typeof(MeshRenderer));
        lastikGo.transform.SetParent(kok, false);
        lastikGo.GetComponent<MeshFilter>().sharedMesh = LastikMesh();
        var lastikRenderer = lastikGo.GetComponent<MeshRenderer>();
        lastikRenderer.sharedMaterial = lastik;
        lastikRenderer.shadowCastingMode = ShadowCastingMode.On;
        lastikRenderer.receiveShadows = true;
        // İnce diskler iki yanakta lastiğin dışında; kollar ve göbek ikisinden de görünür.
        for (int yan = -1; yan <= 1; yan += 2)
            Parca(PrimitiveType.Cylinder, kok, "Jant" + yan, new Vector3(0, yan * .096f, 0), new Vector3(.54f, .006f, .54f), metal);
        Parca(PrimitiveType.Cylinder, kok, "Gobek", Vector3.zero, new Vector3(.13f, .12f, .13f), blok);
        for (int i = 0; i < 5; i++)
        {
            float aci = i * 72f;
            var donus = Quaternion.Euler(0, aci, 0);
            Parca(PrimitiveType.Cube, kok, "JantKolu" + i, donus * new Vector3(.145f, 0, 0),
                new Vector3(.23f, .22f, .028f), metal, new Vector3(0, aci, 0));
        }
        return kok;
    }

    static Mesh LastikMesh()
    {
        // Silindirik lastiğin yuvarlatılmış omuzları: 35 derece yatışta bile y >= 0.
        // x yarıçap, y aks boyunca uzaklık. En büyük yarıçap tam 0,375 m.
        var profil = new[]
        {
            new Vector2(.265f, -.085f), new Vector2(.33f, -.085f),
            new Vector2(.36f, -.055f), new Vector2(.375f, -.020f),
            new Vector2(.375f, .020f), new Vector2(.36f, .055f),
            new Vector2(.33f, .085f), new Vector2(.265f, .085f)
        };
        const int dilim = 32;
        var noktalar = new Vector3[profil.Length * dilim];
        var ucgenler = new int[profil.Length * dilim * 6];
        for (int p = 0; p < profil.Length; p++)
        {
            for (int i = 0; i < dilim; i++)
            {
                float aci = i * Mathf.PI * 2 / dilim;
                int a = p * dilim + i;
                int b = p * dilim + (i + 1) % dilim;
                int c = ((p + 1) % profil.Length) * dilim + i;
                int d = ((p + 1) % profil.Length) * dilim + (i + 1) % dilim;
                noktalar[a] = new Vector3(Mathf.Cos(aci) * profil[p].x, profil[p].y,
                    Mathf.Sin(aci) * profil[p].x);
                int t = a * 6;
                ucgenler[t] = a; ucgenler[t + 1] = c; ucgenler[t + 2] = b;
                ucgenler[t + 3] = b; ucgenler[t + 4] = c; ucgenler[t + 5] = d;
            }
        }
        // Diskte tutulur ki sahne kaydı/derleme sonrası da iki teker aynı mesh'i kullansın.
        const string yol = "Assets/Malzemeler/MotorModeli_Lastik.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(yol);
        bool yeni = mesh == null;
        if (yeni) mesh = new Mesh { name = "MotorLastik" };
        else mesh.Clear();
        mesh.vertices = noktalar;
        mesh.triangles = ucgenler;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        if (yeni) AssetDatabase.CreateAsset(mesh, yol);
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    static void Cubuk(Transform ust, string ad, Vector3 bas, Vector3 son, float cap, Material m)
    {
        var t = Parca(PrimitiveType.Cylinder, ust, ad, (bas + son) * .5f,
            new Vector3(cap, Vector3.Distance(bas, son) * .5f, cap), m);
        t.localRotation = Quaternion.FromToRotation(Vector3.up, son - bas);
    }

    static void Uzuv(Transform ust, string ad, Vector3 bas, Vector3 son, float cap, Material m)
    {
        // Uçlar eklem merkezleri: yarım küreler dirsek/diz bağlantısını kapatır.
        var t = Parca(PrimitiveType.Capsule, ust, ad, (bas + son) * .5f,
            new Vector3(cap, (Vector3.Distance(bas, son) + cap) * .5f, cap), m);
        t.localRotation = Quaternion.FromToRotation(Vector3.up, son - bas);
    }

    static Transform Parca(PrimitiveType tip, Transform ust, string ad, Vector3 konum, Vector3 olcek,
        Material m, Vector3 aci = default)
    {
        var go = GameObject.CreatePrimitive(tip);
        go.name = ad;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        var t = go.transform;
        t.SetParent(ust, false);
        t.localPosition = konum;
        t.localRotation = Quaternion.Euler(aci);
        t.localScale = olcek;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = m;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        return t;
    }

    static Material Malzeme(string ad, Color renk, float metalik, float parlaklik, Color emisyon = default)
    {
        const string klasor = "Assets/Malzemeler";
        if (!AssetDatabase.IsValidFolder(klasor)) AssetDatabase.CreateFolder("Assets", "Malzemeler");
        string yol = klasor + "/MotorModeli_" + ad + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(yol);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, yol);
        }
        m.color = renk;
        m.SetFloat("_Metallic", metalik);
        m.SetFloat("_Glossiness", parlaklik);
        m.SetColor("_EmissionColor", emisyon);
        if (emisyon.maxColorComponent > 0) m.EnableKeyword("_EMISSION");
        else m.DisableKeyword("_EMISSION");
        // Sabit, düşük emisyon; ek ışık veya yanıp sönme yok.
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(m);
        return m;
    }
}
