using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controlador de la pantalla "Mi Perfil".
/// Maneja navegación (atrás / cerrar sesión) y el toggle entre la vista de
/// perfil y la vista de selección de avatar. La data real (stats, nombre,
/// correo) se conectará más adelante; aquí solo se gobierna la interfaz.
/// </summary>
public class PerfilManager : MonoBehaviour
{
    [Header("Navegación")]
    [SerializeField] private Button btnBack;
    [SerializeField] private Button btnCerrarSesion;
    [SerializeField] private string escenaMenu = "SampleScene";

    [Header("Vistas")]
    [SerializeField] private GameObject profileView;
    [SerializeField] private GameObject avatarView;
    [SerializeField] private Button btnCambiarAvatar;
    [SerializeField] private Button btnCancelar;
    [SerializeField] private Button btnGuardar;

    [Header("Selección de avatar (placeholder)")]
    [SerializeField] private Button[] avatarOptions;     // botones de la cuadrícula (desbloqueados)
    [SerializeField] private GameObject[] avatarRings;   // indicador de selección por opción (mismo índice)
    [SerializeField] private Image previewIcon;          // ícono interno del avatar en la vista previa
    [SerializeField] private Image headerAvatarIcon;     // ícono interno del mini avatar del header
    [SerializeField] private Image profileAvatarIcon;    // ícono interno del avatar grande del perfil

    [SerializeField] private int defaultAvatarIndex = 3;

    [Header("Datos del perfil (texto) — se autocompletan por nombre si quedan vacíos")]
    [SerializeField] private TextMeshProUGUI txtUsername;        // tarjeta de perfil
    [SerializeField] private TextMeshProUGUI txtPreviewUsername; // tarjeta de vista previa
    [SerializeField] private TextMeshProUGUI txtEmail;
    [SerializeField] private TextMeshProUGUI txtMolecules;
    [SerializeField] private TextMeshProUGUI txtPlayTime;
    [SerializeField] private TextMeshProUGUI txtMemberSince;
    [SerializeField] private TextMeshProUGUI txtUniverses;

    private int _selectedIndex;
    private int _appliedIndex;

    private void Start()
    {
        if (btnBack)          btnBack.onClick.AddListener(OnBack);
        if (btnCerrarSesion)  btnCerrarSesion.onClick.AddListener(OnCerrarSesion);
        if (btnCambiarAvatar) btnCambiarAvatar.onClick.AddListener(OpenAvatarView);
        // NOTA: la lógica real de logout (endpoint) se conectará luego.
        if (btnCancelar)      btnCancelar.onClick.AddListener(CancelAvatar);
        if (btnGuardar)       btnGuardar.onClick.AddListener(SaveAvatar);

        if (avatarOptions != null)
        {
            for (int i = 0; i < avatarOptions.Length; i++)
            {
                int idx = i;
                if (avatarOptions[i]) avatarOptions[i].onClick.AddListener(() => SelectAvatar(idx));
            }
        }

        _appliedIndex = defaultAvatarIndex;
        ApplyAvatarVisual(_appliedIndex);
        SelectAvatar(_appliedIndex);
        ShowProfile();

        EnsureTextRefs();
        LoadProfile();
    }

    // ── Carga de datos desde el backend ─────────────────────────────────────────
    private void LoadProfile()
    {
        if (string.IsNullOrEmpty(SessionData.UserId))
        {
            Debug.Log("[Perfil] Sin userId en sesión: se mantienen los datos de ejemplo.");
            return;
        }

        ApiManager.Instance.GetProfile(SessionData.UserId,
            onSuccess: p => { if (this != null) Populate(p); },
            onError: (code, detail) =>
                Debug.LogWarning($"[Perfil] No se pudo cargar el perfil · code={code} · {detail}"));
    }

    private void Populate(ApiManager.ProfileResponse p)
    {
        if (p == null) return;

        SetText(txtUsername,        p.username);
        SetText(txtPreviewUsername, p.username);
        SetText(txtEmail,           p.email);
        SetText(txtMolecules,       p.moleculesDiscovered.ToString());
        SetText(txtPlayTime,        FormatPlayTime(p.playTimeSeconds));
        SetText(txtMemberSince,     FormatDate(p.memberSince));
        SetText(txtUniverses,       p.createdUniverses.ToString());

        if (!string.IsNullOrEmpty(p.username)) SessionData.SetUsername(p.username);
    }

    private static void SetText(TextMeshProUGUI t, string value)
    {
        if (t && !string.IsNullOrEmpty(value)) t.text = value;
    }

    private static string FormatPlayTime(int totalSeconds)
    {
        int s = Mathf.Max(0, totalSeconds);
        int days  = s / 86400; s %= 86400;
        int hours = s / 3600;  s %= 3600;
        int mins  = s / 60;
        int secs  = s % 60;

        var sb = new StringBuilder();
        if (days > 0)               sb.Append($"{days}d ");
        if (hours > 0 || days > 0)  sb.Append($"{hours}h ");
        if (mins > 0 || hours > 0 || days > 0) sb.Append($"{mins}m ");
        sb.Append($"{secs}s");
        return sb.ToString();
    }

