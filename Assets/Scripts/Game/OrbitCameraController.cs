using UnityEngine;

/// <summary>
/// Cámara orbital 3D para la zona de juego: gira alrededor de un punto objetivo,
/// hace pan sobre el plano, zoom y "recentrar". Todo con suavizado.
/// La API pública la invocan los controles del HUD (arrastrar, d-pad, flechas, Recentrar).
/// </summary>
public class OrbitCameraController : MonoBehaviour
{
    [Header("Valores por defecto (Recentrar vuelve aquí)")]
    [SerializeField] private float defYaw      = 35f;
    [SerializeField] private float defPitch    = 28f;
    [SerializeField] private float defDistance = 16f;
    [SerializeField] private Vector3 defTarget = Vector3.zero;

    [Header("Límites")]
    [SerializeField] private float minPitch  = 5f;
    [SerializeField] private float maxPitch  = 80f;
    [SerializeField] private float minDist   = 6f;
    [SerializeField] private float maxDist   = 34f;

    [Header("Sensibilidad")]
    [SerializeField] private float rotSpeed  = 0.22f;
    [SerializeField] private float panSpeed  = 6f;
    [SerializeField] private float zoomSpeed = 14f;
    [SerializeField] private float vertSpeed = 6f;
    [SerializeField] private float smooth    = 12f;

    [Header("Límite vertical (subir/bajar cámara)")]
    [SerializeField] private float minTargetY = -1f;
    [SerializeField] private float maxTargetY = 14f;

    // Estado deseado (al que se interpola)
    float   desYaw, desPitch, desDist;
    Vector3 desTarget;
    // Estado actual (aplicado al transform)
    float   curYaw, curPitch, curDist;
    Vector3 curTarget;

    /// <summary>Altura (Y) del punto de enfoque actual de la cámara.</summary>
    public float FocusHeight => curTarget.y;

    void Awake()
    {
        desYaw = curYaw = defYaw;
        desPitch = curPitch = defPitch;
        desDist = curDist = defDistance;
        desTarget = curTarget = defTarget;
        Apply();
    }

    /// <summary>Arrastre del puntero → órbita (yaw/pitch).</summary>
    public void Rotate(Vector2 delta)
    {
        desYaw   += delta.x * rotSpeed;
        desPitch  = Mathf.Clamp(desPitch - delta.y * rotSpeed, minPitch, maxPitch);
    }

    /// <summary>dir.x = strafe (der/izq), dir.y = avance/retroceso sobre el plano.</summary>
    public void PanScreen(Vector2 dir)
    {
        Vector3 right = transform.right;
        Vector3 fwd   = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 move  = (right * dir.x + fwd * dir.y) * panSpeed * Time.deltaTime * (desDist * 0.12f);
        desTarget += move;
    }

    /// <summary>sign &gt; 0 acerca, &lt; 0 aleja.</summary>
    public void Zoom(float sign)
    {
        desDist = Mathf.Clamp(desDist - sign * zoomSpeed * Time.deltaTime, minDist, maxDist);
    }

    /// <summary>sign &gt; 0 sube la cámara, &lt; 0 la baja (mueve el punto objetivo en Y).</summary>
    public void MoveVertical(float sign)
    {
        desTarget.y = Mathf.Clamp(desTarget.y + sign * vertSpeed * Time.deltaTime, minTargetY, maxTargetY);
    }

    public void Recenter()
    {
        desYaw    = defYaw;
        desPitch  = defPitch;
        desDist   = defDistance;
        desTarget = defTarget;
    }

    void LateUpdate()
    {
        float t = 1f - Mathf.Exp(-smooth * Time.deltaTime);
        curYaw    = Mathf.LerpAngle(curYaw, desYaw, t);
        curPitch  = Mathf.Lerp(curPitch, desPitch, t);
        curDist   = Mathf.Lerp(curDist, desDist, t);
        curTarget = Vector3.Lerp(curTarget, desTarget, t);
        Apply();
    }

    void Apply()
    {
        Quaternion rot = Quaternion.Euler(curPitch, curYaw, 0f);
        Vector3 pos = curTarget - (rot * Vector3.forward) * curDist;
        transform.SetPositionAndRotation(pos, rot);
    }
}
