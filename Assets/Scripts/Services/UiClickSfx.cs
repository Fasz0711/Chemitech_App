using UnityEngine;

/// <summary>
/// Marca un Button cuyo click ya fue cableado al sonido de interfaz, y guarda
/// qué familia de sonido le tocó.
///
/// AudioManager re-escanea la escena varias veces (al cargarla y tras el primer
/// frame, para alcanzar las tarjetas que se instancian en Start); esta marca
/// evita que un mismo botón termine con el listener repetido y suene doble.
///
/// El campo 'role' se puede fijar a mano en el inspector para corregir un botón
/// que el clasificador por nombre no acierte: con cualquier valor distinto de
/// Auto, manda lo que digas aquí.
/// </summary>
[DisallowMultipleComponent]
public class UiClickSfx : MonoBehaviour
{
    [Tooltip("Auto = lo decide UiSfxClassifier por el nombre del objeto.")]
    public UiSfxRole role = UiSfxRole.Auto;

    /// <summary>Rol efectivo: el fijado a mano, o el deducido del nombre.</summary>
    public UiSfxRole Resolve()
        => role != UiSfxRole.Auto ? role : UiSfxClassifier.Classify(gameObject.name);
}
