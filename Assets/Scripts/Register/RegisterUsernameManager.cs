using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Text.RegularExpressions;

public class RegisterUsernameManager : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private TMP_InputField inputUsername;

    [Header("Validación")]
    [SerializeField] private TextMeshProUGUI validationLabel;

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
    [SerializeField] private string escenaAtras = "RegisterPasswordScene";

    static readonly Color BG_MET   = new Color(0.18f, 0.75f, 0.35f, 0.85f);
    static readonly Color BG_UNMET = new Color(1f,    1f,    1f,    0.10f);

    private void Start()
    {
        if (btnAtras)      btnAtras.onClick.AddListener(OnAtras);
        if (btnSiguiente)  btnSiguiente.onClick.AddListener(OnSiguiente);
        if (inputUsername) inputUsername.onValueChanged.AddListener(OnUsernameChanged);

        UpdateValidation("");
    }

    private void OnAtras() => SceneManager.LoadScene(escenaAtras);

    private void OnSiguiente()
    {
        Debug.Log("[RegisterUsername] Siguiente — pendiente.");
    }

    private void OnUsernameChanged(string value) => UpdateValidation(value);

    private void UpdateValidation(string username)
    {
        bool hasLength = username.Length >= 8;
        bool hasUpper  = Regex.IsMatch(username, "[A-Z]");
        bool hasNumber = Regex.IsMatch(username, "[0-9]");
        bool hasSymbol = Regex.IsMatch(username, @"[^a-zA-Z0-9]");

        SetReq(reqLengthBg, reqLengthTxt, hasLength, "8+ caracteres");
        SetReq(reqUpperBg,  reqUpperTxt,  hasUpper,  "Una mayúscula");
        SetReq(reqNumberBg, reqNumberTxt, hasNumber, "Un número");
        SetReq(reqSymbolBg, reqSymbolTxt, hasSymbol, "Un símbolo");

        bool allMet    = hasLength && hasUpper && hasNumber && hasSymbol;
        bool showError = username.Length > 0 && !allMet;

        if (validationLabel) validationLabel.gameObject.SetActive(showError);
    }

    private void SetReq(Image bg, TextMeshProUGUI txt, bool met, string label)
    {
        if (bg)  bg.color = met ? BG_MET : BG_UNMET;
        if (txt) txt.text = (met ? "✓  " : "○  ") + label;
    }
}
