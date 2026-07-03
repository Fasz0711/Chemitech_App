using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Modal "Cambia tu contraseña". Valida en cliente (misma lógica que
/// RegisterPasswordScene: 8+ caracteres, mayúscula, número, símbolo) y al enviar
/// llama a PATCH /authentication/password. La validación final (incluye minúscula
/// y la contraseña actual) la hace el backend.
/// </summary>
public class ChangePasswordController : MonoBehaviour
{
    [Header("Modales")]
    [SerializeField] private GameObject formModal;
    [SerializeField] private GameObject successModal;

    [Header("Campos")]
    [SerializeField] private TMP_InputField inputCurrent;
    [SerializeField] private TMP_InputField inputNew;
    [SerializeField] private TMP_InputField inputConfirm;
    [SerializeField] private Button toggleCurrent;
    [SerializeField] private Button toggleNew;
    [SerializeField] private Button toggleConfirm;

    [Header("Fuerza")]
    [SerializeField] private Image[] strengthSegments;   // 5
    [SerializeField] private TextMeshProUGUI strengthLabel;

    [Header("Requisitos")]
    [SerializeField] private GameObject reqPanel;        // checklist (se muestra si no cumple)
    [SerializeField] private Image reqLengthBg, reqUpperBg, reqNumberBg, reqSymbolBg;
    [SerializeField] private TextMeshProUGUI reqLengthTxt, reqUpperTxt, reqNumberTxt, reqSymbolTxt;

    [Header("Banner + botones")]
    [SerializeField] private GameObject banner;
    [SerializeField] private TextMeshProUGUI bannerTxt;
    [SerializeField] private Button btnCancel;
    [SerializeField] private Button btnSubmit;
    [SerializeField] private Button btnSuccessBack;

    static readonly Color BG_MET    = new Color(0.18f, 0.75f, 0.35f, 0.85f);
    static readonly Color BG_UNMET  = new Color(1f, 1f, 1f, 0.10f);
    static readonly Color SEG_ON    = new Color(0.20f, 0.80f, 0.45f, 1f);
    static readonly Color SEG_OFF   = new Color(0.90f, 0.30f, 0.30f, 0.55f);
    static readonly Color COL_WEAK  = new Color(0.92f, 0.32f, 0.32f, 1f);
    static readonly Color COL_MID   = new Color(1.00f, 0.62f, 0.15f, 1f);
    static readonly Color COL_OK    = new Color(0.22f, 0.82f, 0.45f, 1f);
    static readonly Color BANNER_WARN = new Color(0.35f, 0.24f, 0.10f, 0.95f);
    static readonly Color BANNER_ERR  = new Color(0.42f, 0.12f, 0.16f, 0.95f);

    private bool _visCurrent, _visNew, _visConfirm;
    private bool _changing;

    private void Start()
    {
        if (toggleCurrent) toggleCurrent.onClick.AddListener(() => { _visCurrent = !_visCurrent; SetVisible(inputCurrent, toggleCurrent, _visCurrent); });
        if (toggleNew)     toggleNew.onClick.AddListener(() => { _visNew = !_visNew; SetVisible(inputNew, toggleNew, _visNew); });
        if (toggleConfirm) toggleConfirm.onClick.AddListener(() => { _visConfirm = !_visConfirm; SetVisible(inputConfirm, toggleConfirm, _visConfirm); });

        if (inputNew)     inputNew.onValueChanged.AddListener(_ => OnNewChanged());
        if (inputCurrent) inputCurrent.onValueChanged.AddListener(_ => HideBanner());
        if (inputConfirm) inputConfirm.onValueChanged.AddListener(_ => HideBanner());

        if (btnCancel)      btnCancel.onClick.AddListener(Close);
        if (btnSubmit)      btnSubmit.onClick.AddListener(OnSubmit);
        if (btnSuccessBack) btnSuccessBack.onClick.AddListener(Close);

        Close();
    }

    // ── Abrir / cerrar ─────────────────────────────────────────────────────────
    public void Open()
    {
        if (inputCurrent) inputCurrent.text = "";
        if (inputNew)     inputNew.text = "";
        if (inputConfirm) inputConfirm.text = "";
        _visCurrent = _visNew = _visConfirm = false;
        SetVisible(inputCurrent, toggleCurrent, false);
        SetVisible(inputNew, toggleNew, false);
        SetVisible(inputConfirm, toggleConfirm, false);

        _changing = false;
        if (btnSubmit) btnSubmit.interactable = true;
        HideBanner();
        UpdateReqs("");
        if (reqPanel) reqPanel.SetActive(false);

        if (successModal) successModal.SetActive(false);
        if (formModal)    formModal.SetActive(true);
    }

    public void Close()
    {
        if (formModal)    formModal.SetActive(false);
        if (successModal) successModal.SetActive(false);
    }

    // ── Tiempo real ────────────────────────────────────────────────────────────
    private void OnNewChanged()
    {
        HideBanner();
        string pwd = inputNew ? inputNew.text : "";
        UpdateReqs(pwd);

        int unmet = CountUnmet(pwd);
        if (reqPanel) reqPanel.SetActive(pwd.Length > 0 && unmet > 0);
        if (pwd.Length > 0 && unmet > 0)
            ShowBanner($"Tu nueva contraseña no cumple {unmet} requisito{(unmet == 1 ? "" : "s")}. Corrígelos para continuar.", BANNER_WARN);
    }

    private void UpdateReqs(string pwd)
    {
        bool hasLength = pwd.Length >= 8;
        bool hasUpper  = Regex.IsMatch(pwd, "[A-Z]");
        bool hasNumber = Regex.IsMatch(pwd, "[0-9]");
        bool hasSymbol = Regex.IsMatch(pwd, @"[^a-zA-Z0-9]");

        SetReq(reqLengthBg, reqLengthTxt, hasLength, "8+ caracteres");
        SetReq(reqUpperBg,  reqUpperTxt,  hasUpper,  "Una mayúscula");
        SetReq(reqNumberBg, reqNumberTxt, hasNumber, "Un número");
        SetReq(reqSymbolBg, reqSymbolTxt, hasSymbol, "Un símbolo");

        int score = (hasLength ? 1 : 0) + (hasUpper ? 1 : 0) + (hasNumber ? 1 : 0) + (hasSymbol ? 1 : 0);

        int lit; Color labelCol; string label;
        if      (pwd.Length == 0) { lit = 0; labelCol = COL_WEAK; label = ""; }
        else if (score <= 1)      { lit = 1; labelCol = COL_WEAK; label = "Débil"; }
        else if (score == 2)      { lit = 2; labelCol = COL_WEAK; label = "Débil"; }
        else if (score == 3)      { lit = 4; labelCol = COL_MID;  label = "Moderada"; }
        else                      { lit = 5; labelCol = COL_OK;   label = "Fuerte"; }

        Color off = pwd.Length == 0 ? new Color(0.22f, 0.24f, 0.42f, 1f) : SEG_OFF;
        if (strengthSegments != null)
            for (int i = 0; i < strengthSegments.Length; i++)
                if (strengthSegments[i]) strengthSegments[i].color = (i < lit) ? SEG_ON : off;

        if (strengthLabel) { strengthLabel.text = label; strengthLabel.color = labelCol; }
    }

    // ── Enviar ─────────────────────────────────────────────────────────────────
    private void OnSubmit()
    {
        if (_changing) return;

        string cur = inputCurrent ? inputCurrent.text : "";
        string nw  = inputNew ? inputNew.text : "";
        string cf  = inputConfirm ? inputConfirm.text : "";

        if (string.IsNullOrEmpty(cur)) { ShowBanner("Ingresa tu contraseña actual.", BANNER_WARN); return; }

        int unmet = CountUnmet(nw);
        if (unmet > 0)
        {
            if (reqPanel) reqPanel.SetActive(true);
            ShowBanner($"Tu nueva contraseña no cumple {unmet} requisito{(unmet == 1 ? "" : "s")}. Corrígelos para continuar.", BANNER_WARN);
            return;
        }
        if (nw != cf) { ShowBanner("Las contraseñas no coinciden.", BANNER_WARN); return; }

        _changing = true;
        if (btnSubmit) btnSubmit.interactable = false;

        ApiManager.Instance.ChangePassword(SessionData.AccessToken, cur, nw,
            onSuccess: msg =>
            {
                if (this == null) return;
                Debug.Log($"[Settings] Contraseña cambiada: {msg}");
                if (formModal)    formModal.SetActive(false);
                if (successModal) successModal.SetActive(true);
            },
            onError: (code, detail) =>
            {
                if (this == null) return;
                _changing = false;
                if (btnSubmit) btnSubmit.interactable = true;
                ShowBanner(MapError(detail), detail == "ERR_CURRENT_PASSWORD_INVALID" ? BANNER_ERR : BANNER_WARN);
            });
    }

    private string MapError(string detail)
    {
        switch (detail)
        {
            case "ERR_CURRENT_PASSWORD_INVALID": return "La contraseña actual que ingresaste no es correcta. Intenta otra vez.";
            case "ERR_PASSWORD_WEAK":            return "La nueva contraseña es muy débil.";
            case "ERR_PASSWORD_TOO_SHORT":       return "La nueva contraseña es muy corta.";
            case "ERR_PASSWORD_TOO_LONG":        return "La nueva contraseña es muy larga.";
            case "ERR_PASSWORD_REQUIRED":        return "Ingresa una nueva contraseña.";
            case "ERR_TOKEN_EXPIRED":            return "Tu sesión expiró. Vuelve a iniciar sesión.";
            default:                             return "No se pudo cambiar la contraseña. Intenta de nuevo.";
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────
    private static int CountUnmet(string pwd)
    {
        int unmet = 0;
        if (pwd.Length < 8) unmet++;
        if (!Regex.IsMatch(pwd, "[A-Z]")) unmet++;
        if (!Regex.IsMatch(pwd, "[0-9]")) unmet++;
        if (!Regex.IsMatch(pwd, @"[^a-zA-Z0-9]")) unmet++;
        return unmet;
    }

    private void SetReq(Image bg, TextMeshProUGUI txt, bool met, string label)
    {
        if (bg)  bg.color = met ? BG_MET : BG_UNMET;
        if (txt) txt.text = (met ? "✓  " : "○  ") + label;
    }

    private void SetVisible(TMP_InputField field, Button toggle, bool visible)
    {
        if (field)
        {
            field.contentType = visible ? TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password;
            field.ForceLabelUpdate();
        }
        if (toggle)
        {
            var img = toggle.GetComponent<Image>();
            if (img) img.color = visible ? new Color(0.45f, 0.93f, 1f, 1f) : new Color(1f, 1f, 1f, 0.55f);
        }
    }

    private void ShowBanner(string msg, Color bg)
    {
        if (banner) { banner.SetActive(true); var img = banner.GetComponent<Image>(); if (img) img.color = bg; }
        if (bannerTxt) bannerTxt.text = msg;
    }

    private void HideBanner() { if (banner) banner.SetActive(false); }
}
