using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Olvidé mi contraseña — paso 2/3: verificar el código de 6 caracteres.
/// Seis casillas con auto-avance, retroceso con backspace y soporte de pegado.
/// El código es alfanumérico (0-9, a-z, A-Z). "Verificar" llama a
/// POST /authentication/password/reset/verify (no consume el código).
/// Incluye "Reenviar" con cuenta regresiva que re-solicita el código.
/// </summary>
public class ForgotPasswordCodeManager : MonoBehaviour
{
    [Header("Casillas del código (6)")]
    [SerializeField] private TMP_InputField[] boxes;

    [Header("Textos")]
    [SerializeField] private TextMeshProUGUI txtEmail;     // muestra el correo destino
    [SerializeField] private TextMeshProUGUI txtFeedback;  // errores / info
    [SerializeField] private TextMeshProUGUI txtReenviar;  // "Reenviar" / "Reenviar (46s)"

    [Header("Botones")]
    [SerializeField] private Button btnAtras;
    [SerializeField] private Button btnVerificar;
    [SerializeField] private Button btnReenviar;

    [Header("Colores botón Verificar")]
    [SerializeField] private Color colorActivo   = new Color(0.30f, 0.85f, 0.91f, 1f);
    [SerializeField] private Color colorInactivo = new Color(1f, 1f, 1f, 0.15f);

    [Header("Reenviar")]
    [SerializeField] private float resendCooldown = 60f;

    [Header("Escenas")]
    [SerializeField] private string escenaAtras     = "ForgotPasswordEmailScene";
    [SerializeField] private string escenaSiguiente = "ForgotPasswordResetScene";

    static readonly Color COLOR_ERROR = new Color(0.90f, 0.30f, 0.25f, 1f);
    static readonly Color COLOR_INFO  = new Color(1f, 1f, 1f, 0.55f);
    static readonly Color COLOR_OK    = new Color(0.18f, 0.80f, 0.44f, 1f);
    static readonly Color RESEND_ON   = new Color(1f, 0.78f, 0.25f, 1f);   // ámbar (disponible)
    static readonly Color RESEND_OFF  = new Color(1f, 1f, 1f, 0.45f);      // gris (en espera)

    bool  _suppress;      // evita recursión al setear casillas por código
    bool  _verificando;
    float _cooldownLeft;

    void Start()
    {
        if (txtEmail) txtEmail.text = PasswordResetData.Email;

        if (boxes != null)
            for (int i = 0; i < boxes.Length; i++)
            {
                int idx = i;
                if (boxes[i]) boxes[i].onValueChanged.AddListener(v => OnBoxChanged(idx, v));
            }

        if (btnAtras)     btnAtras.onClick.AddListener(() => SceneManager.LoadScene(escenaAtras));
        if (btnVerificar) btnVerificar.onClick.AddListener(OnVerificar);
        if (btnReenviar)  btnReenviar.onClick.AddListener(OnReenviar);

        HideFeedback();
        SetBotonActivo(false);
        StartCooldown();
        FocusBox(0);
    }

    // ── Casillas ──────────────────────────────────────────────────────────────
    void OnBoxChanged(int i, string raw)
    {
        if (_suppress) return;
        HideFeedback();

        string s = SoloAlfanum(raw);

        // Vacía (backspace): retrocede a la casilla anterior.
        if (s.Length == 0)
        {
            UpdateState();
            FocusBox(i - 1);
            return;
        }

        // Reparte 1 carácter por casilla desde 'i' (soporta pegar el código completo).
        _suppress = true;
        int box = i;
        foreach (char c in s)
        {
            if (box >= boxes.Length) break;
            if (boxes[box]) boxes[box].text = c.ToString();
            box++;
        }
        _suppress = false;

        UpdateState();
        FocusBox(Mathf.Min(box, boxes.Length - 1));
    }

