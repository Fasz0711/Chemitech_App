using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Text.RegularExpressions;

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

    [Header("Botones")]
    [SerializeField] private Button btnAtras;
    [SerializeField] private Button btnSiguiente;

    [Header("Escenas")]
    [SerializeField] private string escenaAtras = "RegisterEmailScene";

    static readonly Color BG_MET    = new Color(0.18f, 0.75f, 0.35f, 0.85f);
    static readonly Color BG_UNMET  = new Color(1f,    1f,    1f,    0.10f);
    static readonly Color COL_WEAK   = new Color(0.90f, 0.20f, 0.20f, 1f);
    static readonly Color COL_MID    = new Color(1.00f, 0.60f, 0.10f, 1f);
    static readonly Color COL_STRONG = new Color(0.18f, 0.80f, 0.35f, 1f);

    private bool passwordVisible = false;

    private void Start()
    {
        if (btnAtras)          btnAtras.onClick.AddListener(OnAtras);
        if (btnSiguiente)      btnSiguiente.onClick.AddListener(OnSiguiente);
        if (btnTogglePassword) btnTogglePassword.onClick.AddListener(OnToggle);
        if (inputPassword)     inputPassword.onValueChanged.AddListener(OnPasswordChanged);

        SetPasswordVisible(false);
        UpdateStrength("");
    }

    private void OnAtras() => SceneManager.LoadScene(escenaAtras);

    private void OnSiguiente()
    {
        // TODO: cargar siguiente paso del registro
        Debug.Log("[RegisterPassword] Siguiente — pendiente.");
    }

    private void OnToggle() => SetPasswordVisible(!passwordVisible);

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
        if (btnTogglePassword)
        {
            var img = btnTogglePassword.GetComponent<Image>();
            if (img) img.color = visible
                ? new Color(0.45f, 0.93f, 1f, 1f)
                : new Color(1f, 1f, 1f, 0.55f);
        }
    }

    private void OnPasswordChanged(string value) => UpdateStrength(value);

    private void UpdateStrength(string pwd)
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

        if (strengthBarFill)  { strengthBarFill.fillAmount = fill; strengthBarFill.color = color; }
        if (strengthLabel)    { strengthLabel.text = label; strengthLabel.color = color; }
    }

    private void SetReq(Image bg, TextMeshProUGUI txt, bool met, string label)
    {
        if (bg)  bg.color  = met ? BG_MET : BG_UNMET;
        if (txt) txt.text  = (met ? "✓  " : "○  ") + label;
    }
}
