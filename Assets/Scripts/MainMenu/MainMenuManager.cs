using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controlador principal del Menú. Maneja los eventos de los botones
/// y la navegación entre escenas.
/// Adjuntar a: GameObject vacío "MainMenuManager" en la raíz de la escena.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    // ─── Referencia a Botones ───────────────────────────────────────────────
    [Header("Botones Principales")]
    [SerializeField] private Button btnJugar;
    [SerializeField] private Button btnDiario;
    [SerializeField] private Button btnAjustes;

    [Header("Botón Sesión (esquina inferior derecha)")]
    [SerializeField] private Button btnIniciarSesion;

    // ─── Nombres de Escenas ─────────────────────────────────────────────────
    [Header("Nombres de Escenas")]
    [SerializeField] private string escenaJugar = "GuestPromptScene";
    [SerializeField] private string escenaDiario = "DiaryScene";
    [SerializeField] private string escenaAjustes = "SettingsScene";
    [SerializeField] private string escenaLogin = "LoginScene";

    // ─── Animador de entrada ────────────────────────────────────────────────
    [Header("Animador (opcional)")]
    [SerializeField] private MenuAnimator menuAnimator;

    // ───────────────────────────────────────────────────────────────────────
    private void Start()
    {
        // Registrar listeners
        btnJugar.onClick.AddListener(OnJugarClicked);
        btnDiario.onClick.AddListener(OnDiarioClicked);
        btnAjustes.onClick.AddListener(OnAjustesClicked);
        btnIniciarSesion.onClick.AddListener(OnIniciarSesionClicked);

        // Lanzar animación de entrada si existe
        menuAnimator?.PlayEntrance();
    }

    // ─── Handlers ───────────────────────────────────────────────────────────
    private void OnJugarClicked()
    {
        PlayButtonSound();
        SceneManager.LoadScene(escenaJugar);
    }

    private void OnDiarioClicked()
    {
        PlayButtonSound();
        SceneManager.LoadScene(escenaDiario);
    }

    private void OnAjustesClicked()
    {
        PlayButtonSound();
        SceneManager.LoadScene(escenaAjustes);
    }

    private void OnIniciarSesionClicked()
    {
        PlayButtonSound();
        SceneManager.LoadScene(escenaLogin);
    }

    // ─── Audio (placeholder) ────────────────────────────────────────────────
    private void PlayButtonSound()
    {
        // TODO: AudioManager.Instance.PlaySFX("btn_click");
    }
}