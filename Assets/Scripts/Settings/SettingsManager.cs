using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Pantalla "Ajustes". Solo interfaz por ahora: navega entre pestañas
/// (Gráficos / Audio / Cuenta), resalta los segmentos (Bajo/Medio/Alto) y
/// actualiza el valor de los sliders. La funcionalidad real (aplicar/guardar,
/// acciones de cuenta) se hará luego.
/// </summary>
public class SettingsManager : MonoBehaviour
{
    [Header("Header")]
    [SerializeField] private Button btnBack;
    [SerializeField] private string escenaMenu = "SampleScene";

    [Header("Pestañas")]
    [SerializeField] private Button[]     tabButtons;  // Gráficos, Audio, Cuenta
    [SerializeField] private GameObject[] tabPanels;   // mismo orden

    [Header("Gráficos — segmentos")]
    [SerializeField] private Button[] qualityButtons;  // Bajo, Medio, Alto
    [SerializeField] private Button[] fxButtons;       // Bajo, Medio, Alto

    [Header("Sliders")]
    [SerializeField] private Slider sliderBrillo;  [SerializeField] private TextMeshProUGUI lblBrillo;
    [SerializeField] private Slider sliderMusica;  [SerializeField] private TextMeshProUGUI lblMusica;
    [SerializeField] private Slider sliderEfectos; [SerializeField] private TextMeshProUGUI lblEfectos;

    [Header("Cuenta")]
    [SerializeField] private Button btnCerrarSesion;
    [SerializeField] private Button btnCambiarContrasena;
    [SerializeField] private Button btnEliminarCuenta;

    [Header("Cuenta — invitado / logueado")]
    [SerializeField] private GameObject cuentaLoggedView;
    [SerializeField] private GameObject cuentaGuestView;
    [SerializeField] private Button btnGuestLogin;
    [SerializeField] private Button btnGuestRegister;
    [SerializeField] private string escenaLogin    = "LoginScene";
    [SerializeField] private string escenaRegistro = "RegisterEmailScene";

    [Header("Cuenta — datos (auto por nombre si vacío)")]
    [SerializeField] private TextMeshProUGUI txtUsuario;
    [SerializeField] private TextMeshProUGUI txtCorreo;

    [Header("Cerrar Sesión — modal")]
    [SerializeField] private GameObject logoutModal;
    [SerializeField] private Button btnLogoutCancel;
    [SerializeField] private Button btnLogoutConfirm;

    [Header("Eliminar Cuenta — modal")]
    [SerializeField] private GameObject deleteModal;
    [SerializeField] private TMP_InputField deleteInput;
    [SerializeField] private Button btnDeleteCancel;
    [SerializeField] private Button btnDeleteConfirm;

    [Header("Cambiar Contraseña")]
    [SerializeField] private ChangePasswordController changePassword;

    const string DELETE_WORD = "ELIMINAR";

    private bool _loggingOut;
    private bool _deleting;

    static readonly Color SEL_BG    = new Color(0.18f, 0.82f, 0.88f, 1f); // cyan
    static readonly Color UNSEL_BG  = new Color(0.18f, 0.20f, 0.44f, 1f);
    static readonly Color SEL_TXT   = new Color(0.04f, 0.18f, 0.27f, 1f);
    static readonly Color UNSEL_TXT = Color.white;

