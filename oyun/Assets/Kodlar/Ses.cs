using UnityEngine;

// Yumuşak sinüs tonları; ses dosyası gerekmez. Rekor da durum geçişinde tutulur.
public class Ses : MonoBehaviour
{
    public Motor motor;
    public bool SesAcik { get; private set; }
    public float EnIyiSure { get; private set; }
    public bool YeniRekor { get; private set; }
    AudioSource kaynak;
    AudioClip topla, basla, bitis;
    Motor.Durum oncekiHal;

    void Awake()
    {
        SesAcik = PlayerPrefs.GetInt("SesAcik", 1) != 0;
        EnIyiSure = PlayerPrefs.GetFloat("EnIyiSure", 0f);
        if (!float.IsFinite(EnIyiSure) || EnIyiSure < 0) EnIyiSure = 0;
        kaynak = gameObject.AddComponent<AudioSource>();
        kaynak.playOnAwake = false;
        kaynak.spatialBlend = 0;
        kaynak.volume = 0.4f;
        kaynak.mute = !SesAcik;
        topla = Tonlar("topla", 0.06f, new[] { 660f, 880f });
        basla = Tonlar("basla", 0.18f, new[] { 523.25f });
        bitis = Tonlar("bitis", 0.13f, new[] { 523.25f, 659.25f, 783.99f });
    }

    void OnEnable()
    {
        if (motor == null) return;
        motor.Sifirlandi += Sifirla;
        oncekiHal = motor.Hal;
    }
    void OnDisable() { if (motor != null) motor.Sifirlandi -= Sifirla; }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.M)) return;
        SesAcik = !SesAcik;
        if (kaynak != null) kaynak.mute = !SesAcik;
        PlayerPrefs.SetInt("SesAcik", SesAcik ? 1 : 0);
        PlayerPrefs.Save();
    }

    void LateUpdate()
    {
        if (motor == null) return;
        if (oncekiHal == Motor.Durum.Ayarlaniyor && motor.Hal == Motor.Durum.Suruyor) Cal(basla);
        if (oncekiHal != Motor.Durum.Bitti && motor.Hal == Motor.Durum.Bitti)
        {
            Cal(bitis);
            YeniRekor = float.IsFinite(motor.Sure) && motor.Sure > 0 && (EnIyiSure <= 0 || motor.Sure < EnIyiSure);
            if (YeniRekor)
            {
                EnIyiSure = motor.Sure;
                PlayerPrefs.SetFloat("EnIyiSure", EnIyiSure);
                PlayerPrefs.Save();
            }
        }
        oncekiHal = motor.Hal;
    }

    void Sifirla() { YeniRekor = false; oncekiHal = Motor.Durum.Bekliyor; }
    public void Topla() { Cal(topla); }
    void Cal(AudioClip klip) { if (SesAcik && kaynak != null && klip != null) kaynak.PlayOneShot(klip); }

    static AudioClip Tonlar(string ad, float notaSn, float[] notalar)
    {
        const int ornekHizi = 44100;
        int uzunluk = Mathf.RoundToInt(notaSn * ornekHizi);
        var veri = new float[uzunluk * notalar.Length];
        for (int n = 0; n < notalar.Length; n++)
            for (int i = 0; i < uzunluk; i++)
            {
                float t = i / (float)ornekHizi;
                float zarf = Mathf.Min(1f, t / 0.008f) * Mathf.Min(1f, (notaSn - t) / 0.025f);
                veri[n * uzunluk + i] = 0.45f * zarf * Mathf.Sin(2f * Mathf.PI * notalar[n] * t);
            }
        var klip = AudioClip.Create(ad, veri.Length, 1, ornekHizi, false);
        klip.SetData(veri, 0);
        return klip;
    }

    void OnDestroy()
    {
        if (topla != null) Destroy(topla);
        if (basla != null) Destroy(basla);
        if (bitis != null) Destroy(bitis);
    }
}
