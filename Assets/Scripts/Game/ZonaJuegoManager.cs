using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD de la zona de juego (esqueleto): cronómetro de tiempo jugado, nombre del
/// universo, y cableado de los controles de cámara (rotar/pan/zoom/recentrar).
/// Pausa, Mover, hotbar y Selector de átomos quedan como stubs por ahora.
/// </summary>
public class ZonaJuegoManager : MonoBehaviour
{
    [Header("Cámara / colocación")]
    [SerializeField] private OrbitCameraController  cam;
    [SerializeField] private AtomPlacementController place;

    [Header("Barra superior")]
    [SerializeField] private TextMeshProUGUI txtUniverse;
    [SerializeField] private TextMeshProUGUI txtTimer;
    [SerializeField] private Button btnPause;
    [SerializeField] private Button btnRecentrar;

    [Header("Controles de cámara")]
    [SerializeField] private HoldButton padUp;
    [SerializeField] private HoldButton padDown;
    [SerializeField] private HoldButton padLeft;
    [SerializeField] private HoldButton padRight;
    [SerializeField] private HoldButton vertUp;   // sube la cámara
    [SerializeField] private HoldButton vertDown; // baja la cámara

    // Hotbar y selector de átomos los maneja AtomSelectorController.

    [Header("Escenas")]
    [SerializeField] private string escenaSalir = "MisUniversosScene";

    float elapsed;

    void Start()
    {
        if (txtUniverse) txtUniverse.text = PlayContext.UniverseName;

        if (btnPause)     btnPause.onClick.AddListener(OnPause);
        if (btnRecentrar && cam) btnRecentrar.onClick.AddListener(cam.Recenter);

        // d-pad y flechas: si hay un átomo seleccionado lo mueven; si no, mueven la cámara.
        if (padUp)    padUp.onHold    = () => MovePlane(Vector2.up);
        if (padDown)  padDown.onHold  = () => MovePlane(Vector2.down);
        if (padLeft)  padLeft.onHold  = () => MovePlane(Vector2.left);
        if (padRight) padRight.onHold = () => MovePlane(Vector2.right);
        if (vertUp)   vertUp.onHold   = () => MoveVert(+1f);
        if (vertDown) vertDown.onHold = () => MoveVert(-1f);
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        if (txtTimer) txtTimer.text = FormatTime(elapsed);
    }

    void MovePlane(Vector2 dir)
    {
        if (place) place.MoveOrPan(dir);
        else if (cam) cam.PanScreen(dir);
    }

    void MoveVert(float sign)
    {
        if (place) place.VerticalOrCam(sign);
        else if (cam) cam.MoveVertical(sign);
    }

    static string FormatTime(float seconds)
    {
        int t = Mathf.FloorToInt(seconds);
        int h = t / 3600;
        int m = (t / 60) % 60;
        int s = t % 60;
        return $"{h}:{m:00}:{s:00}";
    }

    // ── Stubs (se definirán en fases siguientes) ──────────────────────────────
    void OnPause()
    {
        Debug.Log("[ZonaJuego] Pausa — saliendo a MisUniversos (temporal).");
        SceneManager.LoadScene(escenaSalir);
    }
}
