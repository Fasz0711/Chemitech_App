using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Capa que distingue un TOQUE de un ARRASTRE sobre la escena de clase. Convive con
/// DragRotateCatcher en el mismo objeto: arrastrar rota la cámara, tocar aísla una
/// molécula.
///
/// Usa eventos de UI en vez de leer el input directamente, así funciona igual con el
/// sistema de entrada viejo y con el nuevo.
/// </summary>
public class ClassTapCatcher : MonoBehaviour, IPointerClickHandler
{
    /// <summary>Posición en pantalla del toque.</summary>
    public event Action<Vector2> Tapped;

    // Un dedo nunca se queda totalmente quieto; por debajo de esto sigue siendo un toque.
    const float TAP_SLOP_PIXELS = 12f;

    public void OnPointerClick(PointerEventData e)
    {
        // Rotar la cámara es arrastrar. Si el dedo recorrió distancia, el usuario quería
        // girar la vista, no seleccionar nada.
        if ((e.position - e.pressPosition).sqrMagnitude > TAP_SLOP_PIXELS * TAP_SLOP_PIXELS)
            return;

        Tapped?.Invoke(e.position);
    }
}
