using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Olvidé mi contraseña — pantalla final: "¡Contraseña cambiada exitosamente!".
/// Al cambiar la contraseña el backend cerró TODAS las sesiones del usuario, así
/// que se limpia la sesión local y "Ir a iniciar sesión" vuelve al login.
/// </summary>
public class ForgotPasswordSuccessManager : MonoBehaviour
{
    [Header("Botón")]
    [SerializeField] private Button btnLogin;

    [Header("Escenas")]
    [SerializeField] private string escenaLogin = "LoginScene";

    void Start()
    {
        // El reset revoca las sesiones activas: forzar re-login limpio.
        PasswordResetData.Clear();
        SessionData.Clear();

        if (btnLogin) btnLogin.onClick.AddListener(() => SceneManager.LoadScene(escenaLogin));
    }
}
