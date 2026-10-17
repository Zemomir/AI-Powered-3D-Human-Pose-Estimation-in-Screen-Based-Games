using UnityEngine;

// Sade ekran yazıları (OnGUI): durum, hız, süre, ilerleme ve ortada büyük yönergeler.
public class Arayuz : MonoBehaviour
{
    public HareketAlici alici;
    public Motor motor;

    GUIStyle kucuk, buyuk;

    void OnGUI()
    {
        int h = Screen.height;
        if (kucuk == null)
        {
            kucuk = new GUIStyle(GUI.skin.label);
            buyuk = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        }
        kucuk.fontSize = Mathf.Max(14, h / 36);
        buyuk.fontSize = Mathf.Max(24, h / 14);

        string kaynak = alici.KlavyeModu ? "Klavye modu (K: kapat)" : (alici.Bagli ? "Kamera bağlı" : "Kamera bağlı değil");
        string sol = $"{kaynak}\nHız: {motor.Hiz * 3.6f:0} km/sa\nSüre: {SureYazi(motor.Sure)}\nYol: %{motor.Ilerleme * 100:0}\nHız hassasiyeti: %{motor.HizHassasiyeti * 100:0}\nDönüş hassasiyeti: %{motor.DonusHassasiyeti * 100:0}\nYumuşatma: {(motor.YumusatmaAcik ? "açık" : "kapalı")}";
        if (motor.DuzeltilmisEgim.HasValue)
            sol += $"\nEğim: {motor.DuzeltilmisEgim.Value:+0;-0;0}°" + (motor.DuzeltilmisOne.HasValue ? $"   Öne: {motor.DuzeltilmisOne.Value * 100:+0.0;-0.0;0.0} cm" : "");
        if (!alici.KlavyeModu)
            sol += $"\nSıfır: öne {motor.OneSifir * 100:+0.0;-0.0;0.0} cm, eğim {motor.EgimSifir:+0;-0;0}°";
        Golgeli(new Rect(16, 12, h, h / 2f), sol, kucuk);
        Golgeli(new Rect(16, h - kucuk.fontSize * 2.2f, Screen.width, kucuk.fontSize * 2f), "R: baştan   K: klavye   + / -: hız   1 / 2: dönüş   Y: yumuşatma   C: sıfırla", kucuk);

        string orta = null;
        if (motor.Hal == Motor.Durum.Bitti) orta = $"Bitti!  {SureYazi(motor.Sure)}\nR ile tekrar";
        else if (alici.PortHatasi != null && !alici.KlavyeModu) orta = alici.PortHatasi;
        else if (!alici.Bagli) orta = "Kamera bağlı değil\nhareket_yayini.py çalışıyor mu?";
        else if (alici.GoruntuDonuk) orta = "Telefondan görüntü gelmiyor\nDroidCam açık mı?";
        else if (!alici.Kalibre) orta = "Oynayacağın gibi rahat otur\nayarlanıyor...";
        else if (!alici.Egim.HasValue) orta = "Kamera seni göremiyor\nomuzların görünsün";
        else if (motor.Hal == Motor.Durum.Ayarlaniyor) orta = motor.AyarOlcuyor ? "Ölçülüyor... kıpırdama" : $"Rahat otur, kıpırdama\n{Mathf.CeilToInt(motor.AyarKalanSn)}";
        else if (motor.YolDisinda) orta = "Yola dön";
        if (orta != null) Golgeli(new Rect(0, h * 0.3f, Screen.width, h * 0.3f), orta, buyuk);
    }

    static string SureYazi(float sn)
    {
        int ondalik = Mathf.RoundToInt(sn * 10);  // önce yuvarla: 59,96 sn "00:60.0" görünmesin
        return $"{ondalik / 600:00}:{ondalik % 600 / 10:00}.{ondalik % 10}";
    }

    static void Golgeli(Rect r, string metin, GUIStyle stil)
    {
        var renk = stil.normal.textColor;
        stil.normal.textColor = new Color(0, 0, 0, 0.6f);
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), metin, stil);
        stil.normal.textColor = Color.white;
        GUI.Label(r, metin, stil);
        stil.normal.textColor = renk;
    }
}
