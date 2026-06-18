using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CuentaCreadaManager : MonoBehaviour
{
    [SerializeField] private Button btnExplorar;

    [Header("Escenas")]
    [SerializeField] private string escenaSiguiente = "MisUniversosScene";

    private void Start()
    {
        if (btnExplorar) btnExplorar.onClick.AddListener(() => SceneManager.LoadScene(escenaSiguiente));
    }
}
