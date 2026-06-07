using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class LoginManager : MonoBehaviour
{
    [Header("Campos de entrada")]
    [SerializeField] private TMP_InputField inputEmail;
    [SerializeField] private TMP_InputField inputPassword;

    [Header("Botones")]
    [SerializeField] private Button btnIniciarSesion;
    [SerializeField] private Button btnOlvidaste;
    [SerializeField] private Button btnRegistrate;
    [SerializeField] private Button btnCerrar;
    [SerializeField] private Button btnTogglePassword;

    [Header("Escenas")]
    [SerializeField] private string escenaMenu     = "SampleScene";
    [SerializeField] private string escenaRegistro = "RegisterEmailScene";

    [Header("Feedback")]
    [SerializeField] private TextMeshProUGUI txtError;

    // Color del ojo cuando la contraseña está visible vs oculta
    static readonly Color COLOR_OJO_OCULTO  = new Color(1f, 1f, 1f, 0.55f);
    static readonly Color COLOR_OJO_VISIBLE = new Color(0.45f, 0.93f, 1f, 1f);

    private bool passwordVisible = false;

    private void Start()
    {
        if (btnIniciarSesion)  btnIniciarSesion.onClick.AddListener(OnLogin);
        if (btnOlvidaste)      btnOlvidaste.onClick.AddListener(OnOlvidaste);
        if (btnRegistrate)     btnRegistrate.onClick.AddListener(OnRegistrate);
        if (btnCerrar)         btnCerrar.onClick.AddListener(OnCerrar);
        if (btnTogglePassword) btnTogglePassword.onClick.AddListener(OnTogglePassword);

        if (txtError) txtError.gameObject.SetActive(false);

        // Estado inicial: contraseña oculta
        SetPasswordVisible(false);
    }

    // ── Handlers ─────────────────────────────────────────────────────────────
    private void OnLogin()
    {
        string email    = inputEmail    ? inputEmail.text.Trim() : "";
        string password = inputPassword ? inputPassword.text     : "";

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ShowError("Completa todos los campos.");
            return;
        }

        Debug.Log($"[LoginManager] Intentando login con: {email}");
        HideError();
        SceneManager.LoadScene(escenaMenu);
    }

    private void OnOlvidaste()
    {
        Debug.Log("[LoginManager] ¿Olvidaste tu contraseña?");
    }

    private void OnRegistrate()
    {
        SceneManager.LoadScene(escenaRegistro);
    }

    private void OnCerrar()
    {
        SceneManager.LoadScene(escenaMenu);
    }

    private void OnTogglePassword()
    {
        SetPasswordVisible(!passwordVisible);
    }

    // ── Lógica central del toggle ─────────────────────────────────────────────
    private void SetPasswordVisible(bool visible)
    {
        passwordVisible = visible;

        if (inputPassword)
        {
            inputPassword.contentType = visible
                ? TMP_InputField.ContentType.Standard
                : TMP_InputField.ContentType.Password;
            inputPassword.ForceLabelUpdate();
        }

        // Tint del ícono: cian cuando está mostrando, blanco opaco cuando oculto
        if (btnTogglePassword)
        {
            var img = btnTogglePassword.GetComponent<Image>();
            if (img) img.color = visible ? COLOR_OJO_VISIBLE : COLOR_OJO_OCULTO;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private void ShowError(string msg)
    {
        if (txtError)
        {
            txtError.text = msg;
            txtError.gameObject.SetActive(true);
        }
    }

    private void HideError()
    {
        if (txtError) txtError.gameObject.SetActive(false);
    }
}
