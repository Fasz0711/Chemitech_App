using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Efecto visual de "apriete" al tocar un botón en pantalla táctil.
/// Se encoge levemente al presionar y vuelve a su tamaño original al soltar.
/// NO requiere Animator — puro código.
///
/// Adjuntar a: cada botón (BtnJugar, BtnDiario, BtnAjustes, BtnIniciarSesion).
/// </summary>
public class ButtonPressEffect : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerExitHandler
{
    // ─── Configuración ───────────────────────────────────────────────────────
    [Header("Escala al presionar")]
    [SerializeField] private float escalaPresionado = 0.94f;
    [Tooltip("Qué tan rápido anima la escala (mayor = más rápido)")]
    [SerializeField] private float velocidadAnim = 12f;

    // ─── Estado interno ──────────────────────────────────────────────────────
    private Vector3 escalaObjetivo = Vector3.one;
    private RectTransform rt;

    // ───────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    private void Update()
    {
        // Interpolación suave hacia la escala objetivo
        rt.localScale = Vector3.Lerp(
            rt.localScale,
            escalaObjetivo,
            Time.deltaTime * velocidadAnim
        );
    }

    // ─── Eventos de puntero ──────────────────────────────────────────────────
    public void OnPointerDown(PointerEventData eventData)
    {
        escalaObjetivo = Vector3.one * escalaPresionado;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        escalaObjetivo = Vector3.one;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Por si el dedo se desliza fuera del botón
        escalaObjetivo = Vector3.one;
    }
}