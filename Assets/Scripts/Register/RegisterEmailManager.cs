using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class RegisterEmailManager : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private TMP_InputField inputEmail;

    [Header("Botones")]
    [SerializeField] private Button btnAtras;
    [SerializeField] private Button btnSiguiente;

    [Header("Escenas")]
    [SerializeField] private string escenaAtras     = "LoginScene";
    [SerializeField] private string escenaSiguiente = "RegisterPasswordScene";

    private void Start()
    {
        if (btnAtras)     btnAtras.onClick.AddListener(OnAtras);
        if (btnSiguiente) btnSiguiente.onClick.AddListener(OnSiguiente);
    }

    private void OnAtras() => SceneManager.LoadScene(escenaAtras);

    private void OnSiguiente()
    {
        string email = inputEmail ? inputEmail.text.Trim() : "";
        if (string.IsNullOrEmpty(email))
        {
            Debug.LogWarning("[RegisterEmail] El campo de correo está vacío.");
            return;
        }
        SceneManager.LoadScene(escenaSiguiente);
    }
}
