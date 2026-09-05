using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Garantiza que todos los campos de texto usen el EDITOR NATIVO de Android, que
/// en horizontal se abre a pantalla completa y por tanto nunca queda tapado por
/// el teclado.
///
/// Para que eso funcione hicieron falta dos cosas:
///   1. Punto de entrada de Android = 'Activity' en vez de 'GameActivity'
///      (Player Settings). Con GameActivity, Unity gestiona el texto por otra
///      ruta y el editor nativo no aparece nunca; el log lo decía literalmente:
///      "Hiding input field is not supported when using Game Activity".
///   2. 'Hide Mobile Input' desactivado en cada campo, que es lo que hace esto.
///
/// El punto 2 ya viene bien en las escenas y además es el valor por defecto de
/// TMP, así que esto es una red de seguridad: si alguien marca esa casilla en el
/// inspector de un campo nuevo, ese campo volvería a quedar tapado por el teclado
/// y sería un fallo difícil de relacionar con su causa.
/// </summary>
public class MobileInputSettings : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        var go = new GameObject("MobileInputSettings");
        go.AddComponent<MobileInputSettings>();
        DontDestroyOnLoad(go);
    }

    void Awake()    => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Se incluyen los inactivos: los campos de los modales arrancan apagados.
        var fields = FindObjectsByType<TMP_InputField>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var f in fields)
        {
            if (!f) continue;
            f.shouldHideMobileInput  = false;
            f.shouldHideSoftKeyboard = false;
        }
    }
}
