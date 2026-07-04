using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Olvidé mi contraseña — paso 3/3: elegir la nueva contraseña.
/// Mismas validaciones y estilo que RegisterPasswordScene (barra de fuerza +
/// 4 requisitos + validación de fortaleza en el backend), más un campo de
/// confirmación ("las contraseñas coinciden"). Al enviar llama a
/// POST /authentication/password/reset (email + code + newPassword).
/// </summary>
public class ForgotPasswordResetManager : MonoBehaviour
{
    [Header("Inputs")]
    [SerializeField] private TMP_InputField inputPassword;
    [SerializeField] private TMP_InputField inputConfirm;
    [SerializeField] private Button btnTogglePassword;
    [SerializeField] private Button btnToggleConfirm;

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

    [Header("Coincidencia")]
    [SerializeField] private TextMeshProUGUI matchLabel;

    [Header("Feedback")]
    [SerializeField] private TextMeshProUGUI txtFeedback;

    [Header("Botones")]
    [SerializeField] private Button btnAtras;
    [SerializeField] private Button btnCambiar;

    [Header("Colores botón Cambiar")]
    [SerializeField] private Color colorActivo   = new Color(0.30f, 0.85f, 0.91f, 1f);
    [SerializeField] private Color colorInactivo = new Color(1f, 1f, 1f, 0.15f);

    [Header("Escenas")]
    [SerializeField] private string escenaAtras     = "ForgotPasswordCodeScene";
    [SerializeField] private string escenaSiguiente = "ForgotPasswordSuccessScene";

    static readonly Color BG_MET     = new Color(0.18f, 0.75f, 0.35f, 0.85f);
    static readonly Color BG_UNMET   = new Color(1f, 1f, 1f, 0.10f);
    static readonly Color COL_WEAK   = new Color(0.90f, 0.20f, 0.20f, 1f);
    static readonly Color COL_MID    = new Color(1.00f, 0.60f, 0.10f, 1f);
    static readonly Color COL_STRONG = new Color(0.18f, 0.80f, 0.35f, 1f);
    static readonly Color COLOR_OK   = new Color(0.18f, 0.80f, 0.44f, 1f);
    static readonly Color COLOR_ERR  = new Color(0.90f, 0.30f, 0.25f, 1f);
    static readonly Color COLOR_INFO = new Color(1f, 1f, 1f, 0.55f);

    const float DEBOUNCE = 0.8f;

    bool _passwordVisible, _confirmVisible;
    bool _passwordValida;   // fortaleza validada por el backend
    bool _enviando;
    Coroutine _debounce;
    int _validationId;

    void Start()
    {
        if (btnAtras)          btnAtras.onClick.AddListener(() => SceneManager.LoadScene(escenaAtras));
        if (btnCambiar)        btnCambiar.onClick.AddListener(OnCambiar);
        if (btnTogglePassword) btnTogglePassword.onClick.AddListener(OnTogglePassword);
        if (btnToggleConfirm)  btnToggleConfirm.onClick.AddListener(OnToggleConfirm);

        if (inputPassword) inputPassword.onValueChanged.AddListener(OnPasswordChanged);
        if (inputConfirm)  inputConfirm.onValueChanged.AddListener(_ => { HideFeedback(); UpdateMatch(); RefreshBoton(); });

        _passwordVisible = _confirmVisible = false;
        ApplyVisibility(inputPassword, btnTogglePassword, false);
        ApplyVisibility(inputConfirm,  btnToggleConfirm,  false);
        UpdateStrength("");
        UpdateMatch();
        SetBotonActivo(false);
        HideFeedback();
    }

