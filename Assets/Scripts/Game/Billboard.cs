using UnityEngine;

/// <summary>Hace que el objeto (etiqueta de átomo) siempre mire a la cámara.</summary>
public class Billboard : MonoBehaviour
{
    Transform cam;

    void Start()
    {
        if (Camera.main) cam = Camera.main.transform;
    }

    void LateUpdate()
    {
        if (!cam) { if (Camera.main) cam = Camera.main.transform; else return; }
        transform.rotation = Quaternion.LookRotation(transform.position - cam.position);
    }
}
