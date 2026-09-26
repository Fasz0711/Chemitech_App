using UnityEngine;

/// <summary>
/// Cámara en PRIMERA PERSONA: gira sobre sí misma y se desplaza por el espacio.
///
/// Antes orbitaba alrededor de un punto (posición = objetivo − adelante × distancia), y
/// eso se sentía como tercera persona: girar describía un arco alrededor de un centro
/// invisible del que no se podía uno separar. Ahora la cámara ES el punto de vista.
///
///   • Arrastrar          → gira la vista (yaw y pitch), sin moverse del sitio.
///   • D-pad              → avanza, retrocede y se desplaza a los lados, SIEMPRE EN
///                          HORIZONTAL: mirar hacia abajo no te hace descender.
///   • Flechas verticales → suben y bajan. Es la única forma de cambiar de altura.
///   • Recentrar          → vuelve al punto y al ángulo de partida.
///
/// EL NOMBRE SE QUEDA como estaba a propósito: cambiarlo obliga a renombrar el archivo,
/// y las tres escenas referencian este script por el GUID de su .meta. No compensa el
/// riesgo por un nombre. Ya no orbita nada.
/// </summary>
public class OrbitCameraController : MonoBehaviour
{
    [Header("Punto de partida (Recentrar vuelve aquí)")]
    // Equivale exactamente al encuadre que daba la cámara orbital con objetivo (0, 0.55, 0),
    // yaw 35, pitch 28 y distancia 8: al pasar a primera persona la vista inicial no cambia.
    [SerializeField] private Vector3 defPosition = new Vector3(-4.05f, 4.31f, -5.79f);
    [SerializeField] private float   defYaw      = 35f;
    [SerializeField] private float   defPitch    = 28f;

    [Header("Límites")]
    // Rotación libre. Se corta en ±85 y no en ±90 porque justo en el polo la dirección
    // horizontal de avance se queda sin definir.
    [SerializeField] private float minPitch   = -85f;
    [SerializeField] private float maxPitch   =  85f;
    // Caja por la que se puede volar. Generosa respecto a la zona de construcción, para
    // poder mirar el conjunto desde fuera sin perderse.
    [SerializeField] private float boundsHalf = 40f;
    [SerializeField] private float minY       = -15f;
    [SerializeField] private float maxY       =  30f;

    [Header("Sensibilidad")]
    [SerializeField] private float rotSpeed  = 0.22f;
    [SerializeField] private float panSpeed  = 6f;    // unidades/segundo en el plano
    [SerializeField] private float vertSpeed = 6f;
    [SerializeField] private float smooth    = 12f;

    // Estado deseado (al que se interpola) y estado actual (el que se aplica).
    Vector3 desPos, curPos;
    float   desYaw, curYaw, desPitch, curPitch;

    /// <summary>La vista a la que se DIRIGE la cámara, no la interpolada. Es lo que hay
    /// que compartir: si se leyera la actual, pulsar el botón justo después de girar
    /// mandaría un fotograma intermedio del suavizado.</summary>
    public Vector3 ViewPosition => desPos;
    public float   ViewYaw      => desYaw;
    public float   ViewPitch    => desPitch;

    void Awake()
    {
        desPos   = curPos   = defPosition;
        desYaw   = curYaw   = defYaw;
        desPitch = curPitch = defPitch;
        Apply();
    }

    /// <summary>Arrastre del puntero → gira la vista en el sitio.</summary>
    public void Rotate(Vector2 delta)
    {
        desYaw  += delta.x * rotSpeed;
        desPitch = Mathf.Clamp(desPitch - delta.y * rotSpeed, minPitch, maxPitch);
    }

    /// <summary>dir.x = lateral, dir.y = adelante/atrás. SOLO EN HORIZONTAL.
    ///
    /// La dirección sale del YAW y no de 'forward' a propósito: con 'forward', mirar al
    /// suelo y avanzar te hundiría, y la altura dejaría de estar bajo control. Aquí
    /// avanzar es avanzar, y subir se pide aparte.</summary>
    public void PanScreen(Vector2 dir)
    {
        float rad = desYaw * Mathf.Deg2Rad;
        Vector3 fwd   = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);

        desPos += (right * dir.x + fwd * dir.y) * panSpeed * Time.deltaTime;
        ClampPos();
    }

    /// <summary>sign &gt; 0 sube, &lt; 0 baja.</summary>
    public void MoveVertical(float sign)
    {
        desPos.y += sign * vertSpeed * Time.deltaTime;
        ClampPos();
    }

    /// <summary>Coloca la vista entera. Lo usa la clase para traer a los alumnos a la
    /// vista del docente: el suavizado hace que el salto se vea como un movimiento y no
    /// como un corte.</summary>
    public void SetView(Vector3 position, float yaw, float pitch)
    {
        desPos   = position;
        desYaw   = yaw;
        desPitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        ClampPos();
    }

    public void Recenter()
    {
        desPos   = defPosition;
        desYaw   = defYaw;
        desPitch = defPitch;
    }

    void ClampPos()
    {
        desPos.x = Mathf.Clamp(desPos.x, -boundsHalf, boundsHalf);
        desPos.y = Mathf.Clamp(desPos.y, minY, maxY);
        desPos.z = Mathf.Clamp(desPos.z, -boundsHalf, boundsHalf);
    }

    void LateUpdate()
    {
        float t = 1f - Mathf.Exp(-smooth * Time.deltaTime);
        curYaw   = Mathf.LerpAngle(curYaw, desYaw, t);
        curPitch = Mathf.Lerp(curPitch, desPitch, t);
        curPos   = Vector3.Lerp(curPos, desPos, t);
        Apply();
    }

    void Apply()
        => transform.SetPositionAndRotation(curPos, Quaternion.Euler(curPitch, curYaw, 0f));
}
