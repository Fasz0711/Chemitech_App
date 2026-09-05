using System.Collections;
using System.Collections.Generic;
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

    [Header("Modal descubrimiento")]
    [SerializeField] private GameObject      discoveryModal;
    [SerializeField] private TextMeshProUGUI discoveryFormula;
    [SerializeField] private TextMeshProUGUI discoveryName;
    [SerializeField] private Button          btnVerDiario;
    [SerializeField] private Button          btnContinuar;

    [Header("Tutorial")]
    [SerializeField] private TutorialManager tutorial;

    [Header("Escenas")]
    [SerializeField] private string escenaSalir   = "MisUniversosScene";
    [SerializeField] private string escenaDiario  = "DiaryScene";
    [SerializeField] private string escenaAjustes = "SettingsScene";

    float elapsed;
    bool  paused;
    long  lastReportedElapsed; // segundos ya enviados al backend (evita doble conteo)
    long  lastSavedElapsed;    // 'elapsed' en el último Guardar (progreso comprometido)

    // Escena a la que se irá si el jugador confirma salir sin guardar.
    string pendingLeaveScene;

    // Margen antes de avisar por tiempo no guardado. Sin él, cualquier salida
    // tras un par de segundos de juego dispararía el modal y sería molesto.
    const long UNSAVED_TIME_THRESHOLD = 60;
    BondManager bondManager;

    void Start()
    {
        bondManager = FindObjectOfType<BondManager>();

        // Ambientación y efectos 3D. Se crean por código para no tener que tocar
        // ZonaJuegoScene, que está ajustada a mano.
        new GameObject("ZoneEnvironment").AddComponent<ZoneEnvironment>();
        new GameObject("ZoneEffects").AddComponent<ZoneEffects>();

        // Carga el tiempo acumulado y los átomos guardados del universo.
        if (PlayContext.Current != null) elapsed = PlayContext.Current.playSeconds;
        lastReportedElapsed = (long)elapsed; // el tiempo cargado ya fue reportado en sesiones previas
        lastSavedElapsed    = (long)elapsed; // y también está ya guardado en el universo
        if (txtUniverse) txtUniverse.text = PlayContext.UniverseName;

        if (place != null && PlayContext.Current != null)
        {
            place.ImportAtoms(PlayContext.Current.atoms);
            RestoreBonds();   // dibuja los enlaces guardados sin re-detectar (no re-descubre)
        }

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
        if (btnAjustes)     btnAjustes.onClick.AddListener(OnAjustes);
        if (btnTutorial)    btnTutorial.onClick.AddListener(OnTutorial);
        if (btnSalir)       btnSalir.onClick.AddListener(OnSalir);
        if (btnExitConfirm) btnExitConfirm.onClick.AddListener(ConfirmLeave);
        if (btnExitCancel)  btnExitCancel.onClick.AddListener(CloseExitConfirm);

        // Modal de descubrimiento (nueva molécula)
        if (bondManager != null) bondManager.OnNewDiscovery += ShowDiscovery;
        if (btnContinuar) btnContinuar.onClick.AddListener(CloseDiscovery);
        if (btnVerDiario) btnVerDiario.onClick.AddListener(VerDiario);

        if (pauseModal)     pauseModal.SetActive(false);
        if (exitModal)      exitModal.SetActive(false);
        if (discoveryModal) discoveryModal.SetActive(false);
        if (savedToast)     savedToast.SetActive(false);
    }

    void OnDestroy()
    {
        if (bondManager != null) bondManager.OnNewDiscovery -= ShowDiscovery;
    }

    // ── Modal de descubrimiento ────────────────────────────────────────────────
    void ShowDiscovery(string formula, string name)
    {
        paused = true;   // congela el cronómetro mientras se celebra
        if (discoveryFormula) discoveryFormula.text = FormatFormula(formula);
        if (discoveryName)    discoveryName.text    = string.IsNullOrEmpty(name) ? "" : name;
        if (discoveryModal)   discoveryModal.SetActive(true);
    }

    void CloseDiscovery()
    {
        if (discoveryModal) discoveryModal.SetActive(false);
        paused = false;
    }

    void VerDiario()
    {
        Guardar();   // no perder progreso al salir al diario
        SceneManager.LoadScene(escenaDiario);
    }

    static string FormatFormula(string f)
    {
        if (string.IsNullOrEmpty(f)) return "";
        return System.Text.RegularExpressions.Regex.Replace(f, "([0-9]+)", "<sub>$1</sub>");
    }

    void Update()
    {
        bool tutorialOpen = tutorial != null && tutorial.IsOpen;
        if (!paused && !tutorialOpen) elapsed += Time.deltaTime;
        if (txtTimer) txtTimer.text = FormatTime(elapsed);
    }

    // Reabre el tutorial desde el menú de pausa ("Ver tutorial").
    void OnTutorial()
    {
        ClosePause();
        if (tutorial) tutorial.Replay();
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
        PlayContext.Current.bonds       = ExportBondSaves();  // enlaces detectados → índices
        PlayContext.Current.playSeconds = (long)elapsed;
        UniverseStore.Update(PlayContext.Current);
        place.ClearDirty();
        lastSavedElapsed = (long)elapsed;   // el tiempo queda comprometido junto con los átomos
        Debug.Log($"[ZonaJuego] Guardado: {PlayContext.Current.atoms.Count} átomos · {PlayContext.Current.bonds.Count} enlaces · {PlayContext.Current.playSeconds}s");
        ReportTimeDelta();
        if (savedToast) StartCoroutine(ShowSavedToast());
    }

    // Enlaces actuales (del BondManager) mapeados a índices de la lista de átomos.
    List<BondSave> ExportBondSaves()
    {
        var result = new List<BondSave>();
        if (bondManager == null) return result;

        var ordered = place.GetOrderedAtoms();
        var idToIndex = new Dictionary<int, int>();
        for (int i = 0; i < ordered.Count; i++) idToIndex[ordered[i].id] = i;

        foreach (var (a, b, order) in bondManager.ExportBonds())
            if (idToIndex.TryGetValue(a, out int ia) && idToIndex.TryGetValue(b, out int ib))
                result.Add(new BondSave { a = ia, b = ib, order = order });
        return result;
    }

    // Restaura los enlaces guardados (índices → ids de los átomos recién creados)
    // y los dibuja sin llamar al backend (no re-descubre la molécula).
    void RestoreBonds()
    {
        if (bondManager == null) return;
        var saved = PlayContext.Current?.bonds;
        // Sin enlaces guardados (save viejo o sin molécula): dejar que detecte normal.
        if (saved == null || saved.Count == 0) return;

        var ordered = place.GetOrderedAtoms();
        var bonds = new List<(int a, int b, int order)>();
        foreach (var bs in saved)
            if (bs.a >= 0 && bs.a < ordered.Count && bs.b >= 0 && bs.b < ordered.Count)
                bonds.Add((ordered[bs.a].id, ordered[bs.b].id, bs.order));

        bondManager.ImportBonds(bonds);
    }

    // Suma al backend el tiempo jugado desde el último reporte (cuenta con sesión).
    void ReportTimeDelta()
    {
        if (string.IsNullOrEmpty(SessionData.UserId)) return; // invitado: no se contabiliza
        long delta = (long)elapsed - lastReportedElapsed;
        if (delta <= 0) return;
        long snapshot = (long)elapsed;
        ApiManager.Instance.AddTimePlayed(SessionData.UserId, (int)delta,
            onSuccess: stats =>
            {
                lastReportedElapsed = snapshot;
                Debug.Log($"[TimePlayed] +{delta}s enviados · total cuenta={stats.totalPlayTimeSeconds}s");
            },
            onError: (code, detail) =>
                Debug.LogWarning($"[TimePlayed] No se pudo enviar +{delta}s · code={code} · {detail}"));
    }

    IEnumerator ShowSavedToast()
    {
        savedToast.SetActive(true);
        yield return new WaitForSecondsRealtime(1.6f);
        if (savedToast) savedToast.SetActive(false);
    }

    void OnSalir()   => RequestLeave(escenaSalir,   "¿Salir sin guardar?",         "Salir");
    void OnAjustes() => RequestLeave(escenaAjustes, "¿Ir a ajustes sin guardar?",  "Ir a ajustes");

    /// <summary>
    /// Cualquier salida de la zona de juego pasa por aquí. Ajustes descarga la
    /// escena igual que Salir, así que descarta el mismo progreso y merece el
    /// mismo aviso: sin él, tocar "Ajustes" se llevaba los átomos y el tiempo
    /// sin decir nada.
    /// </summary>
    void RequestLeave(string scene, string title, string confirmLabel)
    {
        pendingLeaveScene = scene;

        if (!HasUnsavedProgress()) { SceneManager.LoadScene(scene); return; }

        ConfigureExitModal(title, confirmLabel);
        OpenExitConfirm();
    }

    /// <summary>
    /// Hay progreso sin comprometer. Además de los átomos, cuenta el TIEMPO:
    /// solo se registra al guardar, así que salir tras un rato de juego lo
    /// pierde aunque no se haya tocado ningún átomo.
    /// </summary>
    bool HasUnsavedProgress()
    {
        if (place != null && place.Dirty) return true;
        return (long)elapsed - lastSavedElapsed >= UNSAVED_TIME_THRESHOLD;
    }

    void ConfirmLeave()
    {
        SceneManager.LoadScene(string.IsNullOrEmpty(pendingLeaveScene) ? escenaSalir : pendingLeaveScene);
    }

    // El modal es el mismo para las dos salidas, así que se le adaptan el título
    // y la etiqueta del botón rojo: "Salir" no tiene sentido si vas a Ajustes.
    // Se buscan en el momento porque no hay campos cableados para ellos.
    void ConfigureExitModal(string title, string confirmLabel)
    {
        if (exitModal)
        {
            var t = exitModal.transform.Find("Panel/Title");
            if (t && t.TryGetComponent<TextMeshProUGUI>(out var tmp)) tmp.text = title;
        }

        if (btnExitConfirm)
        {
            // Por nombre del hijo no: se busca el texto que haya dentro del botón.
            var lbl = btnExitConfirm.GetComponentInChildren<TextMeshProUGUI>(true);
            if (lbl) lbl.text = confirmLabel;
        }
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
