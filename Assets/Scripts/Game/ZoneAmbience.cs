using UnityEngine;

/// <summary>
/// Enciende la ambientación 3D en escenas que NO tienen ZonaJuegoManager.
///
/// La rejilla de la plataforma y el cielo estrellado no están guardados en ninguna
/// escena: los crea ZoneEnvironment en tiempo de ejecución, y en la zona de juego es el
/// manager quien lo instancia. Las escenas de clase no tienen ese manager, así que su
/// plataforma salía lisa y el fondo plano aunque la geometría estuviera puesta.
///
/// Se pone en el objeto PlayArea. No sustituye a nada: si algo ya creó la ambientación
/// —por ejemplo, si algún día la clase compartiera manager con la zona de juego— no
/// vuelve a crearla.
/// </summary>
public class ZoneAmbience : MonoBehaviour
{
    [Tooltip("Pulsos al formarse un enlace. Solo sirve donde hay detección: la pizarra " +
             "del docente. El alumno recibe la química ya resuelta y no detecta nada.")]
    [SerializeField] private bool effects = true;

    void Awake()
    {
        if (FindObjectOfType<ZoneEnvironment>() == null)
            new GameObject("ZoneEnvironment").AddComponent<ZoneEnvironment>();

        if (effects && FindObjectOfType<ZoneEffects>() == null)
            new GameObject("ZoneEffects").AddComponent<ZoneEffects>();
    }
}
