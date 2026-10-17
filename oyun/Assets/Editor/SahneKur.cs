using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Sahneyi koddan kurar ki editörde elle tıklamak gerekmesin ve kurulum tekrarlanabilsin.
// Komut satırı:  Unity.exe -batchmode -quit -projectPath <oyun> -executeMethod SahneKur.Kur
//                Unity.exe -batchmode -quit -projectPath <oyun> -executeMethod SahneKur.Derle
// Editörde: üst menü Fizyo-Oyun > Sahneyi kur / Windows için derle.
public static class SahneKur
{
    const string SAHNE = "Assets/Sahneler/Oyun.unity";
    const string MALZEME_KLASORU = "Assets/Malzemeler";

    [MenuItem("Fizyo-Oyun/Sahneyi kur")]
    public static void Kur()
    {
        Directory.CreateDirectory("Assets/Sahneler");
        Directory.CreateDirectory(MALZEME_KLASORU);
        var sahne = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cim = Malzeme("Cim", new Color(0.30f, 0.48f, 0.27f));
        var asfalt = Malzeme("Asfalt", new Color(0.33f, 0.34f, 0.36f));
        var beyaz = Malzeme("Beyaz", new Color(0.95f, 0.95f, 0.92f));
        var kirmizi = Malzeme("MotorKirmizi", new Color(0.85f, 0.22f, 0.18f));
        var siyah = Malzeme("Lastik", new Color(0.08f, 0.08f, 0.09f));
        var kask = Malzeme("Kask", new Color(0.18f, 0.45f, 0.85f));
        var agacGovde = Malzeme("AgacGovde", new Color(0.36f, 0.26f, 0.17f));
        var yaprak = Malzeme("Yaprak", new Color(0.25f, 0.40f, 0.23f));
        var kaya = Malzeme("Kaya", new Color(0.43f, 0.45f, 0.42f));
        var yildiz = Malzeme("Yildiz", new Color(1f, 0.78f, 0.16f));
        yildiz.EnableKeyword("_EMISSION");
        yildiz.SetColor("_EmissionColor", new Color(0.25f, 0.17f, 0.02f));
        EditorUtility.SetDirty(yildiz);

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.42f, 0.45f, 0.50f);
        var isik = new GameObject("Gunes").AddComponent<Light>();
        isik.type = LightType.Directional;
        isik.intensity = 1.1f;
        isik.color = new Color(1f, 0.96f, 0.88f);
        isik.shadows = LightShadows.Soft;
        isik.transform.rotation = Quaternion.Euler(50, -30, 0);

        var zemin = GameObject.CreatePrimitive(PrimitiveType.Plane);
        zemin.name = "Zemin";
        Object.DestroyImmediate(zemin.GetComponent<Collider>());
        zemin.transform.position = new Vector3(30, -0.01f, 180);
        zemin.transform.localScale = new Vector3(50, 1, 50);  // 500 m x 500 m
        zemin.GetComponent<MeshRenderer>().sharedMaterial = cim;

        var yolGo = new GameObject("Yol", typeof(MeshFilter), typeof(MeshRenderer));
        var yol = yolGo.AddComponent<Yol>();
        yolGo.GetComponent<MeshRenderer>().sharedMaterial = asfalt;
        yol.kenarMalzemesi = beyaz;
        yol.bitisMalzemesi = beyaz;

        var oyun = new GameObject("Oyun");
        var alici = oyun.AddComponent<HareketAlici>();
        oyun.AddComponent<EkranGoruntusu>();

