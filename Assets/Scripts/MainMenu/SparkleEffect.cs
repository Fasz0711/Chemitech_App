using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Efecto de parpadeo/twinkle para los destellos decorativos del fondo.
/// Hace que la estrella pulse su escala y alfa de forma suave.
///
/// Adjuntar a: cada GameObject de destello (Sparkle_Yellow, Sparkle_Pink, etc.)
/// </summary>
public class SparkleEffect : MonoBehaviour
{
    // ─── Escala ─────────────────────────────────────────────────────────────
    [Header("Escala")]
    [SerializeField] private float escalaMin = 0.4f;
    [SerializeField] private float escalaMax = 1.1f;
    [SerializeField] private float velEscala = 2.2f;

    // ─── Alfa ────────────────────────────────────────────────────────────────
    [Header("Alfa (opacidad)")]
    [SerializeField] private float alfaMin = 0.3f;
    [SerializeField] private float alfaMax = 1.0f;

    // ─── Rotación ────────────────────────────────────────────────────────────
    [Header("Rotación constante")]
    [SerializeField] private float velRotacion = 40f;  // grados/seg

    // ─── Desfase ─────────────────────────────────────────────────────────────
    [SerializeField] private float desfase = 0f;

    // ─── Referencias ─────────────────────────────────────────────────────────
    private RectTransform rt;
    private Image img;

    // ───────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        img = GetComponent<Image>();
        if (Mathf.Approximately(desfase, 0f))
            desfase = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        float t = (Mathf.Sin(Time.time * velEscala + desfase) + 1f) * 0.5f; // rango [0,1]

        // Escala
        float escala = Mathf.Lerp(escalaMin, escalaMax, t);
        rt.localScale = Vector3.one * escala;

        // Alfa
        if (img != null)
        {
            Color c = img.color;
            c.a = Mathf.Lerp(alfaMin, alfaMax, t);
            img.color = c;
        }

        // Rotación constante
        rt.Rotate(0f, 0f, velRotacion * Time.deltaTime);
    }
}