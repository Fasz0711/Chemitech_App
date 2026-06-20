using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Botón de "mantener presionado": invoca onHold cada frame mientras el puntero
/// está abajo sobre él. Útil para d-pad y flechas de zoom de la cámara.
/// </summary>
public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public System.Action onHold;
    bool held;

    public void OnPointerDown(PointerEventData e) => held = true;
    public void OnPointerUp(PointerEventData e)   => held = false;
    public void OnPointerExit(PointerEventData e) => held = false;
    void OnDisable() => held = false;

    void Update()
    {
        if (held) onHold?.Invoke();
    }
}
