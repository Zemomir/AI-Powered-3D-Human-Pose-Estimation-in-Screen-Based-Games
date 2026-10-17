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
    public Yildizlar yildizlar;

    const string AdresArgumani = "-otopilotOfset";
    UdpClient istemci;
    IPEndPoint hedef;
    float sonrakiGonderim;
    float ofset;

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

        istemci = new UdpClient();
        int port = FindObjectOfType<HareketAlici>()?.port ?? 5065;
        hedef = new IPEndPoint(IPAddress.Loopback, port);
    }

    void Update()
    {
        if (istemci == null || motor == null || Time.realtimeSinceStartup < sonrakiGonderim) return;
        sonrakiGonderim = Time.realtimeSinceStartup + 1f / 30f;

        float egim = 0f, one = 0f;
        if (!(Time.realtimeSinceStartup < 6f && motor.Hal == Motor.Durum.Ayarlaniyor))
        {
            Yol yol = motor.yol;
            if (yol != null && yol.Noktalar != null && yol.Noktalar.Count > 1)
            {
                int indeks = yol.EnYakin(motor.transform.position, 0, out _);
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

        string json = "{\"t\":" + Time.realtimeSinceStartup.ToString("R", CultureInfo.InvariantCulture) +
            ",\"kalibre\":true,\"egim\":" + egim.ToString("R", CultureInfo.InvariantCulture) +
            ",\"one\":" + one.ToString("R", CultureInfo.InvariantCulture) + ",\"donuk\":false}";
        byte[] veri = Encoding.UTF8.GetBytes(json);
        istemci.Send(veri, veri.Length, hedef);
    }

    void OnDestroy()
    {
        istemci?.Close();
        istemci = null;
    }
}
