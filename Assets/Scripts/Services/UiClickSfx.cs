using UnityEngine;

/// <summary>
/// Marca un Button cuyo click ya fue cableado al sonido de interfaz.
/// AudioManager re-escanea la escena varias veces (al cargarla y tras el primer
/// frame, para alcanzar las tarjetas que se instancian en Start); esta marca
/// evita que un mismo botón termine con el listener repetido y suene doble.
/// </summary>
[DisallowMultipleComponent]
public class UiClickSfx : MonoBehaviour
{
}
