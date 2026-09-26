using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Conecta el d-pad, las flechas verticales y "Recentrar" con la colocación de átomos.
///
/// Existe porque HoldButton.onHold es un delegado normal, no un evento serializado: hay
/// que asignarlo desde código. En la zona de juego lo hace ZonaJuegoManager, pero ese
/// manager arrastra cronómetro, guardado y menú de pausa, que en la pizarra del docente
/// no aplican. Esto es solo el cableado, sin nada más.
/// </summary>
public class PlacementHudBinder : MonoBehaviour
{
    [SerializeField] private AtomPlacementController placement;
    [SerializeField] private OrbitCameraController   cam;
    [SerializeField] private Button     btnRecentrar;

    [Header("D-pad (desplaza sobre el plano)")]
    [SerializeField] private HoldButton padUp;
    [SerializeField] private HoldButton padDown;
    [SerializeField] private HoldButton padLeft;
    [SerializeField] private HoldButton padRight;

    [Header("Flechas verticales (suben y bajan)")]
    [SerializeField] private HoldButton vertUp;
    [SerializeField] private HoldButton vertDown;

    void Start()
    {
        if (btnRecentrar && cam) btnRecentrar.onClick.AddListener(cam.Recenter);

        // Las flechas mueven el átomo activo si hay uno, y si no la cámara. Esa decisión
        // vive en AtomPlacementController, que es quien sabe si hay algo seleccionado.
        if (padUp)    padUp.onHold    = () => Move(Vector2.up);
        if (padDown)  padDown.onHold  = () => Move(Vector2.down);
        if (padLeft)  padLeft.onHold  = () => Move(Vector2.left);
        if (padRight) padRight.onHold = () => Move(Vector2.right);
        if (vertUp)   vertUp.onHold   = () => Vertical(+1f);
        if (vertDown) vertDown.onHold = () => Vertical(-1f);
    }

    void Move(Vector2 dir)
    {
        if (placement) placement.MoveOrPan(dir);
        else if (cam)  cam.PanScreen(dir);
    }

    void Vertical(float sign)
    {
        if (placement) placement.VerticalOrCam(sign);
        else if (cam)  cam.MoveVertical(sign);
    }
}