    private void Start()
    {
        if (btnBack) btnBack.onClick.AddListener(() => SceneManager.LoadScene(escenaMenu));

        Wire(tabButtons, SelectTab);
        Wire(qualityButtons, i => Highlight(qualityButtons, i));
        Wire(fxButtons,      i => Highlight(fxButtons, i));

        WireSlider(sliderBrillo,  lblBrillo);
        WireSlider(sliderMusica,  lblMusica);
        WireSlider(sliderEfectos, lblEfectos);

        if (btnCerrarSesion)      btnCerrarSesion.onClick.AddListener(ShowLogoutModal);
        if (btnCambiarContrasena && changePassword) btnCambiarContrasena.onClick.AddListener(changePassword.Open);
        if (btnEliminarCuenta)    btnEliminarCuenta.onClick.AddListener(ShowDeleteModal);

        if (btnLogoutCancel)  btnLogoutCancel.onClick.AddListener(HideLogoutModal);
        if (btnLogoutConfirm) btnLogoutConfirm.onClick.AddListener(ConfirmLogout);
        HideLogoutModal();

        if (btnDeleteCancel)  btnDeleteCancel.onClick.AddListener(HideDeleteModal);
        if (btnDeleteConfirm) btnDeleteConfirm.onClick.AddListener(ConfirmDelete);
        if (deleteInput)      deleteInput.onValueChanged.AddListener(OnDeleteInputChanged);
        HideDeleteModal();

        if (btnGuestLogin)    btnGuestLogin.onClick.AddListener(() => SceneManager.LoadScene(escenaLogin));
        if (btnGuestRegister) btnGuestRegister.onClick.AddListener(() => SceneManager.LoadScene(escenaRegistro));

        // Cuenta: invitado ve el llamado a acción; logueado ve su info.
        bool logged = SessionData.IsLoggedIn;
        if (cuentaLoggedView) cuentaLoggedView.SetActive(logged);
        if (cuentaGuestView)  cuentaGuestView.SetActive(!logged);

        if (logged) LoadAccount();

        // Estado inicial (según mockups)
        SelectTab(0);
        Highlight(qualityButtons, 2); // Alto
        Highlight(fxButtons, 1);      // Medio
        RefreshLabel(sliderBrillo,  lblBrillo);
        RefreshLabel(sliderMusica,  lblMusica);
        RefreshLabel(sliderEfectos, lblEfectos);
    }

    private void Wire(Button[] btns, System.Action<int> onSelect)
    {
        if (btns == null) return;
        for (int i = 0; i < btns.Length; i++)
        {
            int idx = i;
            if (btns[i]) btns[i].onClick.AddListener(() => onSelect(idx));
        }
    }

    private void WireSlider(Slider s, TextMeshProUGUI lbl)
    {
        if (s != null) s.onValueChanged.AddListener(_ => RefreshLabel(s, lbl));
    }

    private void RefreshLabel(Slider s, TextMeshProUGUI lbl)
    {
        if (s && lbl) lbl.text = Mathf.RoundToInt(s.value).ToString();
    }

    private void SelectTab(int index)
    {
        if (tabPanels != null)
            for (int i = 0; i < tabPanels.Length; i++)
                if (tabPanels[i]) tabPanels[i].SetActive(i == index);
        Highlight(tabButtons, index);
    }

    private void Highlight(Button[] btns, int selected)
    {
        if (btns == null) return;
        for (int i = 0; i < btns.Length; i++)
        {
            if (!btns[i]) continue;
            var img = btns[i].GetComponent<Image>();
            if (img) img.color = (i == selected) ? SEL_BG : UNSEL_BG;
            var lbl = btns[i].GetComponentInChildren<TextMeshProUGUI>();
            if (lbl) lbl.color = (i == selected) ? SEL_TXT : UNSEL_TXT;
        }
    }

    // ── Datos de cuenta (GET /users/profile) ────────────────────────────────────
    private void LoadAccount()
    {
        EnsureAccountRefs();
        if (string.IsNullOrEmpty(SessionData.UserId)) return;

        ApiManager.Instance.GetProfile(SessionData.UserId,
            onSuccess: p =>
            {
                if (this == null || p == null) return;
                if (txtUsuario && !string.IsNullOrEmpty(p.username)) txtUsuario.text = p.username;
                if (txtCorreo  && !string.IsNullOrEmpty(p.email))    txtCorreo.text  = p.email;
                if (!string.IsNullOrEmpty(p.username)) SessionData.SetUsername(p.username);
            },
            onError: (code, detail) => Debug.LogWarning($"[Settings] No se pudo cargar el perfil · code={code} · {detail}"));
    }