    private static string FormatDate(string iso)
    {
        if (DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            return dt.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        return string.IsNullOrEmpty(iso) ? "" : iso;
    }

    // ── Navegación ─────────────────────────────────────────────────────────────
    private void OnBack() => SceneManager.LoadScene(escenaMenu);

    private void OnCerrarSesion()
    {
        // TODO: conectar endpoint de logout. Por ahora solo interfaz.
        Debug.Log("[Perfil] Cerrar Sesión (placeholder, sin lógica todavía).");
    }

    // ── Toggle de vistas ───────────────────────────────────────────────────────
    private void ShowProfile()
    {
        if (avatarView)  avatarView.SetActive(false);
        if (profileView) profileView.SetActive(true);
    }

    private void OpenAvatarView()
    {
        SelectAvatar(_appliedIndex); // arranca mostrando el avatar actual
        if (profileView) profileView.SetActive(false);
        if (avatarView)  avatarView.SetActive(true);
    }

    private void CancelAvatar() => ShowProfile();

    private void SaveAvatar()
    {
        _appliedIndex = _selectedIndex;
        ApplyAvatarVisual(_appliedIndex);
        ShowProfile();
    }

    // ── Selección (placeholder visual) ──────────────────────────────────────────
    private void SelectAvatar(int index)
    {
        _selectedIndex = index;

        if (avatarRings != null)
            for (int i = 0; i < avatarRings.Length; i++)
                if (avatarRings[i]) avatarRings[i].SetActive(i == index);

        ApplyTo(previewIcon, index);
    }

    private void ApplyAvatarVisual(int index)
    {
        ApplyTo(previewIcon, index);
        ApplyTo(profileAvatarIcon, index);
        ApplyTo(headerAvatarIcon, index);
    }

    // Copia el ícono y el color de fondo de la opción al avatar destino.
    // targetIcon es el ícono interno; su círculo padre recibe el color de fondo.
    private void ApplyTo(Image targetIcon, int index)
    {
        if (!targetIcon) return;

        var inner = GetOptionInner(index);
        if (inner)
        {
            targetIcon.sprite = inner.sprite;
            targetIcon.color  = inner.color;
        }

        var bg = GetOptionImage(index);
        if (bg && targetIcon.transform.parent)
        {
            var circle = targetIcon.transform.parent.GetComponent<Image>();
            if (circle) circle.color = bg.color;
        }
    }

    private Image GetOptionInner(int index)
    {
        if (avatarOptions == null || index < 0 || index >= avatarOptions.Length || avatarOptions[index] == null)
            return null;
        var t = avatarOptions[index].transform.Find("Inner");
        return t ? t.GetComponent<Image>() : null;
    }

    private Image GetOptionImage(int index)
    {
        if (avatarOptions == null || index < 0 || index >= avatarOptions.Length || avatarOptions[index] == null)
            return null;
        return avatarOptions[index].GetComponent<Image>();
    }

    // ── Autocableado de textos (si quedaron vacíos en el inspector) ─────────────
    private void EnsureTextRefs()
    {
        var pv = profileView ? profileView.transform : FindRootChild("ProfileView");
        var av = avatarView  ? avatarView.transform  : FindRootChild("AvatarView");

        if (!txtUsername && pv)        txtUsername        = FindTMP(pv, "Username");
        if (!txtPreviewUsername && av) txtPreviewUsername = FindTMP(av, "Username");

        if (!txtEmail && pv)
        {
            var field = FindChild(pv, "Field");
            if (field) { var t = field.Find("Text"); if (t) txtEmail = t.GetComponent<TextMeshProUGUI>(); }
        }

        if (!txtMolecules && pv)   txtMolecules   = FindValue(pv, "Stat_Moléculas Descubiertas");
        if (!txtPlayTime && pv)    txtPlayTime    = FindValue(pv, "Stat_Tiempo Jugado");
        if (!txtMemberSince && pv) txtMemberSince = FindValue(pv, "Stat_Usuario desde");
        if (!txtUniverses && pv)   txtUniverses   = FindValue(pv, "Stat_Universos Creados");
    }

    private static Transform FindRootChild(string name)
    {
        var go = GameObject.Find(name);
        return go ? go.transform : null;
    }

    private static TextMeshProUGUI FindTMP(Transform root, string name)
    {
        var t = FindChild(root, name);
        return t ? t.GetComponent<TextMeshProUGUI>() : null;
    }

    private static TextMeshProUGUI FindValue(Transform root, string cardName)
    {
        var card = FindChild(root, cardName);
        if (!card) return null;
        var v = card.Find("Value");
        return v ? v.GetComponent<TextMeshProUGUI>() : null;
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
}
