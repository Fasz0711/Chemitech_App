using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD de la zona de juego: cronómetro por universo (acumulado), controles de
/// cámara/colocación, y el modal de pausa (reanudar, guardar, ajustes, tutorial,
/// salir). El cronómetro se detiene mientras el modal de pausa está abierto.
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
    [SerializeField] private HoldButton vertUp;
    [SerializeField] private HoldButton vertDown;

    [Header("Modal de pausa")]
    [SerializeField] private GameObject      pauseModal;
    [SerializeField] private TextMeshProUGUI pauseSubtitle; // "Universo X · 0:00:00"
    [SerializeField] private Button btnReanudar;
    [SerializeField] private Button btnGuardar;
    [SerializeField] private Button btnAjustes;
    [SerializeField] private Button btnTutorial;
    [SerializeField] private Button btnSalir;
    [SerializeField] private GameObject savedToast;         // "Guardado ✓"

    [Header("Modal confirmar salida")]
    [SerializeField] private GameObject exitModal;
    [SerializeField] private Button btnExitConfirm;
    [SerializeField] private Button btnExitCancel;

    [Header("Escenas")]
    [SerializeField] private string escenaSalir = "MisUniversosScene";

    float elapsed;
    bool  paused;

    void Start()
    {
        // Carga el tiempo acumulado y los átomos guardados del universo.
        if (PlayContext.Current != null) elapsed = PlayContext.Current.playSeconds;
        if (txtUniverse) txtUniverse.text = PlayContext.UniverseName;
        if (place != null && PlayContext.Current != null) place.ImportAtoms(PlayContext.Current.atoms);

        if (btnRecentrar && cam) btnRecentrar.onClick.AddListener(cam.Recenter);

        if (padUp)    padUp.onHold    = () => MovePlane(Vector2.up);
        if (padDown)  padDown.onHold  = () => MovePlane(Vector2.down);
        if (padLeft)  padLeft.onHold  = () => MovePlane(Vector2.left);
        if (padRight) padRight.onHold = () => MovePlane(Vector2.right);
        if (vertUp)   vertUp.onHold   = () => MoveVert(+1f);
        if (vertDown) vertDown.onHold = () => MoveVert(-1f);

        // Modal de pausa
        if (btnPause)       btnPause.onClick.AddListener(OpenPause);
        if (btnReanudar)    btnReanudar.onClick.AddListener(ClosePause);
        if (btnGuardar)     btnGuardar.onClick.AddListener(Guardar);
        if (btnAjustes)     btnAjustes.onClick.AddListener(() => Debug.Log("[ZonaJuego] Ajustes — pendiente."));
        if (btnTutorial)    btnTutorial.onClick.AddListener(() => Debug.Log("[ZonaJuego] Tutorial — pendiente."));
        if (btnSalir)       btnSalir.onClick.AddListener(OnSalir);
        if (btnExitConfirm) btnExitConfirm.onClick.AddListener(() => SceneManager.LoadScene(escenaSalir));
        if (btnExitCancel)  btnExitCancel.onClick.AddListener(CloseExitConfirm);

        if (pauseModal) pauseModal.SetActive(false);
        if (exitModal)  exitModal.SetActive(false);
        if (savedToast) savedToast.SetActive(false);
    }

    void Update()
    {
        if (!paused) elapsed += Time.deltaTime;
        if (txtTimer) txtTimer.text = FormatTime(elapsed);
    }

    // ── Pausa ─────────────────────────────────────────────────────────────────
    void OpenPause()
    {
        paused = true;
        if (savedToast) savedToast.SetActive(false);
        if (pauseSubtitle)
            pauseSubtitle.text = $"{PlayContext.UniverseName}   ·   <color=#3FE0D0>{FormatTime(elapsed)}</color>";
        if (pauseModal) pauseModal.SetActive(true);
    }

    void ClosePause()
    {
        if (pauseModal) pauseModal.SetActive(false);
        paused = false;
    }

    void Guardar()
    {
        if (PlayContext.Current == null || place == null) return;
        PlayContext.Current.atoms       = place.ExportAtoms();
        PlayContext.Current.playSeconds = (long)elapsed;
        UniverseStore.Update(PlayContext.Current);
        if (place) place.ClearDirty();
        Debug.Log($"[ZonaJuego] Guardado: {PlayContext.Current.atoms.Count} átomos · {PlayContext.Current.playSeconds}s");
        if (savedToast) StartCoroutine(ShowSavedToast());
    }

    IEnumerator ShowSavedToast()
    {
        savedToast.SetActive(true);
        yield return new WaitForSecondsRealtime(1.6f);
        if (savedToast) savedToast.SetActive(false);
    }

    void OnSalir()
    {
        // Solo pide confirmación si hay cambios sin guardar; si no, sale directo.
        if (place != null && place.Dirty) OpenExitConfirm();
        else SceneManager.LoadScene(escenaSalir);
    }

    void OpenExitConfirm()  { if (exitModal) exitModal.SetActive(true); }
    void CloseExitConfirm() { if (exitModal) exitModal.SetActive(false); }

    // ── Controles ─────────────────────────────────────────────────────────────
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
}
