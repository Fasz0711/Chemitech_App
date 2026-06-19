using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class RegisterUsernameManager : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private TMP_InputField inputUsername;

    [Header("Feedback")]
    [SerializeField] private TextMeshProUGUI txtFeedback;

    [Header("Requisitos — fondos")]
    [SerializeField] private Image reqLengthBg;
    [SerializeField] private Image reqCharsBg;
    [SerializeField] private Image reqSpacesBg;
    [SerializeField] private Image reqUnderscoreBg;

    [Header("Requisitos — textos")]
    [SerializeField] private TextMeshProUGUI reqLengthTxt;
    [SerializeField] private TextMeshProUGUI reqCharsTxt;
    [SerializeField] private TextMeshProUGUI reqSpacesTxt;
    [SerializeField] private TextMeshProUGUI reqUnderscoreTxt;

    [Header("Botones")]
    [SerializeField] private Button btnAtras;
    [SerializeField] private Button btnSiguiente;

    [Header("Colores botón Siguiente")]
    [SerializeField] private Color colorActivo   = new Color(0.30f, 0.85f, 0.91f, 1f);
    [SerializeField] private Color colorInactivo = new Color(1f, 1f, 1f, 0.15f);

    [Header("Escenas")]
    [SerializeField] private string escenaAtras     = "RegisterPasswordScene";
    [SerializeField] private string escenaSiguiente = "CuentaCreadaScene";

    static readonly Color BG_MET   = new Color(0.18f, 0.75f, 0.35f, 0.85f);
    static readonly Color BG_UNMET = new Color(1f, 1f, 1f, 0.10f);
    static readonly Color COLOR_OK   = new Color(0.18f, 0.80f, 0.44f, 1f);
    static readonly Color COLOR_ERR  = new Color(0.90f, 0.30f, 0.25f, 1f);
    static readonly Color COLOR_INFO = new Color(1f, 1f, 1f, 0.55f);

    static readonly Regex VALID_CHARS = new Regex(@"^[a-zA-Z0-9_ ]*$");

    const float DEBOUNCE = 0.8f;

    bool _usernameValido;
    Coroutine _debounce;
    int _validationId;

    void Start()
    {
        if (btnAtras)      btnAtras.onClick.AddListener(OnAtras);
        if (btnSiguiente)  btnSiguiente.onClick.AddListener(OnSiguiente);
        if (inputUsername) inputUsername.onValueChanged.AddListener(OnUsernameChanged);

        UpdateRequisitos("");
        SetBotonActivo(false);
        HideFeedback();
    }

    void OnAtras() => SceneManager.LoadScene(escenaAtras);

    void OnSiguiente()
    {
        if (!_usernameValido) return;
        RegistrationData.Username = inputUsername.text;
        SceneManager.LoadScene(escenaSiguiente);
    }

    // ── Validación en tiempo real ─────────────────────────────────────────────

    void OnUsernameChanged(string value)
    {
        _validationId++;
        _usernameValido = false;
        SetBotonActivo(false);
        UpdateRequisitos(value);

        if (_debounce != null) StopCoroutine(_debounce);

        if (string.IsNullOrEmpty(value))
        {
            HideFeedback();
            return;
        }

        ShowFeedback("Verificando...", COLOR_INFO);
        _debounce = StartCoroutine(DebounceValidar(value));
    }

    IEnumerator DebounceValidar(string username)
    {
        yield return new WaitForSeconds(DEBOUNCE);

        int myId = _validationId;

        ApiManager.Instance.ValidateUsername(username,
            onSuccess: _ =>
            {
                if (myId != _validationId) return;
                _usernameValido = true;
                ShowFeedback("¡Nombre de usuario disponible!", COLOR_OK);
                SetBotonActivo(true);
            },
            onError: (code, detail) =>
            {
                if (myId != _validationId) return;
                _usernameValido = false;
                SetBotonActivo(false);

                switch (detail)
                {
                    case "ERR_USERNAME_REQUIRED":
                        ShowFeedback("Ingresa un nombre de usuario.", COLOR_ERR);
                        break;
                    case "ERR_USERNAME_TOO_SHORT":
                        ShowFeedback("El nombre de usuario es muy corto.", COLOR_ERR);
                        break;
                    case "ERR_USERNAME_TOO_LONG":
                        ShowFeedback("El nombre de usuario es muy largo.", COLOR_ERR);
                        break;
                    case "ERR_USERNAME_INVALID_CHARS":
                        ShowFeedback("Contiene caracteres no permitidos.", COLOR_ERR);
                        break;
                    case "ERR_USERNAME_STARTS_WITH_SPACE":
                        ShowFeedback("No puede comenzar con un espacio.", COLOR_ERR);
                        break;
                    case "ERR_USERNAME_ENDS_WITH_SPACE":
                        ShowFeedback("No puede terminar con un espacio.", COLOR_ERR);
                        break;
                    case "ERR_USERNAME_CONSECUTIVE_SPACES":
                        ShowFeedback("No puede tener espacios consecutivos.", COLOR_ERR);
                        break;
                    case "ERR_USERNAME_ONLY_UNDERSCORES":
                        ShowFeedback("No puede ser solo guiones bajos.", COLOR_ERR);
                        break;
                    case "ERR_USERNAME_TAKEN":
                        ShowFeedback("Este nombre de usuario ya está en uso.", COLOR_ERR);
                        break;
                    default:
                        ShowFeedback("No se pudo verificar el nombre de usuario.", COLOR_ERR);
                        break;
                }
            }
        );
    }

    // ── Requisitos locales (guía visual inmediata) ────────────────────────────

    void UpdateRequisitos(string u)
    {
        bool empty = string.IsNullOrEmpty(u);

        bool okLength     = u.Length >= 5;
        bool okChars      = !empty && VALID_CHARS.IsMatch(u);
        bool okSpaces     = !empty && !u.StartsWith(" ") && !u.EndsWith(" ") && !u.Contains("  ");
        bool okUnderscore = !empty && Regex.IsMatch(u, "[^_]");

        SetReq(reqLengthBg,     reqLengthTxt,     okLength,     "5+ caracteres");
        SetReq(reqCharsBg,      reqCharsTxt,      okChars,      "Sin caracteres especiales");
        SetReq(reqSpacesBg,     reqSpacesTxt,     okSpaces,     "Sin espacios al inicio/final");
        SetReq(reqUnderscoreBg, reqUnderscoreTxt, okUnderscore, "No solo guiones bajos");
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

    void SetReq(Image bg, TextMeshProUGUI txt, bool met, string label)
    {
        if (bg)  bg.color = met ? BG_MET : BG_UNMET;
        if (txt) txt.text = (met ? "✓  " : "○  ") + label;
    }
}
