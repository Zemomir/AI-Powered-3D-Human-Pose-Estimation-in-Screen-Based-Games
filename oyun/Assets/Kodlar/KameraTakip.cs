using UnityEngine;

// Motorun arkasından ve biraz yukarıdan yumuşak takip.
public class KameraTakip : MonoBehaviour
{
    public Transform hedef;
    public Motor motor;
    Camera kamera;
    public float geri = 6.5f, yukari = 2.8f, bakisYuksekligi = 1.1f, yumusaklik = 4f;

    void Start()
    {
        kamera = GetComponent<Camera>();
        if (kamera != null) kamera.fieldOfView = 60f;
    }

    void LateUpdate()
    {
        if (kamera != null && motor != null)
        {
            float oran = Mathf.Clamp01(motor.Hiz / Mathf.Max(0.01f, motor.enCokHiz));
            kamera.fieldOfView = Mathf.Lerp(kamera.fieldOfView, 60f + 10f * oran,
                1f - Mathf.Exp(-3f * Time.deltaTime));
        }
        if (hedef == null) return;
        Vector3 ileri = hedef.forward; ileri.y = 0; ileri.Normalize();
        Vector3 istenen = hedef.position - ileri * geri + Vector3.up * yukari;
        transform.position = Vector3.Lerp(transform.position, istenen, yumusaklik * Time.deltaTime);
        transform.LookAt(hedef.position + Vector3.up * bakisYuksekligi);
    }
}