    private void EnsureAccountRefs()
    {
        Transform root = cuentaLoggedView ? cuentaLoggedView.transform : null;
        if (root == null)
        {
            var go = GameObject.Find("CuentaLoggedView");
            if (go) root = go.transform;
        }
        if (root == null) return;

        if (!txtUsuario) txtUsuario = FindTMP(root, "UsuarioValue");
        if (!txtCorreo)  txtCorreo  = FindTMP(root, "CorreoValue");
    }

    private static TextMeshProUGUI FindTMP(Transform root, string name)
    {
        var t = FindChild(root, name);
        return t ? t.GetComponent<TextMeshProUGUI>() : null;
    }

    private static Transform FindChild(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform c in root)
        {
            var r = FindChild(c, name);
            if (r) return r;
        }
        return null;
    }

    // ── Cerrar Sesión (modal + endpoint) ────────────────────────────────────────
    private void ShowLogoutModal() { if (logoutModal) logoutModal.SetActive(true); }
    private void HideLogoutModal() { if (logoutModal) logoutModal.SetActive(false); }

    private void ConfirmLogout()
    {
        if (_loggingOut) return;
        _loggingOut = true;
        if (btnLogoutConfirm) btnLogoutConfirm.interactable = false;

        string refreshToken = SessionData.RefreshToken;

        // Sin refreshToken (p. ej. invitado): cerrar localmente directo.
        if (string.IsNullOrEmpty(refreshToken))
        {
            FinishLogout();
            return;
        }

        ApiManager.Instance.Logout(refreshToken,
            onSuccess: msg =>
            {
                Debug.Log($"[Settings] Logout: {msg}");
                FinishLogout();
            },
            onError: (code, detail) =>
            {
                Debug.LogWarning($"[Settings] Logout falló ({code}, {detail}); se cierra sesión localmente igual.");
                FinishLogout();
            });
    }

    // Limpia la sesión local y vuelve al menú (siempre, haya respondido o no el back).
    private void FinishLogout()
    {
        SessionData.Clear();
        SceneManager.LoadScene(escenaMenu);
    }

    // ── Eliminar cuenta (modal + confirmación por texto + endpoint) ─────────────
    private void ShowDeleteModal()
    {
        if (deleteInput) deleteInput.text = "";
        SetDeleteConfirmEnabled(false);
        if (deleteModal) deleteModal.SetActive(true);
    }

    private void HideDeleteModal() { if (deleteModal) deleteModal.SetActive(false); }

    private void OnDeleteInputChanged(string value)
    {
        bool ok = string.Equals(value.Trim(), DELETE_WORD, System.StringComparison.OrdinalIgnoreCase);
        SetDeleteConfirmEnabled(ok);
    }

    private void SetDeleteConfirmEnabled(bool on)
    {
        if (btnDeleteConfirm) btnDeleteConfirm.interactable = on;
    }

    private void ConfirmDelete()
    {
        if (_deleting) return;
        string typed = deleteInput ? deleteInput.text.Trim() : "";
        if (!string.Equals(typed, DELETE_WORD, System.StringComparison.OrdinalIgnoreCase)) return;

        _deleting = true;
        SetDeleteConfirmEnabled(false);

        ApiManager.Instance.DeleteAccount(SessionData.AccessToken,
            onSuccess: msg =>
            {
                if (this == null) return;
                Debug.Log($"[Settings] Cuenta eliminada: {msg}");
                SessionData.Clear();
                SceneManager.LoadScene(escenaMenu);
            },
            onError: (code, detail) =>
            {
                Debug.LogWarning($"[Settings] No se pudo eliminar la cuenta ({code}, {detail}). La cuenta NO fue eliminada.");
                _deleting = false;
                SetDeleteConfirmEnabled(true); // permite reintentar
            });
    }
}
