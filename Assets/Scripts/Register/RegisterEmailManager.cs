using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class RegisterEmailManager : MonoBehaviour
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
    [SerializeField] private string escenaSiguiente = "RegisterPasswordScene";

    static readonly Color COLOR_OK    = new Color(0.18f, 0.80f, 0.44f, 1f);
    static readonly Color COLOR_ERROR = new Color(0.90f, 0.30f, 0.25f, 1f);
    static readonly Color COLOR_INFO  = new Color(1f, 1f, 1f, 0.55f);

    static readonly Regex EMAIL_REGEX = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
    const float DEBOUNCE = 0.8f;

    Coroutine _debounce;
    bool _emailValido;
    int _validationId;

    void Start()
    {
        if (btnAtras)     btnAtras.onClick.AddListener(OnAtras);
        if (btnSiguiente) btnSiguiente.onClick.AddListener(OnSiguiente);
        if (inputEmail)   inputEmail.onValueChanged.AddListener(OnEmailChanged);

        SetBotonActivo(false);
        HideFeedback();
    }

    void OnAtras()
    {
        RegistrationData.Clear();
        SceneManager.LoadScene(escenaAtras);
    }

    void OnSiguiente()
    {
        if (!_emailValido) return;
        RegistrationData.Email = inputEmail.text.Trim();
        SceneManager.LoadScene(escenaSiguiente);
    }

    // ── Validación en tiempo real ─────────────────────────────────────────────

    void OnEmailChanged(string value)
    {
        _validationId++;
        _emailValido = false;
        SetBotonActivo(false);

        if (_debounce != null) StopCoroutine(_debounce);

        string email = value.Trim();

        if (string.IsNullOrEmpty(email))
        {
            HideFeedback();
            return;
        }

        if (!EMAIL_REGEX.IsMatch(email))
        {
            ShowFeedback("Ingresa un correo con formato válido.", COLOR_ERROR);
            return;
        }

        ShowFeedback("Verificando...", COLOR_INFO);
        _debounce = StartCoroutine(DebounceValidar(email));
    }

    IEnumerator DebounceValidar(string email)
    {
        yield return new WaitForSeconds(DEBOUNCE);

        int myId = _validationId;

        ApiManager.Instance.ValidateEmail(email,
            onSuccess: _ =>
            {
                if (myId != _validationId) return;
                _emailValido = true;
                ShowFeedback("¡Correo disponible!", COLOR_OK);
                SetBotonActivo(true);
            },
            onError: (code, detail) =>
            {
                if (myId != _validationId) return;
                _emailValido = false;
                SetBotonActivo(false);

                switch (detail)
                {
                    case "ERR_INVALID_EMAIL":
                        ShowFeedback("El correo no tiene un formato válido.", COLOR_ERROR);
                        break;
                    case "ERR_EMAIL_TAKEN":
                        ShowFeedback("Este correo ya está registrado.", COLOR_ERROR);
                        break;
                    default:
                        ShowFeedback("No se pudo verificar el correo.", COLOR_ERROR);
                        break;
                }
            }
        );
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void SetBotonActivo(bool activo)
    {
        if (!btnSiguiente) return;
        btnSiguiente.interactable = activo;

        var cb = btnSiguiente.colors;
        cb.normalColor   = activo ? colorActivo   : colorInactivo;
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
