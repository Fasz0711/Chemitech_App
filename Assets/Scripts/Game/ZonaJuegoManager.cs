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
    [Header("Cámara")]
    [SerializeField] private OrbitCameraController cam;
    [SerializeField] private DragRotateCatcher     rotateCatcher;

    [Header("Barra superior")]
    [SerializeField] private TextMeshProUGUI txtUniverse;
    [SerializeField] private TextMeshProUGUI txtTimer;
    [SerializeField] private Button btnPause;
    [SerializeField] private Button btnMover;
    [SerializeField] private Button btnRecentrar;

    [Header("Mover (modo)")]
    [SerializeField] private Image moverIndicator;

    [Header("Controles de cámara")]
    [SerializeField] private HoldButton padUp;
    [SerializeField] private HoldButton padDown;
    [SerializeField] private HoldButton padLeft;
    [SerializeField] private HoldButton padRight;
    [SerializeField] private HoldButton vertUp;   // sube la cámara
    [SerializeField] private HoldButton vertDown; // baja la cámara

    [Header("Hotbar (6) y selector")]
    [SerializeField] private Button[] slots;
    [SerializeField] private Button   btnSelector;

    [Header("Escenas")]
    [SerializeField] private string escenaSalir = "MisUniversosScene";

    float elapsed;
    bool  moverMode = true;

    void Start()
    {
        if (txtUniverse) txtUniverse.text = PlayContext.UniverseName;

        if (rotateCatcher && cam) rotateCatcher.cam = cam;

        if (btnPause)     btnPause.onClick.AddListener(OnPause);
        if (btnMover)     btnMover.onClick.AddListener(OnToggleMover);
        if (btnRecentrar && cam) btnRecentrar.onClick.AddListener(cam.Recenter);
        if (btnSelector)  btnSelector.onClick.AddListener(OnSelector);

        if (cam)
        {
            if (padUp)    padUp.onHold    = () => cam.PanScreen(Vector2.up);
            if (padDown)  padDown.onHold  = () => cam.PanScreen(Vector2.down);
            if (padLeft)  padLeft.onHold  = () => cam.PanScreen(Vector2.left);
            if (padRight) padRight.onHold = () => cam.PanScreen(Vector2.right);
            if (vertUp)   vertUp.onHold   = () => cam.MoveVertical(+1f);
            if (vertDown) vertDown.onHold = () => cam.MoveVertical(-1f);
        }

        if (slots != null)
            for (int i = 0; i < slots.Length; i++)
            {
                int idx = i;
                if (slots[i]) slots[i].onClick.AddListener(() => OnSlot(idx));
            }

        UpdateMoverVisual();
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        if (txtTimer) txtTimer.text = FormatTime(elapsed);
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

    void OnToggleMover()
    {
        moverMode = !moverMode;
        UpdateMoverVisual();
        Debug.Log($"[ZonaJuego] Modo Mover = {moverMode}");
    }

    void UpdateMoverVisual()
    {
        if (moverIndicator)
            moverIndicator.color = moverMode ? Color.white : new Color(1f, 1f, 1f, 0.35f);
    }

    void OnSelector() => Debug.Log("[ZonaJuego] Abrir Selector de átomos — pendiente.");

    void OnSlot(int idx) => Debug.Log($"[ZonaJuego] Slot {idx} presionado — pendiente.");
}
