using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class CuentaCreadaManager : MonoBehaviour
{
    [Header("Botones")]
    [SerializeField] private Button btnExplorar;
    [SerializeField] private Button btnReintentar;

    [Header("Feedback")]
    [SerializeField] private TextMeshProUGUI txtStatus;

    [Header("Escenas")]
    [SerializeField] private string escenaSiguiente = "MisUniversosScene";
    [SerializeField] private string escenaError     = "RegisterEmailScene";

    static readonly Color COLOR_OK   = new Color(0.18f, 0.80f, 0.44f, 1f);
    static readonly Color COLOR_ERR  = new Color(0.90f, 0.30f, 0.25f, 1f);
    static readonly Color COLOR_INFO = new Color(1f, 1f, 1f, 0.55f);

    void Start()
    {
        if (btnExplorar)   btnExplorar.onClick.AddListener(() => SceneManager.LoadScene(escenaSiguiente));
        if (btnReintentar) btnReintentar.onClick.AddListener(CrearCuenta);

        CrearCuenta();
    }

    void CrearCuenta()
    {
        SetBotonExplorar(false);
        if (btnReintentar) btnReintentar.gameObject.SetActive(false);
        ShowStatus("Creando tu cuenta...", COLOR_INFO);

        ApiManager.Instance.CreateAccount(
            RegistrationData.Email,
            RegistrationData.Password,
            RegistrationData.Username,
            onSuccess: userId =>
            {
                // La cuenta se creó, pero /authentication/account NO devuelve tokens.
                // Iniciamos sesión automáticamente con las mismas credenciales para
                // obtener accessToken/refreshToken (sin token, IsLoggedIn=false = invitado).
                string email    = RegistrationData.Email;
                string password = RegistrationData.Password;
                string username = RegistrationData.Username;

                ApiManager.Instance.Login(email, password,
                    onSuccess: resp =>
                    {
                        SessionData.SetTokens(resp.accessToken, resp.refreshToken, resp.tokenType, resp.expiresIn);
                        string uid = !string.IsNullOrEmpty(resp.userId) ? resp.userId
                                   : !string.IsNullOrEmpty(resp.userPublicId) ? resp.userPublicId : userId;
                        SessionData.SetSession(uid, username, email);
                        RegistrationData.Clear();
                        Debug.Log($"[CuentaCreada] Cuenta creada y sesión iniciada · userId='{uid}'");
                        ShowStatus("¡Tu cuenta está lista!", COLOR_OK);
                        SetBotonExplorar(true);
                    },
                    onError: (code, detail) =>
                    {
                        // La cuenta existe pero el auto-login falló (raro). Guardamos lo
                        // básico; el usuario podrá iniciar sesión manualmente.
                        SessionData.SetSession(userId, username, email);
                        RegistrationData.Clear();
                        Debug.LogWarning($"[CuentaCreada] Auto-login falló ({code}, {detail}).");
                        ShowStatus("¡Tu cuenta está lista!", COLOR_OK);
                        SetBotonExplorar(true);
                    });
            },
            onError: (code, detail) =>
            {
                ShowStatus(MapError(detail), COLOR_ERR);
                if (btnReintentar) btnReintentar.gameObject.SetActive(true);
            }
        );
    }

    string MapError(string detail)
    {
        switch (detail)
        {
            case "ERR_EMAIL_REQUIRED":
                return "Falta el correo electrónico.";
            case "ERR_INVALID_EMAIL":
                return "El correo no tiene un formato válido.";
            case "ERR_EMAIL_TAKEN":
                return "Este correo ya está registrado.";

            case "ERR_PASSWORD_REQUIRED":
                return "Falta la contraseña.";
            case "ERR_PASSWORD_TOO_SHORT":
                return "La contraseña es muy corta.";
            case "ERR_PASSWORD_TOO_LONG":
                return "La contraseña es muy larga.";
            case "ERR_PASSWORD_WEAK":
                return "La contraseña es muy débil.";

            case "ERR_USERNAME_REQUIRED":
                return "Falta el nombre de usuario.";

            default:
                return "No se pudo crear la cuenta. Intenta de nuevo.";
        }
    }

    void SetBotonExplorar(bool activo)
    {
        if (!btnExplorar) return;
        btnExplorar.interactable = activo;
    }

    void ShowStatus(string msg, Color color)
    {
        if (!txtStatus) return;
        txtStatus.text  = msg;
        txtStatus.color = color;
        txtStatus.gameObject.SetActive(true);
    }
}