        var motorGo = new GameObject("Motor");
        var motor = motorGo.AddComponent<Motor>();
        motor.alici = alici;
        motor.yol = yol;
        var govde = new GameObject("Govde").transform;
        govde.SetParent(motorGo.transform, false);
        motor.govde = govde;
        Parca(PrimitiveType.Cube, govde, new Vector3(0, 0.75f, 0), new Vector3(0.45f, 0.45f, 1.6f), kirmizi, "Kasa");
        Parca(PrimitiveType.Cylinder, govde, new Vector3(0, 0.4f, 0.75f), new Vector3(0.75f, 0.08f, 0.75f), siyah, "OnTeker", new Vector3(0, 0, 90));
        Parca(PrimitiveType.Cylinder, govde, new Vector3(0, 0.4f, -0.75f), new Vector3(0.75f, 0.08f, 0.75f), siyah, "ArkaTeker", new Vector3(0, 0, 90));
        motor.onTeker = govde.Find("OnTeker");
        motor.arkaTeker = govde.Find("ArkaTeker");
        Parca(PrimitiveType.Capsule, govde, new Vector3(0, 1.35f, -0.15f), new Vector3(0.45f, 0.45f, 0.45f), kirmizi, "Surucu");
        Parca(PrimitiveType.Sphere, govde, new Vector3(0, 1.95f, -0.05f), new Vector3(0.38f, 0.38f, 0.38f), kask, "Kask");

        var ses = oyun.AddComponent<Ses>();
        ses.motor = motor;
        var yildizlar = oyun.AddComponent<Yildizlar>();
        yildizlar.yol = yol;
        yildizlar.motor = motor;
        yildizlar.ses = ses;
        yildizlar.malzeme = yildiz;
        var cevre = new GameObject("Cevre").AddComponent<Cevre>();
        cevre.yol = yol;
        cevre.agacGovde = agacGovde;
        cevre.yaprak = yaprak;
        cevre.kaya = kaya;

        var arayuz = oyun.AddComponent<Arayuz>();
        arayuz.alici = alici;
        arayuz.motor = motor;
        arayuz.yildizlar = yildizlar;
        arayuz.ses = ses;

        var kayit = oyun.AddComponent<OyunKaydi>();
        kayit.alici = alici;
        kayit.motor = motor;
        kayit.yildizlar = yildizlar;

        var kamera = new GameObject("Kamera", typeof(Camera), typeof(AudioListener));
        kamera.tag = "MainCamera";
        var cam = kamera.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.62f, 0.78f, 0.92f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 70f;
        RenderSettings.fogEndDistance = 260f;
        RenderSettings.fogColor = cam.backgroundColor;
        cam.farClipPlane = 600;
        kamera.transform.position = new Vector3(0, 4, -9);
        var takip = kamera.AddComponent<KameraTakip>();
        takip.hedef = motorGo.transform;
        takip.motor = motor;

        EditorSceneManager.SaveScene(sahne, SAHNE);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(SAHNE, true) };

        PlayerSettings.productName = "Fizyo-Oyun";
        PlayerSettings.companyName = "FizyoOyun";
        // Python penceresi öndeyken oyun durmasın: kamera ile oyun aynı anda çalışıyor.
        PlayerSettings.runInBackground = true;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.resizableWindow = true;
        AssetDatabase.SaveAssets();
        Debug.Log("SahneKur: sahne kuruldu -> " + SAHNE);
    }

    [MenuItem("Fizyo-Oyun/Windows için derle")]
    public static void Derle()
    {
        Kur();  // sahne koddan üretiliyor; derlenen her zaman koddaki son hali olsun
        string cikti = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "oyun_derleme", "FizyoOyun.exe"));
        var rapor = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { SAHNE },
            locationPathName = cikti,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        });
        Debug.Log($"SahneKur: derleme {rapor.summary.result}, {rapor.summary.totalErrors} hata -> {cikti}");
        if (rapor.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    static Material Malzeme(string ad, Color renk)
    {
        string yol = $"{MALZEME_KLASORU}/{ad}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(yol);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, yol);
        }
        m.color = renk;
        m.SetFloat("_Glossiness", 0.15f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void Parca(PrimitiveType tip, Transform ust, Vector3 konum, Vector3 olcek, Material m, string ad, Vector3 aci = default)
    {
        var go = GameObject.CreatePrimitive(tip);
        go.name = ad;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(ust, false);
        go.transform.localPosition = konum;
        go.transform.localRotation = Quaternion.Euler(aci);
        go.transform.localScale = olcek;
        go.GetComponent<MeshRenderer>().sharedMaterial = m;
    }
}
