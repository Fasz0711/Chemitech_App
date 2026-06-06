using UnityEngine;

/// <summary>
/// Hace flotar suavemente un átomo decorativo con oscilación senoidal
/// y una leve rotación. No requiere DOTween — solo Unity nativo.
///
/// Adjuntar a: cada GameObject de átomo (O, H, C, Na, N).
/// </summary>
public class FloatingAtom : MonoBehaviour
{
    // ─── Parámetros de flotación ────────────────────────────────────────────
    [Header("Flotación vertical")]
    [Tooltip("Píxeles máximos que sube/baja el átomo")]
    [SerializeField] private float amplitud = 18f;
    [Tooltip("Velocidad de oscilación")]
    [SerializeField] private float velocidad = 1.1f;

    [Header("Rotación suave")]
    [SerializeField] private float anguloMax = 10f;   // grados
    [SerializeField] private float velRotacion = 0.5f;

    [Header("Desfase inicial (se auto-asigna si es 0)")]
    [SerializeField] private float desfase = 0f;

    // ─── Estado interno ─────────────────────────────────────────────────────
    private RectTransform rt;
    private Vector2 posInicial;

    // ───────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    private void Start()
    {
        posInicial = rt.anchoredPosition;

        // Desfase aleatorio para que no todos los átomos se muevan igual
        if (Mathf.Approximately(desfase, 0f))
            desfase = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        float t = Time.time;

        // Flotación vertical
        float offsetY = Mathf.Sin(t * velocidad + desfase) * amplitud;
        rt.anchoredPosition = new Vector2(posInicial.x, posInicial.y + offsetY);

        // Rotación suave (usa un coseno distinto para que sea independiente)
        float angulo = Mathf.Cos(t * velRotacion + desfase) * anguloMax;
        rt.localRotation = Quaternion.Euler(0f, 0f, angulo);
    }
}