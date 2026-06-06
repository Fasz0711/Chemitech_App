using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GuestPromptManager : MonoBehaviour
{
    [Header("Botones")]
    [SerializeField] private Button btnIniciarSesion;
    [SerializeField] private Button btnContinuarSinCuenta;

    [Header("Escenas")]
    [SerializeField] private string escenaLogin = "LoginScene";

    private void Start()
    {
        if (btnIniciarSesion)      btnIniciarSesion.onClick.AddListener(OnIniciarSesion);
        if (btnContinuarSinCuenta) btnContinuarSinCuenta.onClick.AddListener(OnContinuarSinCuenta);
    }

    private void OnIniciarSesion() => SceneManager.LoadScene(escenaLogin);

    private void OnContinuarSinCuenta()
    {
        // TODO: cargar escena de juego
        Debug.Log("[GuestPrompt] Continuar sin cuenta — pendiente.");
    }
}
