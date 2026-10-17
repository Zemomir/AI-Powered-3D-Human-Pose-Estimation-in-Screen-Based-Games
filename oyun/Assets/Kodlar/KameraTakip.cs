using UnityEngine;

// Motorun arkasından ve biraz yukarıdan yumuşak takip.
public class KameraTakip : MonoBehaviour
{
    public Transform hedef;
    public float geri = 9f, yukari = 4f, bakisYuksekligi = 1.2f, yumusaklik = 4f;

    void LateUpdate()
    {
        if (hedef == null) return;
        Vector3 ileri = hedef.forward; ileri.y = 0; ileri.Normalize();
        Vector3 istenen = hedef.position - ileri * geri + Vector3.up * yukari;
        transform.position = Vector3.Lerp(transform.position, istenen, yumusaklik * Time.deltaTime);
        transform.LookAt(hedef.position + Vector3.up * bakisYuksekligi);
    }
}
