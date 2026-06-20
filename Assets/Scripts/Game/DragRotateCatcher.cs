using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Capa transparente a pantalla completa, detrás del HUD, que captura los
/// arrastres sobre el espacio vacío y los traduce a rotación de la cámara.
/// Los botones del HUD, al estar delante, consumen sus propios eventos.
/// </summary>
public class DragRotateCatcher : MonoBehaviour, IDragHandler
{
    public OrbitCameraController cam;

    public void OnDrag(PointerEventData e)
    {
        if (cam) cam.Rotate(e.delta);
    }
}
