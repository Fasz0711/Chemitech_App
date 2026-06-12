using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MisUniversosManager : MonoBehaviour
{
    [SerializeField] private Button btnAtras;
    [SerializeField] private Button btnCrear;

    [Header("Escenas")]
    [SerializeField] private string escenaAtras = "SampleScene";

    private void Start()
    {
        if (btnAtras) btnAtras.onClick.AddListener(() => SceneManager.LoadScene(escenaAtras));
        if (btnCrear) btnCrear.onClick.AddListener(() => Debug.Log("[MisUniversos] Crear universo — pendiente."));
    }
}