    static string SoloAlfanum(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
            if ((c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
                sb.Append(c);
        return sb.ToString();
    }

    void FocusBox(int index)
    {
        if (boxes == null || index < 0 || index >= boxes.Length || boxes[index] == null) return;
        var b = boxes[index];
        b.Select();
        b.ActivateInputField();
        b.caretPosition = b.text.Length;
    }

    string CurrentCode()
    {
        if (boxes == null) return "";
        var sb = new StringBuilder(boxes.Length);
        foreach (var b in boxes) sb.Append(b ? b.text : "");
        return sb.ToString();
    }

    bool AllFilled()
    {
        if (boxes == null || boxes.Length == 0) return false;
        foreach (var b in boxes) if (b == null || string.IsNullOrEmpty(b.text)) return false;
        return true;
    }

    void UpdateState() => SetBotonActivo(AllFilled() && !_verificando);

    // ── Verificar ─────────────────────────────────────────────────────────────
    void OnVerificar()
    {
        if (_verificando || !AllFilled()) return;

        string code  = CurrentCode();
        string email = PasswordResetData.Email;

        _verificando = true;
        SetBotonActivo(false);
        ShowFeedback("Verificando código...", COLOR_INFO);

        ApiManager.Instance.VerifyPasswordResetCode(email, code,
            onSuccess: _ =>
            {
                if (this == null) return;
                PasswordResetData.Code = code;
                SceneManager.LoadScene(escenaSiguiente);
            },
            onError: (http, detail) =>
            {
                if (this == null) return;
                _verificando = false;
                UpdateState();
                ShowFeedback(MapError(detail), COLOR_ERROR);
            });
    }

    string MapError(string detail)
    {
        switch (detail)
        {
            case "ERR_CODE_INVALID": return "Código incorrecto. Revísalo e intenta otra vez.";
            case "ERR_CODE_EXPIRED": return "El código venció. Pulsa \"Reenviar\" para pedir uno nuevo.";
            default:                 return "No se pudo verificar el código. Intenta de nuevo.";
        }
    }

    // ── Reenviar (con cuenta regresiva) ────────────────────────────────────────
    void OnReenviar()
    {
        if (_cooldownLeft > 0f) return;

        ShowFeedback("Reenviando código...", COLOR_INFO);
        if (btnReenviar) btnReenviar.interactable = false;

        ApiManager.Instance.RequestPasswordReset(PasswordResetData.Email,
            onSuccess: _ =>
            {
                if (this == null) return;
                ShowFeedback("Te enviamos un código nuevo.", COLOR_OK);
                ClearBoxes();
                StartCooldown();
                FocusBox(0);
            },
            onError: (http, detail) =>
            {
                if (this == null) return;
                if (btnReenviar) btnReenviar.interactable = true;
                ShowFeedback("No se pudo reenviar el código. Intenta de nuevo.", COLOR_ERROR);
            });
    }

    void StartCooldown()
    {
        _cooldownLeft = resendCooldown;
        if (btnReenviar) btnReenviar.interactable = false;
        UpdateReenviarLabel();
    }

    void Update()
    {
        if (_cooldownLeft <= 0f) return;
        _cooldownLeft -= Time.deltaTime;
        if (_cooldownLeft <= 0f)
        {
            _cooldownLeft = 0f;
            if (btnReenviar) btnReenviar.interactable = true;
        }
        UpdateReenviarLabel();
    }

    void UpdateReenviarLabel()
    {
        if (!txtReenviar) return;
        if (_cooldownLeft > 0f)
        {
            txtReenviar.text  = $"Reenviar ({Mathf.CeilToInt(_cooldownLeft)}s)";
            txtReenviar.color = RESEND_OFF;
        }
        else
        {
            txtReenviar.text  = "Reenviar";
            txtReenviar.color = RESEND_ON;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    void ClearBoxes()
    {
        _suppress = true;
        if (boxes != null) foreach (var b in boxes) if (b) b.text = "";
        _suppress = false;
        UpdateState();
    }

    void SetBotonActivo(bool activo)
    {
        if (!btnVerificar) return;
        btnVerificar.interactable = activo;
        var cb = btnVerificar.colors;
        cb.normalColor   = activo ? colorActivo : colorInactivo;
        cb.disabledColor = colorInactivo;
        btnVerificar.colors = cb;
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
