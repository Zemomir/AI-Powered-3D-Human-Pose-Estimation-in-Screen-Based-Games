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

        var cimDoku = DokuUret.Uret("Cim", 512, 1);
        var toprakDoku = DokuUret.Uret("Toprak", 512, 2);
        var asfaltDoku = DokuUret.Uret("Asfalt", 512, 0);
        var kabukDoku = DokuUret.Uret("Kabuk", 256, 3);
        var asfalt = Malzeme("Asfalt", Color.white);
        Dokula(asfalt, asfaltDoku, new Vector2(2, 1));
        var beyaz = Malzeme("Beyaz", new Color(0.95f, 0.95f, 0.92f));
        var agacGovde = Malzeme("AgacGovde", Color.white);
        Dokula(agacGovde, kabukDoku, new Vector2(2, 2));
        var yaprak = Malzeme("Yaprak", new Color(0.25f, 0.40f, 0.23f));
        var kaya = Malzeme("Kaya", new Color(0.43f, 0.45f, 0.42f));
        var yildiz = Malzeme("Yildiz", new Color(1f, 0.78f, 0.16f));
        yildiz.EnableKeyword("_EMISSION");
        yildiz.SetColor("_EmissionColor", new Color(0.25f, 0.17f, 0.02f));
        EditorUtility.SetDirty(yildiz);

        var ufuk = new Color(.70f, .77f, .80f);
        RenderSettings.skybox = Gokyuzu();
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.48f, .59f, .72f);
        RenderSettings.ambientEquatorColor = new Color(.57f, .59f, .55f);
        RenderSettings.ambientGroundColor = new Color(.24f, .27f, .22f);
        var isik = new GameObject("Gunes").AddComponent<Light>();
        isik.type = LightType.Directional;
        isik.intensity = 1.05f;
        isik.color = new Color(1f, .95f, .85f);
        isik.shadows = LightShadows.Soft;
        isik.shadowStrength = .8f;
        isik.transform.rotation = Quaternion.Euler(40, -30, 0);
        RenderSettings.sun = isik;
        var yolGo = new GameObject("Yol", typeof(MeshFilter), typeof(MeshRenderer));
        var yol = yolGo.AddComponent<Yol>();
        yolGo.GetComponent<MeshRenderer>().sharedMaterial = asfalt;
        yol.kenarMalzemesi = beyaz;
        yol.bitisMalzemesi = beyaz;

        AraziUret.Kur(yol, cimDoku, toprakDoku);
        var sus = new GameObject("YolSusleri").AddComponent<YolSusu>();
        sus.yol = yol;
        sus.beyaz = beyaz;
        sus.kirmizi = Malzeme("BordurKirmizi", new Color(.58f, .16f, .12f));
        sus.reflektor = Malzeme("Reflektor", new Color(.72f, .49f, .23f));

        var oyun = new GameObject("Oyun");
        oyun.AddComponent<Grafik>();
        var alici = oyun.AddComponent<HareketAlici>();
        var kameraOnizleme = oyun.AddComponent<KameraOnizleme>();
        kameraOnizleme.alici = alici;
        oyun.AddComponent<EkranGoruntusu>();

        var motorGo = new GameObject("Motor");
        var motor = motorGo.AddComponent<Motor>();
        motor.alici = alici;
        motor.yol = yol;
        var govde = new GameObject("Govde").transform;
        govde.SetParent(motorGo.transform, false);
        motor.govde = govde;
        MotorModeli.Kur(govde, out motor.onTeker, out motor.arkaTeker, out Transform solKol, out Transform sagKol);

        var ses = oyun.AddComponent<Ses>();
        ses.motor = motor;
        ses.alici = alici;
        var yildizlar = oyun.AddComponent<Yildizlar>();
        yildizlar.yol = yol;
        yildizlar.motor = motor;
        yildizlar.ses = ses;
        yildizlar.malzeme = yildiz;
        var seyirciler = new GameObject("Seyirciler").AddComponent<Seyirciler>();
        seyirciler.yol = yol;
        seyirciler.motor = motor;
        seyirciler.alici = alici;
        seyirciler.ses = ses;
        seyirciler.solKolKok = solKol;
        seyirciler.sagKolKok = sagKol;
        seyirciler.ten = Malzeme("SeyirciTen", new Color(.68f, .43f, .28f));
        seyirciler.pantolon = Malzeme("SeyirciPantolon", new Color(.15f, .20f, .28f));
        seyirciler.sapka = Malzeme("SeyirciSapka", new Color(.85f, .69f, .28f));
        seyirciler.belirginTisort = Malzeme("SeyirciCakRengi", new Color(1f, .72f, .05f));
        seyirciler.tisortler = new[] {
            Malzeme("SeyirciMavi", new Color(.18f, .43f, .67f)),
            Malzeme("SeyirciMercan", new Color(.76f, .31f, .23f)),
            Malzeme("SeyirciYesil", new Color(.27f, .54f, .35f)),
            Malzeme("SeyirciMor", new Color(.53f, .31f, .59f)),
        };
        var otoPilot = oyun.AddComponent<OtoPilot>();
        otoPilot.motor = motor;
        otoPilot.seyirciler = seyirciler;
        var cevre = new GameObject("Cevre").AddComponent<Cevre>();
        cevre.yol = yol;
        cevre.agacGovde = agacGovde;
        cevre.yaprak = yaprak;
        cevre.kaya = kaya;
        cevre.yaprakTonlari = new[]
        {
            yaprak,
            Malzeme("YaprakAcik", new Color(.32f, .44f, .23f)),
            Malzeme("YaprakKoyu", new Color(.18f, .31f, .20f)),
            Malzeme("YaprakZeytin", new Color(.36f, .43f, .22f)),
        };

        var arayuz = oyun.AddComponent<Arayuz>();
        arayuz.alici = alici;
        arayuz.motor = motor;
        arayuz.yildizlar = yildizlar;
        arayuz.seyirciler = seyirciler;
        arayuz.ses = ses;

        var kopru = oyun.AddComponent<KopruYonetici>();
        kopru.alici = alici;
        motor.kopru = kopru;
        var anaMenu = oyun.AddComponent<AnaMenu>();
        anaMenu.motor = motor;
        anaMenu.alici = alici;
        anaMenu.kopru = kopru;
        anaMenu.arayuz = arayuz;
        anaMenu.ses = ses;
        arayuz.anaMenu = anaMenu;

        var kayit = oyun.AddComponent<OyunKaydi>();
        kayit.alici = alici;
        kayit.motor = motor;
        kayit.yildizlar = yildizlar;
        kayit.seyirciler = seyirciler;

        var kamera = new GameObject("Kamera", typeof(Camera), typeof(AudioListener));
        kamera.tag = "MainCamera";
        var cam = kamera.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.backgroundColor = new Color(0.62f, 0.78f, 0.92f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 110f;
        RenderSettings.fogEndDistance = 520f;
        RenderSettings.fogColor = ufuk;
        cam.farClipPlane = 600;
        cam.nearClipPlane = 0.5f;
        kamera.transform.position = new Vector3(0, 2.8f, -6.5f);
        var takip = kamera.AddComponent<KameraTakip>();
        takip.hedef = motorGo.transform;
        takip.motor = motor;

        EditorSceneManager.SaveScene(sahne, SAHNE);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(SAHNE, true) };

        PlayerSettings.productName = "Fizyo-Oyun";
        PlayerSettings.companyName = "FizyoOyun";
        // Python penceresi öndeyken oyun durmasın: kamera ile oyun aynı anda çalışıyor.
        PlayerSettings.runInBackground = true;
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.defaultIsNativeResolution = true;
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

    static void Dokula(Material m, Texture2D doku, Vector2 tekrar)
    {
        m.mainTexture = doku;
        m.mainTextureScale = tekrar;
        m.SetFloat("_Glossiness", .08f);
        EditorUtility.SetDirty(m);
    }

    static Material Gokyuzu()
    {
        string yol = MALZEME_KLASORU + "/Gokyuzu.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(yol);
        if (m == null)
        {
            m = new Material(Shader.Find("Skybox/Procedural"));
            AssetDatabase.CreateAsset(m, yol);
        }
        m.SetFloat("_SunSize", .035f);
        m.SetFloat("_SunSizeConvergence", 5f);
        m.SetFloat("_AtmosphereThickness", 1.1f);
        m.SetColor("_SkyTint", new Color(.50f, .55f, .60f));
        m.SetColor("_GroundColor", new Color(.37f, .40f, .34f));
        m.SetFloat("_Exposure", 1.1f);
        EditorUtility.SetDirty(m);
        return m;
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

}
