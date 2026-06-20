using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GuestPromptManager : MonoBehaviour
{
    [Header("Botones")]
    [SerializeField] private Button btnIniciarSesion;
    [SerializeField] private Button btnContinuarSinCuenta;

    [Header("Escenas")]
    [SerializeField] private string escenaLogin     = "LoginScene";
    [SerializeField] private string escenaInvitado  = "MisUniversosScene";

    private void Start()
    {
        if (btnIniciarSesion)      btnIniciarSesion.onClick.AddListener(OnIniciarSesion);
        if (btnContinuarSinCuenta) btnContinuarSinCuenta.onClick.AddListener(OnContinuarSinCuenta);
    }

    private void OnIniciarSesion() => SceneManager.LoadScene(escenaLogin);

    private void OnContinuarSinCuenta()
    {
        // Sesión de invitado: sin token. Limpia cualquier sesión previa para que
        // UniverseStore opere en modo invitado (universos solo en memoria).
        SessionData.Clear();
        SceneManager.LoadScene(escenaInvitado);
    }
}
