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
    // Rotación libre: se puede mirar la escena desde arriba y también desde abajo.
    // Se corta en ±85 y no en ±90 a propósito: justo en el polo la proyección de
    // 'forward' sobre el plano vale cero y el desplazamiento lateral se quedaría sin
    // dirección que seguir.
    [SerializeField] private float minPitch  = -85f;
    [SerializeField] private float maxPitch  =  85f;
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

    /// <summary>El punto al que mira la cámara: lo que hay justo bajo la cruz central.
    /// La colocación lo sigue, así que mover la cámara es lo que mueve el átomo.</summary>
    public Vector3 FocusPoint => curTarget;

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

        // Mirando casi en vertical, 'forward' apenas tiene componente horizontal y
        // normalizarlo daría una dirección sin sentido. Ahí el "hacia delante" de la
        // pantalla es la vertical de la cámara, que sí apunta a algún lado del plano.
        Vector3 flat = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (flat.sqrMagnitude < 0.0001f) flat = Vector3.ProjectOnPlane(transform.up, Vector3.up);
        Vector3 fwd = flat.normalized;
        Vector3 move  = (right * dir.x + fwd * dir.y) * panSpeed * Time.deltaTime * (desDist * 0.12f);
        desTarget += move;
    }

    /// <summary>sign &gt; 0 acerca, &lt; 0 aleja.</summary>
    public void Zoom(float sign)
    {
        desDist = Mathf.Clamp(desDist - sign * zoomSpeed * Time.deltaTime, minDist, maxDist);
    }

    /// <summary>Establece la distancia de zoom directamente (para slider de UI).</summary>
    public void SetZoomDistance(float distance)
    {
        desDist = Mathf.Clamp(distance, minDist, maxDist);
    }

    /// <summary>Fija la orientación completa. Lo usa la escena de clase para "seguir la
    /// vista del docente" y para el botón de volver a ella: el suavizado de LateUpdate
    /// hace que el salto se vea como un movimiento, no como un corte.</summary>
    public void SetView(float yaw, float pitch, float distance)
    {
        desYaw   = yaw;
        desPitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        desDist  = Mathf.Clamp(distance, minDist, maxDist);
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
