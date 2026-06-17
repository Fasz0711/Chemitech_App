using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class RegisterPasswordManager : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private TMP_InputField inputPassword;
    [SerializeField] private Button btnTogglePassword;

    [Header("Fortaleza")]
    [SerializeField] private Image strengthBarFill;
    [SerializeField] private TextMeshProUGUI strengthLabel;

    [Header("Requisitos — fondos")]
    [SerializeField] private Image reqLengthBg;
    [SerializeField] private Image reqUpperBg;
    [SerializeField] private Image reqNumberBg;
    [SerializeField] private Image reqSymbolBg;

    [Header("Requisitos — textos")]
    [SerializeField] private TextMeshProUGUI reqLengthTxt;
    [SerializeField] private TextMeshProUGUI reqUpperTxt;
    [SerializeField] private TextMeshProUGUI reqNumberTxt;
    [SerializeField] private TextMeshProUGUI reqSymbolTxt;

    [Header("Feedback")]
    [SerializeField] private TextMeshProUGUI txtFeedback;

    [Header("Botones")]
    [SerializeField] private Button btnAtras;
    [SerializeField] private Button btnSiguiente;

    [Header("Colores botón Siguiente")]
    [SerializeField] private Color colorActivo   = new Color(0.30f, 0.85f, 0.91f, 1f);
    [SerializeField] private Color colorInactivo = new Color(1f, 1f, 1f, 0.15f);

    [Header("Escenas")]
    [SerializeField] private string escenaAtras     = "RegisterEmailScene";
    [SerializeField] private string escenaSiguiente = "RegisterUsernameScene";

    static readonly Color BG_MET     = new Color(0.18f, 0.75f, 0.35f, 0.85f);
    static readonly Color BG_UNMET   = new Color(1f, 1f, 1f, 0.10f);
    static readonly Color COL_WEAK   = new Color(0.90f, 0.20f, 0.20f, 1f);
    static readonly Color COL_MID    = new Color(1.00f, 0.60f, 0.10f, 1f);
    static readonly Color COL_STRONG = new Color(0.18f, 0.80f, 0.35f, 1f);
    static readonly Color COLOR_OK   = new Color(0.18f, 0.80f, 0.44f, 1f);
    static readonly Color COLOR_ERR  = new Color(0.90f, 0.30f, 0.25f, 1f);
    static readonly Color COLOR_INFO = new Color(1f, 1f, 1f, 0.55f);

    const float DEBOUNCE = 0.8f;

    bool _passwordVisible;
    bool _passwordValida;
    Coroutine _debounce;
    int _validationId;

    void Start()
    {
        if (btnAtras)          btnAtras.onClick.AddListener(OnAtras);
        if (btnSiguiente)      btnSiguiente.onClick.AddListener(OnSiguiente);
        if (btnTogglePassword) btnTogglePassword.onClick.AddListener(OnToggle);
        if (inputPassword)     inputPassword.onValueChanged.AddListener(OnPasswordChanged);

        SetPasswordVisible(false);
        UpdateStrength("");
        SetBotonActivo(false);
        HideFeedback();
    }

    void OnAtras() => SceneManager.LoadScene(escenaAtras);

    void OnSiguiente()
    {
        if (!_passwordValida) return;
        RegistrationData.Password = inputPassword.text;
        SceneManager.LoadScene(escenaSiguiente);
    }

    void OnToggle() => SetPasswordVisible(!_passwordVisible);

    // ── Validación en tiempo real ─────────────────────────────────────────────

    void OnPasswordChanged(string value)
    {
        _validationId++;
        _passwordValida = false;
        SetBotonActivo(false);
        UpdateStrength(value);

        if (_debounce != null) StopCoroutine(_debounce);

        if (string.IsNullOrEmpty(value))
        {
            HideFeedback();
            return;
        }

        ShowFeedback("Verificando...", COLOR_INFO);
        _debounce = StartCoroutine(DebounceValidar(value));
    }

    IEnumerator DebounceValidar(string password)
    {
        yield return new WaitForSeconds(DEBOUNCE);

        int myId = _validationId;

        ApiManager.Instance.ValidatePassword(password,
            onSuccess: _ =>
            {
                if (myId != _validationId) return;
                _passwordValida = true;
                ShowFeedback("¡Contraseña segura!", COLOR_OK);
                SetBotonActivo(true);
            },
            onError: (code, detail) =>
            {
                if (myId != _validationId) return;
                _passwordValida = false;
                SetBotonActivo(false);

                switch (detail)
                {
                    case "ERR_PASSWORD_REQUIRED":
                        ShowFeedback("Ingresa una contraseña.", COLOR_ERR);
                        break;
                    case "ERR_PASSWORD_TOO_SHORT":
                        ShowFeedback("La contraseña es muy corta.", COLOR_ERR);
                        break;
                    case "ERR_PASSWORD_TOO_LONG":
                        ShowFeedback("La contraseña es muy larga.", COLOR_ERR);
                        break;
                    case "ERR_PASSWORD_WEAK":
                        ShowFeedback("La contraseña es muy débil.", COLOR_ERR);
                        break;
                    default:
                        ShowFeedback("No se pudo verificar la contraseña.", COLOR_ERR);
                        break;
                }
            }
        );
    }

    // ── Barra de fortaleza (local) ────────────────────────────────────────────

    void UpdateStrength(string pwd)
    {
        bool hasLength = pwd.Length >= 8;
        bool hasUpper  = Regex.IsMatch(pwd, "[A-Z]");
        bool hasNumber = Regex.IsMatch(pwd, "[0-9]");
        bool hasSymbol = Regex.IsMatch(pwd, @"[^a-zA-Z0-9]");

        SetReq(reqLengthBg, reqLengthTxt, hasLength, "8+ caracteres");
        SetReq(reqUpperBg,  reqUpperTxt,  hasUpper,  "Una mayúscula");
        SetReq(reqNumberBg, reqNumberTxt, hasNumber, "Un número");
        SetReq(reqSymbolBg, reqSymbolTxt, hasSymbol, "Un símbolo");

        int score = (hasLength ? 1 : 0) + (hasUpper ? 1 : 0)
                  + (hasNumber ? 1 : 0) + (hasSymbol ? 1 : 0);

        float fill; Color color; string label;
        if      (pwd.Length == 0) { fill = 0f;    color = COL_WEAK;   label = ""; }
        else if (score <= 1)      { fill = 0.25f; color = COL_WEAK;   label = "Débil"; }
        else if (score == 2)      { fill = 0.50f; color = COL_MID;    label = "Moderada"; }
        else if (score == 3)      { fill = 0.75f; color = COL_MID;    label = "Fuerte"; }
        else                      { fill = 1.00f; color = COL_STRONG; label = "Muy fuerte"; }

        if (strengthBarFill) { strengthBarFill.fillAmount = fill; strengthBarFill.color = color; }
        if (strengthLabel)   { strengthLabel.text = label; strengthLabel.color = color; }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void SetPasswordVisible(bool visible)
    {
        _passwordVisible = visible;
        if (inputPassword)
        {
            inputPassword.contentType = visible
                ? TMP_InputField.ContentType.Standard
                : TMP_InputField.ContentType.Password;
            inputPassword.ForceLabelUpdate();
        }
        if (btnTogglePassword)
        {
            var img = btnTogglePassword.GetComponent<Image>();
            if (img) img.color = visible
                ? new Color(0.45f, 0.93f, 1f, 1f)
                : new Color(1f, 1f, 1f, 0.55f);
        }
    }

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
