using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Animación de entrada del Menú Principal.
/// - Título: cae desde arriba con rebote
/// - Botones: aparecen en cascada de abajo hacia arriba con fade
/// - Átomos: aparecen con fade tras los botones
/// - Botón IniciarSesión: aparece desde la derecha
///
/// Adjuntar al mismo GameObject que MainMenuManager.
/// Llamar a PlayEntrance() desde MainMenuManager.Start().
/// </summary>
public class MenuAnimator : MonoBehaviour
{
    // ─── Elementos a animar ──────────────────────────────────────────────────
    [Header("Título")]
    [SerializeField] private RectTransform tituloRT;
    [SerializeField] private CanvasGroup tituloGroup;

    [Header("Botones (en orden: Jugar, Diario, Ajustes)")]
    [SerializeField] private RectTransform[] botonesRT;
    [SerializeField] private CanvasGroup[] botonesGroup;

    [Header("Botón Iniciar Sesión")]
    [SerializeField] private RectTransform loginRT;
    [SerializeField] private CanvasGroup loginGroup;

    [Header("Átomos decorativos")]
    [SerializeField] private CanvasGroup[] atomosGroup;

    // ─── Tiempos ─────────────────────────────────────────────────────────────
    [Header("Tiempos de animación")]
    [SerializeField] private float duracionTitulo = 0.55f;
    [SerializeField] private float duracionBoton = 0.35f;
    [SerializeField] private float desfaseBoton = 0.10f; // entre botones
    [SerializeField] private float duracionLogin = 0.30f;
    [SerializeField] private float duracionAtomos = 0.50f;

    // ─── Offsets iniciales ───────────────────────────────────────────────────
    [Header("Desplazamientos iniciales")]
    [SerializeField] private float offsetTituloY = 120f;  // cae desde +120px
    [SerializeField] private float offsetBotonY = -50f;  // sube desde -50px
    [SerializeField] private float offsetLoginX = 200f;  // entra desde +200px derecha

    // ───────────────────────────────────────────────────────────────────────
    /// <summary>Inicia la secuencia de entrada.</summary>
    public void PlayEntrance()
    {
        // Ocultar todo al inicio
        SetupInitialState();
        StartCoroutine(SequenceEntrance());
    }

    // ─── Setup inicial (todo invisible/desplazado) ───────────────────────────
    private void SetupInitialState()
    {
        // Título
        if (tituloGroup) { tituloGroup.alpha = 0f; }
        if (tituloRT) { ShiftY(tituloRT, offsetTituloY); }

        // Botones
        for (int i = 0; i < botonesGroup.Length; i++)
        {
            if (botonesGroup[i]) botonesGroup[i].alpha = 0f;
            if (botonesRT[i]) ShiftY(botonesRT[i], offsetBotonY);
        }

        // Login
        if (loginGroup) { loginGroup.alpha = 0f; }
        if (loginRT) { ShiftX(loginRT, offsetLoginX); }

        // Átomos
        foreach (var g in atomosGroup)
            if (g) g.alpha = 0f;
    }

    // ─── Secuencia principal ─────────────────────────────────────────────────
    private IEnumerator SequenceEntrance()
    {
        // 1. Animar título
        yield return StartCoroutine(AnimarTitulo());

        // 2. Animar botones en cascada
        for (int i = 0; i < botonesRT.Length; i++)
        {
            StartCoroutine(AnimarBoton(i));
            yield return new WaitForSeconds(desfaseBoton);
        }

        // Esperar a que el último botón termine
        yield return new WaitForSeconds(duracionBoton);

        // 3. Botón login y átomos en paralelo
        StartCoroutine(AnimarLogin());
        StartCoroutine(AnimarAtomos());
    }

    // ─── Animadores individuales ─────────────────────────────────────────────
    private IEnumerator AnimarTitulo()
    {
        float t = 0f;
        Vector2 posInicial = tituloRT.anchoredPosition;
        Vector2 posFinal = posInicial - new Vector2(0, offsetTituloY); // posición real

        while (t < 1f)
        {
            t += Time.deltaTime / duracionTitulo;
            float ease = EaseOutBack(Mathf.Clamp01(t));

            if (tituloRT) tituloRT.anchoredPosition = Vector2.Lerp(posInicial, posFinal, ease);
            if (tituloGroup) tituloGroup.alpha = Mathf.Lerp(0f, 1f, Mathf.Clamp01(t * 2f));
            yield return null;
        }
    }

    private IEnumerator AnimarBoton(int indice)
    {
        if (indice >= botonesRT.Length) yield break;

        float t = 0f;
        var rt = botonesRT[indice];
        var grp = botonesGroup[indice];

        Vector2 posInicial = rt.anchoredPosition;
        Vector2 posFinal = posInicial - new Vector2(0, offsetBotonY);

        while (t < 1f)
        {
            t += Time.deltaTime / duracionBoton;
            float ease = EaseOutCubic(Mathf.Clamp01(t));

            rt.anchoredPosition = Vector2.Lerp(posInicial, posFinal, ease);
            grp.alpha = Mathf.Clamp01(t * 1.5f);
            yield return null;
        }
    }

    private IEnumerator AnimarLogin()
    {
        float t = 0f;
        Vector2 posInicial = loginRT.anchoredPosition;
        Vector2 posFinal = posInicial - new Vector2(offsetLoginX, 0);

        while (t < 1f)
        {
            t += Time.deltaTime / duracionLogin;
            float ease = EaseOutCubic(Mathf.Clamp01(t));

            loginRT.anchoredPosition = Vector2.Lerp(posInicial, posFinal, ease);
            loginGroup.alpha = Mathf.Clamp01(t * 2f);
            yield return null;
        }
    }

    private IEnumerator AnimarAtomos()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duracionAtomos;
            foreach (var g in atomosGroup)
                if (g) g.alpha = Mathf.Clamp01(t);
            yield return null;
        }
    }

    // ─── Funciones de easing ─────────────────────────────────────────────────
    private static float EaseOutCubic(float t)
        => 1f - Mathf.Pow(1f - t, 3f);

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    private static void ShiftY(RectTransform rt, float dy)
    {
        var p = rt.anchoredPosition;
        rt.anchoredPosition = new Vector2(p.x, p.y + dy);
    }

    private static void ShiftX(RectTransform rt, float dx)
    {
        var p = rt.anchoredPosition;
        rt.anchoredPosition = new Vector2(p.x + dx, p.y);
    }
}