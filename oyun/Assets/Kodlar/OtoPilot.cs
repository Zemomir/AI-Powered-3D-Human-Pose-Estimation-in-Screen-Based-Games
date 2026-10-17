using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

// Yalnız testlerde, kamera alıcısına gerçek UDP sinyali gönderir.
public class OtoPilot : MonoBehaviour
{
    public Motor motor;
    public Seyirciler seyirciler;

    const string AdresArgumani = "-otopilotOfset";
    UdpClient istemci;
    IPEndPoint hedef;
    float sonrakiGonderim;
    float ofset;
    int ipucu = -1;  // EnYakin dar pencerede arar; ipucu motorla birlikte ilerlemeli
    bool elKullan = true;
    float solBitis, sagBitis, solTekrar, sagTekrar;

    void Awake()
    {
        string[] argumanlar = Environment.GetCommandLineArgs();
        if (Array.IndexOf(argumanlar, "-otopilot") < 0)
        {
            enabled = false;
            return;
        }

        for (int i = 0; i + 1 < argumanlar.Length; i++)
            if (argumanlar[i] == AdresArgumani &&
                float.TryParse(argumanlar[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float deger) &&
                float.IsFinite(deger)) ofset = deger;
        int elArgumani = Array.IndexOf(argumanlar, "-otopilotEl");
        if (elArgumani >= 0 && elArgumani + 1 < argumanlar.Length)
            elKullan = argumanlar[elArgumani + 1] != "0";

        istemci = new UdpClient();
        int port = FindFirstObjectByType<HareketAlici>()?.port ?? 5065;
        hedef = new IPEndPoint(IPAddress.Loopback, port);
    }

    void Update()
    {
        if (istemci == null || motor == null || Time.realtimeSinceStartup < sonrakiGonderim) return;
        sonrakiGonderim = Time.realtimeSinceStartup + 1f / 30f;

        float egim = 0f, one = 0f;
        if (motor.Hal != Motor.Durum.Suruyor)
        {
            if (motor.Hal == Motor.Durum.Bekliyor || motor.Hal == Motor.Durum.Bitti)
                ipucu = motor.yol != null ? motor.yol.BaslangicIndeksi : -1;
        }
        else
        {
            Yol yol = motor.yol;
            if (yol != null && yol.Noktalar != null && yol.Noktalar.Count > 1)
            {
                if (ipucu < 0) ipucu = yol.BaslangicIndeksi;
                int indeks = yol.EnYakin(motor.transform.position, ipucu, out _);
                ipucu = indeks;
                int hedefIndeks = Mathf.Clamp(indeks + 8, 0, yol.Noktalar.Count - 1);
                Vector3 yon = yol.Yon(hedefIndeks);
                Vector3 sag = Vector3.Cross(Vector3.up, yon).normalized;
                Vector3 nokta = yol.Noktalar[hedefIndeks] + sag * ofset;
                Vector3 delta = nokta - motor.transform.position;
                delta.y = 0;
                float aci = Vector3.SignedAngle(motor.transform.forward, delta, Vector3.up);
                egim = Mathf.Abs(aci) < 1f ? 0f : Mathf.Clamp(3f + Mathf.Abs(aci) * 0.8f, 0f, 25f) * Mathf.Sign(aci);
                one = 0.02f;
            }
        }

        float solEl = -1.6f, sagEl = -1.6f;
        if (elKullan && motor.Hal == Motor.Durum.Suruyor && seyirciler != null)
        {
            solEl = ElDarbasi(seyirciler.BekleyenSeyirci(false, ipucu), ref solBitis, ref solTekrar);
            sagEl = ElDarbasi(seyirciler.BekleyenSeyirci(true, ipucu), ref sagBitis, ref sagTekrar);
        }
        else ElleriSifirla();

        string json = "{\"t\":" + Time.realtimeSinceStartup.ToString("R", CultureInfo.InvariantCulture) +
            ",\"kalibre\":true,\"egim\":" + egim.ToString("R", CultureInfo.InvariantCulture) +
            ",\"one\":" + one.ToString("R", CultureInfo.InvariantCulture) + ",\"donuk\":false" +
            ",\"sol_el\":" + solEl.ToString("R", CultureInfo.InvariantCulture) +
            ",\"sag_el\":" + sagEl.ToString("R", CultureInfo.InvariantCulture) + "}";
        byte[] veri = Encoding.UTF8.GetBytes(json);
        istemci.Send(veri, veri.Length, hedef);
    }

    void OnEnable() { if (motor != null) motor.Sifirlandi += ElleriSifirla; }
    void OnDisable() { if (motor != null) motor.Sifirlandi -= ElleriSifirla; }
    void ElleriSifirla() { solBitis = sagBitis = solTekrar = sagTekrar = 0; }

    static float ElDarbasi(bool bekleyen, ref float bitis, ref float tekrar)
    {
        float simdi = Time.realtimeSinceStartup;
        if (bekleyen && simdi >= tekrar)
        {
            bitis = simdi + .6f;
            tekrar = bitis + .2f; // En az altı UDP örneği boyunca inik: yeniden kurulma görülsün.
        }
        return simdi < bitis ? .6f : -1.6f;
    }

    void OnDestroy()
    {
        istemci?.Close();
        istemci = null;
    }
}
