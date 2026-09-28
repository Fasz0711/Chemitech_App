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

    /// <summary>Cuánto puede recorrer el dedo sin dejar de ser un toque.
    ///
    /// ERA UN 12 FIJO, Y POR ESO ESTO FUNCIONABA EN PC Y NO EN EL CELULAR. Con ratón el
    /// puntero recorre 0 o 1 píxeles entre pulsar y soltar, así que 12 sobraba; un dedo
    /// sobre una pantalla de 400 ppp recorre 12 píxeles en menos de un milímetro, o sea
    /// SIEMPRE. Cada toque del alumno se clasificaba como arrastre y se descartaba.
    ///
    /// La fórmula es la misma que ya usa AtomPlacementController.DragThreshold() para lo
    /// mismo en la pizarra del docente y en la zona de juego, donde este fallo ya se
    /// había corregido. Que las dos rutas usen el mismo número es el punto: el alumno y
    /// el docente tocan la misma escena y no pueden necesitar pulsos distintos.</summary>
    static float TapSlopPixels()
    {
        float dpiBased  = (Screen.dpi > 1f) ? Screen.dpi * 0.12f : 0f;
        float sizeBased = Screen.height * 0.022f;
        return Mathf.Max(12f, dpiBased, sizeBased);   // 12 mantiene el comportamiento en PC
    }

    public void OnPointerClick(PointerEventData e)
    {
        // Rotar la cámara es arrastrar. Si el dedo recorrió distancia, el usuario quería
        // girar la vista, no seleccionar nada.
        float slop = TapSlopPixels();
        if ((e.position - e.pressPosition).sqrMagnitude > slop * slop)
            return;

        Tapped?.Invoke(e.position);
    }
}
