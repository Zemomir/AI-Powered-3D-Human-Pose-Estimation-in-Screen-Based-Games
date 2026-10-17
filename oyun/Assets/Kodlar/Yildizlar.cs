using System.Collections.Generic;
using UnityEngine;

// Aynı yol, aynı hedefler: üç sol, üç orta, üç sağ.
public class Yildizlar : MonoBehaviour
{
    public Yol yol;
    public Motor motor;
    public Ses ses;
    public Material malzeme;

    public int Toplanan { get; private set; }
    public int Toplam => yildizlar.Count;
    public int SolToplanan { get; private set; }
    public int OrtaToplanan { get; private set; }
    public int SagToplanan { get; private set; }
    public int SolToplam { get; private set; }
    public int OrtaToplam { get; private set; }
    public int SagToplam { get; private set; }
    public string KareOlayi { get; private set; } = "";

    class Yildiz
    {
        public Transform gorsel;
        public Vector3 konum;
        public int taraf;
        public bool toplandi;
    }
    readonly List<Yildiz> yildizlar = new List<Yildiz>();

    void OnEnable() { if (motor != null) motor.Sifirlandi += Sifirla; }
    void OnDisable() { if (motor != null) motor.Sifirlandi -= Sifirla; }

    void Start()
    {
        if (yol == null || yol.Noktalar == null || yol.Noktalar.Count < 2 || malzeme == null) return;
        int sira = 0;
        int ilk = yol.BaslangicIndeksi + 30, son = yol.Noktalar.Count - 15;
        // Desen 9 yıldızda bir tamamlanır (3 sol, 3 orta, 3 sağ); yalnız tam desenler konur ki
        // sol ve sağ yıldız sayısı eşit olsun: taraf karşılaştırması fizyoterapist için veri.
        int adet = Mathf.Max(0, (son - ilk + 11) / 12) / 9 * 9;
        for (int i = ilk; i < son && sira < adet; i += 12)
        {
            int taraf = (sira / 3) % 3;
            // Cross(up, ileri) sağdır; negatif değer sürücünün soludur.
            Vector3 yan = Vector3.Cross(Vector3.up, yol.Yon(i)).normalized;
            Vector3 konum = yol.Noktalar[i] + yan * ((taraf - 1) * 2.2f) + Vector3.up * 1.2f;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Yildiz_" + sira;
            var carpisma = go.GetComponent<Collider>();
            carpisma.enabled = false;
            Destroy(carpisma);
            go.transform.SetParent(transform, false);
            go.transform.position = konum;
            go.transform.localScale = Vector3.one * 0.6f;
            go.transform.rotation = Quaternion.Euler(45, 0, 45);
            go.GetComponent<MeshRenderer>().sharedMaterial = malzeme;
            yildizlar.Add(new Yildiz { gorsel = go.transform, konum = konum, taraf = taraf });
            if (taraf == 0) SolToplam++;
            else if (taraf == 1) OrtaToplam++;
            else SagToplam++;
            sira++;
        }
    }

    void Update()
    {
        KareOlayi = "";
        for (int i = 0; i < yildizlar.Count; i++)
        {
            var y = yildizlar[i];
            if (y.toplandi || y.gorsel == null) continue;
            y.gorsel.position = y.konum + Vector3.up * (0.12f * Mathf.Sin(Time.time * 1.7f + i));
            y.gorsel.Rotate(Vector3.up, 30f * Time.deltaTime, Space.Self);
            if (motor == null || motor.Hal != Motor.Durum.Suruyor) continue;
            Vector3 fark = motor.transform.position - y.konum;
            fark.y = 0;
            if (fark.sqrMagnitude >= 1.4f * 1.4f) continue;
            y.toplandi = true;
            y.gorsel.gameObject.SetActive(false);
            Toplanan++;
            string olay;
            if (y.taraf == 0) { SolToplanan++; olay = "yildiz_sol"; }
            else if (y.taraf == 1) { OrtaToplanan++; olay = "yildiz_orta"; }
            else { SagToplanan++; olay = "yildiz_sag"; }
            KareOlayi += (KareOlayi.Length == 0 ? "" : " ") + olay;
            if (ses != null) ses.Topla();
        }
    }

    void Sifirla()
    {
        Toplanan = SolToplanan = OrtaToplanan = SagToplanan = 0;
        KareOlayi = "";
        foreach (var y in yildizlar)
        {
            y.toplandi = false;
            if (y.gorsel == null) continue;
            y.gorsel.gameObject.SetActive(true);
            y.gorsel.position = y.konum;
            y.gorsel.rotation = Quaternion.Euler(45, 0, 45);
        }
    }
}