    // ── Validación en tiempo real ─────────────────────────────────────────────
    void OnPasswordChanged(string value)
    {
        _validationId++;
        _passwordValida = false;
        HideFeedback();
        UpdateStrength(value);
        UpdateMatch();
        RefreshBoton();

        if (_debounce != null) StopCoroutine(_debounce);
        if (string.IsNullOrEmpty(value)) return;

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
                RefreshBoton();
            },
            onError: (code, detail) =>
            {
                if (myId != _validationId) return;
                _passwordValida = false;
                RefreshBoton();
            });
    }

    // ── Barra de fortaleza (local, idéntica a RegisterPasswordScene) ───────────
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

    void UpdateMatch()
    {
        if (!matchLabel) return;
        string p = inputPassword ? inputPassword.text : "";
        string c = inputConfirm  ? inputConfirm.text  : "";

        if (string.IsNullOrEmpty(c))
        {
            matchLabel.gameObject.SetActive(false);
            return;
        }
        matchLabel.gameObject.SetActive(true);
        bool ok = p == c;
        matchLabel.text  = ok ? "✓  Las contraseñas coinciden" : "✕  Las contraseñas no coinciden";
        matchLabel.color = ok ? COLOR_OK : COLOR_ERR;
    }

    bool PasswordsMatch()
    {
        string p = inputPassword ? inputPassword.text : "";
        string c = inputConfirm  ? inputConfirm.text  : "";
        return !string.IsNullOrEmpty(p) && p == c;
    }

    void RefreshBoton() => SetBotonActivo(_passwordValida && PasswordsMatch() && !_enviando);

    // ── Enviar ────────────────────────────────────────────────────────────────
    void OnCambiar()
    {
        if (_enviando || !_passwordValida || !PasswordsMatch()) return;

        _enviando = true;
        SetBotonActivo(false);
        ShowFeedback("Cambiando contraseña...", COLOR_INFO);

        ApiManager.Instance.ResetPassword(PasswordResetData.Email, PasswordResetData.Code, inputPassword.text,
            onSuccess: _ =>
            {
                if (this == null) return;
                SceneManager.LoadScene(escenaSiguiente);
            },
            onError: (code, detail) =>
            {
                if (this == null) return;
                _enviando = false;
                RefreshBoton();
                ShowFeedback(MapError(detail), COLOR_ERR);
            });
    }

    string MapError(string detail)
    {
        switch (detail)
        {
            case "ERR_CODE_INVALID":
                return "El código ya no es válido. Vuelve a solicitarlo.";
            case "ERR_CODE_EXPIRED":
                return "El código venció. Vuelve a solicitar uno nuevo.";
            case "ERR_PASSWORD_REQUIRED":
                return "Ingresa una nueva contraseña.";
            case "ERR_PASSWORD_TOO_SHORT":
                return "La contraseña es muy corta.";
            case "ERR_PASSWORD_TOO_LONG":
                return "La contraseña es muy larga.";
            case "ERR_PASSWORD_WEAK":
                return "La contraseña es muy débil.";
            default:
                return "No se pudo cambiar la contraseña. Intenta de nuevo.";
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    void OnTogglePassword()
    {
        _passwordVisible = !_passwordVisible;
        ApplyVisibility(inputPassword, btnTogglePassword, _passwordVisible);
    }

    void OnToggleConfirm()
    {
        _confirmVisible = !_confirmVisible;
        ApplyVisibility(inputConfirm, btnToggleConfirm, _confirmVisible);
    }

    void ApplyVisibility(TMP_InputField field, Button toggle, bool visible)
    {
        if (field)
        {
            field.contentType = visible
                ? TMP_InputField.ContentType.Standard
                : TMP_InputField.ContentType.Password;
            field.ForceLabelUpdate();
        }
        if (toggle)
        {
            var img = toggle.GetComponent<Image>();
            if (img) img.color = visible
                ? new Color(0.45f, 0.93f, 1f, 1f)
                : new Color(1f, 1f, 1f, 0.55f);
        }
    }

    void SetBotonActivo(bool activo)
    {
        if (!btnCambiar) return;
        btnCambiar.interactable = activo;
        var cb = btnCambiar.colors;
        cb.normalColor   = activo ? colorActivo : colorInactivo;
        cb.disabledColor = colorInactivo;
        btnCambiar.colors = cb;
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
