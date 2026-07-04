using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Olvidé mi contraseña — paso 1/3: pedir el correo.
/// Valida el formato en cliente y, al pulsar "Siguiente", llama a
/// POST /authentication/password/reset/request (que SIEMPRE responde igual,
/// exista o no la cuenta) y avanza a la pantalla del código.
/// </summary>
public class ForgotPasswordEmailManager : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private TMP_InputField inputEmail;

    [Header("Feedback")]
    [SerializeField] private TextMeshProUGUI txtFeedback;

    [Header("Botones")]
    [SerializeField] private Button btnAtras;
    [SerializeField] private Button btnSiguiente;

    [Header("Colores botón Siguiente")]
    [SerializeField] private Color colorActivo   = new Color(0.30f, 0.85f, 0.91f, 1f);
    [SerializeField] private Color colorInactivo = new Color(1f, 1f, 1f, 0.15f);

    [Header("Escenas")]
    [SerializeField] private string escenaAtras     = "LoginScene";
    [SerializeField] private string escenaSiguiente = "ForgotPasswordCodeScene";

    static readonly Color COLOR_ERROR = new Color(0.90f, 0.30f, 0.25f, 1f);
    static readonly Color COLOR_INFO  = new Color(1f, 1f, 1f, 0.55f);

    static readonly Regex EMAIL_REGEX = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");

    bool _emailValido;
    bool _enviando;

    void Start()
    {
        if (btnAtras)     btnAtras.onClick.AddListener(OnAtras);
        if (btnSiguiente) btnSiguiente.onClick.AddListener(OnSiguiente);
        if (inputEmail)   inputEmail.onValueChanged.AddListener(OnEmailChanged);

        // Precarga el correo si venía del login.
        if (inputEmail && !string.IsNullOrEmpty(PasswordResetData.Email))
            inputEmail.text = PasswordResetData.Email;

        HideFeedback();
        Validar(inputEmail ? inputEmail.text : "");
    }

    void OnAtras() => SceneManager.LoadScene(escenaAtras);

    void OnEmailChanged(string value)
    {
        HideFeedback();
        Validar(value);
    }

    void Validar(string value)
    {
        string email = (value ?? "").Trim();
        _emailValido = !string.IsNullOrEmpty(email) && EMAIL_REGEX.IsMatch(email);
        SetBotonActivo(_emailValido);
    }

    void OnSiguiente()
    {
        if (!_emailValido || _enviando) return;

        string email = inputEmail.text.Trim();
        _enviando = true;
        SetBotonActivo(false);
        ShowFeedback("Enviando código...", COLOR_INFO);

        ApiManager.Instance.RequestPasswordReset(email,
            onSuccess: _ =>
            {
                if (this == null) return;
                PasswordResetData.Email = email;
                PasswordResetData.Code  = "";
                SceneManager.LoadScene(escenaSiguiente);
            },
            onError: (code, detail) =>
            {
                if (this == null) return;
                _enviando = false;
                SetBotonActivo(true);
                ShowFeedback(MapError(detail), COLOR_ERROR);
            });
    }

    string MapError(string detail)
    {
        switch (detail)
        {
            case "ERR_EMAIL_REQUIRED":
            case "ERR_INVALID_EMAIL":
                return "Ingresa un correo con formato válido.";
            case "ERR_SMTP_NOT_CONFIGURED":
                return "El servicio de correo no está disponible ahora. Intenta más tarde.";
            default:
                return "No se pudo enviar el código. Intenta de nuevo.";
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    void SetBotonActivo(bool activo)
    {
        if (!btnSiguiente) return;
        btnSiguiente.interactable = activo;
        var cb = btnSiguiente.colors;
        cb.normalColor   = activo ? colorActivo : colorInactivo;
        cb.disabledColor = colorInactivo;
        btnSiguiente.colors = cb;
    }

    void ShowFeedback(string msg, Color color)
    {
        if (!txtFeedback) return;
        txtFeedback.text  = msg;
        txtFeedback.color = color;
        txtFeedback.gameObject.SetActive(true);
    }

    void HideFeedback()
    {
        if (txtFeedback) txtFeedback.gameObject.SetActive(false);
    }
}
