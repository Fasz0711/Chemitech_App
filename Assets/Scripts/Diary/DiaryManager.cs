using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Pantalla "Diario de Moléculas".
///  • Invitado  → vista bloqueada (iniciar sesión / crear cuenta).
///  • Con sesión → diario (por ahora estado vacío) + contador real de moléculas
///    descubiertas (GET /users/profile). La lista de moléculas se hará luego.
/// </summary>
public class DiaryManager : MonoBehaviour
{
    [Header("Header")]
    [SerializeField] private Button btnBack;
    [SerializeField] private GameObject guestBadge;       // "Modo invitado"
    [SerializeField] private GameObject molCountBadge;    // "X Moléculas Descubiertas"
    [SerializeField] private TextMeshProUGUI molCountLabel;

    [Header("Vistas")]
    [SerializeField] private GameObject guestView;        // bloqueado
    [SerializeField] private GameObject loggedInView;     // diario (vacío)

    [Header("Botones invitado")]
    [SerializeField] private Button btnIniciarSesion;
    [SerializeField] private Button btnCrearCuenta;

    [Header("Botón estado vacío")]
    [SerializeField] private Button btnEmpezarJugar;

    [Header("Escenas")]
    [SerializeField] private string escenaMenu     = "SampleScene";
    [SerializeField] private string escenaLogin    = "LoginScene";
    [SerializeField] private string escenaRegistro = "RegisterEmailScene";
    [SerializeField] private string escenaJugar    = "MisUniversosScene";

    private void Start()
    {
        if (btnBack)          btnBack.onClick.AddListener(() => SceneManager.LoadScene(escenaMenu));
        if (btnIniciarSesion) btnIniciarSesion.onClick.AddListener(() => SceneManager.LoadScene(escenaLogin));
        if (btnCrearCuenta)   btnCrearCuenta.onClick.AddListener(() => SceneManager.LoadScene(escenaRegistro));
        if (btnEmpezarJugar)  btnEmpezarJugar.onClick.AddListener(() => SceneManager.LoadScene(escenaJugar));

        bool logged = SessionData.IsLoggedIn;
        if (guestView)     guestView.SetActive(!logged);
        if (loggedInView)  loggedInView.SetActive(logged);
        if (guestBadge)    guestBadge.SetActive(!logged);
        if (molCountBadge) molCountBadge.SetActive(logged);

        if (logged) LoadCount();
    }

    private void LoadCount()
    {
        if (molCountLabel) molCountLabel.text = "0 Moléculas Descubiertas";
        if (string.IsNullOrEmpty(SessionData.UserId)) return;

        ApiManager.Instance.GetProfile(SessionData.UserId,
            onSuccess: p =>
            {
                if (this == null || molCountLabel == null) return;
                molCountLabel.text = $"{p.moleculesDiscovered} Moléculas Descubiertas";
            },
            onError: (code, detail) => Debug.LogWarning($"[Diary] No se pudo obtener el perfil · code={code} · {detail}"));
    }
}
