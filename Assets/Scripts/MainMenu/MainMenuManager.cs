using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

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
    [SerializeField] private TextMeshProUGUI txtSesionLabel; // texto del botón (fallback: hijo del botón)

    // ─── Nombres de Escenas ─────────────────────────────────────────────────
    [Header("Nombres de Escenas")]
    [SerializeField] private string escenaJugar = "GuestPromptScene";          // sin sesión
    [SerializeField] private string escenaJugarLogueado = "MisUniversosScene"; // con sesión iniciada
    [SerializeField] private string escenaDiario = "DiaryScene";
    [SerializeField] private string escenaAjustes = "SettingsScene";
    [SerializeField] private string escenaLogin = "LoginScene";
    [SerializeField] private string escenaPerfil = "PerfilScene"; // destino al tocar con sesión iniciada (pendiente)

    // Texto por defecto cuando NO hay sesión iniciada.
    private const string TEXTO_INICIAR_SESION = "Iniciar Sesión";

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

        // Mostrar el nombre del usuario si hay sesión iniciada
        RefreshSessionLabel();

        // Lanzar animación de entrada si existe
        menuAnimator?.PlayEntrance();
    }

    // ─── Etiqueta del botón de sesión ───────────────────────────────────────
    private void RefreshSessionLabel()
    {
        // Fallback: si no se cableó, tomamos el Label del propio botón.
        if (txtSesionLabel == null && btnIniciarSesion != null)
            txtSesionLabel = btnIniciarSesion.GetComponentInChildren<TextMeshProUGUI>(true);

        if (txtSesionLabel == null) return;

        // Sin sesión → texto por defecto.
        if (!SessionData.IsLoggedIn)
        {
            txtSesionLabel.text = TEXTO_INICIAR_SESION;
            return;
        }

        // Con sesión y nombre ya cacheado → usarlo directo.
        if (!string.IsNullOrEmpty(SessionData.Username))
        {
            txtSesionLabel.text = SessionData.Username;
            return;
        }

        // Con sesión pero sin nombre → pedirlo al backend con el userId del login.
        if (string.IsNullOrEmpty(SessionData.UserId)) return;

        ApiManager.Instance.GetProfile(SessionData.UserId,
            onSuccess: profile =>
            {
                if (this == null || txtSesionLabel == null) return;      // escena ya cambió
                if (profile == null || string.IsNullOrEmpty(profile.username)) return; // sin nombre → dejar default
                SessionData.SetUsername(profile.username);
                txtSesionLabel.text = profile.username;
            },
            onError: (code, detail) =>
            {
                Debug.LogWarning($"[MainMenu] No se pudo obtener el perfil · code={code} · {detail}");
            });
    }

    // ─── Handlers ───────────────────────────────────────────────────────────
    private void OnJugarClicked()
    {
        PlayButtonSound();

        // Con sesión iniciada → saltar el prompt de invitado e ir directo al juego.
        // Sin sesión → mostrar GuestPromptScene (permite loguearse o seguir como invitado).
        SceneManager.LoadScene(SessionData.IsLoggedIn ? escenaJugarLogueado : escenaJugar);
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

        // Con sesión iniciada → pantalla de perfil/cuenta (pendiente de implementar).
        if (SessionData.IsLoggedIn)
        {
            if (!string.IsNullOrEmpty(escenaPerfil) && Application.CanStreamedLevelBeLoaded(escenaPerfil))
                SceneManager.LoadScene(escenaPerfil);
            else
                Debug.Log($"[MainMenu] Sesión iniciada: '{escenaPerfil}' aún no existe en Build Settings.");
            return;
        }

        // Sin sesión → ir a login.
        SceneManager.LoadScene(escenaLogin);
    }

    // ─── Audio (placeholder) ────────────────────────────────────────────────
    private void PlayButtonSound()
    {
        // TODO: AudioManager.Instance.PlaySFX("btn_click");
    }
}