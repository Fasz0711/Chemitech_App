using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// La pizarra virtual: la misma escena que ve el alumno, más el panel de conducción.
///
/// A diferencia del alumno, el docente NO sondea el estado en bucle. Es el único que
/// puede cambiarlo y /commands/apply le devuelve el estado completo, así que pinta al
/// instante y guarda esa versión. Solo recarga al entrar y cuando un 409 le avisa de
/// que se quedó viejo (p. ej. si abrió la clase en dos sitios). Lo que sí sondea es el
/// roster, que cambia por su cuenta conforme entran los alumnos.
/// </summary>
public class ClaseDocenteManager : MonoBehaviour
{
    [Header("Cámara")]
    [SerializeField] private OrbitCameraController cam;

    [Header("Mundo")]
    [SerializeField] private Transform sceneRoot;
    [SerializeField] private Material  atomMaterial;
    [SerializeField] private Material  bondMaterial;

    [Header("HUD")]
    [SerializeField] private TextMeshProUGUI className;
    [SerializeField] private TextMeshProUGUI rosterLabel;
    [SerializeField] private TextMeshProUGUI statusLabel;
    [SerializeField] private GameObject      emptyHint;
    [SerializeField] private Button          btnSalir;

    [Header("Barra de acciones")]
    [SerializeField] private Button btnAgua;
    [SerializeField] private Button btnSal;
    [SerializeField] private Button btnCO2;
    [SerializeField] private Button btnLimpiar;
    [SerializeField] private Button btnFijarVista;
    [SerializeField] private TextMeshProUGUI lblFijarVista;
    [SerializeField] private Button btnModoAgregar;
    [SerializeField] private TextMeshProUGUI lblModoAgregar;

    [Header("Resaltado (botones generados del estado)")]
    [SerializeField] private RectTransform highlightBar;
    [SerializeField] private GameObject    highlightButtonTemplate;
    [SerializeField] private Button        btnQuitarResaltado;

    [Header("Sesión de clase")]
    [SerializeField] private Button          btnIniciar;
    [SerializeField] private Button          btnTerminar;
    [SerializeField] private GameObject      stopModal;
    [SerializeField] private Button          btnStopConfirm;
    [SerializeField] private Button          btnStopCancel;

    [Header("Aviso")]
    [SerializeField] private GameObject      noticeRoot;
    [SerializeField] private TextMeshProUGUI noticeText;

    [Header("Render")]
    [SerializeField] private float worldScale    = 6f;
    [SerializeField] private float atomSize      = 0.9f;
    [SerializeField] private float bondThickness = 0.09f;
    [SerializeField] private float bondSpacing   = 0.20f;

    [Header("Ajustes")]
    [SerializeField] private float rosterSeconds = 5f;

    [Header("Escenas")]
    [SerializeField] private string escenaClases = "MisClasesScene";

    // Comandos exactos del contrato. Se escriben A MANO: JsonUtility.ToJson emitiría
    // todos los campos con su valor por defecto y un "view" que solo enfoca viajaría
    // con locked=false.
    // replace=true cambia la escena entera; replace=false suma. Hace falta poder sumar:
    // la disolución de la lección es sal Y agua a la vez, no una u otra.
    const string SHOW_FMT =
        @"[{{""action"":""show"",""molecules"":[""{0}""],""replace"":{1}}}]";
    const string CMD_LIMPIAR  = @"[{""action"":""clear""}]";
    const string CMD_FIJAR    = @"[{""action"":""view"",""locked"":true}]";
    const string CMD_LIBERAR  = @"[{""action"":""view"",""locked"":false}]";
    const string CMD_SIN_RESALTADO = @"[{""action"":""highlight""}]";
    const string HIGHLIGHT_FMT =
        @"[{{""action"":""highlight"",""selector"":{{""by"":""element"",""value"":""{0}""}}}}]";

    const float NOTICE_SECONDS = 3.5f;

    ClassSceneRenderer sceneRenderer;
    readonly List<GameObject> highlightButtons = new List<GameObject>();

    int       currentVersion = -1;
    bool      busy;
    bool      addMode;      // los botones de molécula suman en vez de reemplazar
    bool      leaving;
    bool      cameraLocked;
    string    classStatus = "waiting";
    Coroutine noticeCo;

    void Start()
    {
        if (btnSalir)            btnSalir.onClick.AddListener(Leave);
        if (btnAgua)             btnAgua.onClick.AddListener(() => Show("agua"));
        if (btnSal)              btnSal.onClick.AddListener(() => Show("sal"));
        if (btnCO2)              btnCO2.onClick.AddListener(() => Show("co2"));
        if (btnModoAgregar)      btnModoAgregar.onClick.AddListener(ToggleAddMode);
        if (btnLimpiar)          btnLimpiar.onClick.AddListener(() => Apply(CMD_LIMPIAR));
        if (btnQuitarResaltado)  btnQuitarResaltado.onClick.AddListener(() => Apply(CMD_SIN_RESALTADO));
        if (btnFijarVista)       btnFijarVista.onClick.AddListener(ToggleView);
        if (btnIniciar)          btnIniciar.onClick.AddListener(StartClass);
        if (btnTerminar)         btnTerminar.onClick.AddListener(() => { if (stopModal) stopModal.SetActive(true); });
        if (btnStopCancel)       btnStopCancel.onClick.AddListener(() => { if (stopModal) stopModal.SetActive(false); });
        if (btnStopConfirm)      btnStopConfirm.onClick.AddListener(StopClass);

        if (highlightButtonTemplate) highlightButtonTemplate.SetActive(false);
        if (stopModal) stopModal.SetActive(false);
        HideNotice();

        if (className) className.text = ClassContext.HasClass ? ClassContext.ClassName : "Clase";
        RefreshAddModeLabel();

        if (!sceneRoot) sceneRoot = transform;
        sceneRenderer = new ClassSceneRenderer(sceneRoot, atomMaterial, bondMaterial,
                                               worldScale, atomSize, bondThickness, bondSpacing);

        if (!ClassContext.HasClass) { Leave(); return; }

        LoadState();
        StartCoroutine(RosterLoop());
    }

    void Update() => sceneRenderer?.UpdateVisuals();

    void OnDestroy() => sceneRenderer?.Dispose();

    // ── Estado ─────────────────────────────────────────────────────────────────

    /// <summary>Carga inicial, y recarga tras un 409: la escena cambió por fuera.</summary>
    void LoadState()
    {
        ApiManager.Instance.GetClassState(ClassContext.ClassId, -1,
            onSuccess: state => { if (state != null) Paint(state); },
            onError:   (code, detail) => ShowNotice(MapError(code, detail)));
    }

    void Apply(string actionsJson)
    {
        if (busy) return;
        if (classStatus == "ended") { ShowNotice("La clase terminó: ya no se pueden aplicar comandos."); return; }

        busy = true;

        ApiManager.Instance.ApplyCommand(ClassContext.ClassId, actionsJson, currentVersion,
            onSuccess: state =>
            {
                busy = false;
                if (state != null) Paint(state);
            },
            onError: (code, detail) =>
            {
                busy = false;

                // La escena cambió desde la última que vimos: recargar y que el docente
                // vuelva a apretar. No se aplicó nada, así que no hay nada que deshacer.
                if (detail == "ERR_STALE_SCENE")
                {
                    ShowNotice("La escena cambió. Se recargó: vuelve a intentarlo.");
                    LoadState();
                    return;
                }

                ShowNotice(MapError(code, detail));
            });
    }

    /// <summary>Pinta un estado completo, venga del sondeo inicial o de un apply. Guardar
    /// SU versión es lo que evita que dos botones seguidos choquen con 409.</summary>
    void Paint(ClassStateResponse state)
    {
        currentVersion = state.version;
        classStatus    = state.status;

        sceneRenderer.Render(state.molecules);
        sceneRenderer.ApplyHighlights(state.highlights);

        bool empty = state.molecules == null || state.molecules.Length == 0;
        if (emptyHint) emptyHint.SetActive(empty);

        if (state.camera != null)
        {
            cameraLocked = state.camera.locked;
            if (cam) cam.SetView(state.camera.yaw, state.camera.pitch, state.camera.distance);
        }

        RefreshControls(state);
        RebuildHighlightButtons(state);
    }

    void RefreshControls(ClassStateResponse state)
    {
        if (lblFijarVista) lblFijarVista.text = cameraLocked ? "Liberar vista" : "Fijar vista";

        bool running = classStatus == "running";
        bool ended   = classStatus == "ended";

        if (btnIniciar)  btnIniciar.gameObject.SetActive(!running && !ended);
        if (btnTerminar) btnTerminar.gameObject.SetActive(running);

        if (statusLabel)
            statusLabel.text = ended    ? "Clase terminada"
                             : running  ? "Clase en curso"
                                        : "Sin iniciar · los alumnos aún no ven nada";
    }

    /// <summary>Un botón de resaltado por elemento PRESENTE en la escena. Generarlos del
    /// estado en vez de fijarlos evita ofrecer "Resaltar O" sin oxígenos, que daría
    /// ERR_SCENE_REFERENCE.</summary>
    void RebuildHighlightButtons(ClassStateResponse state)
    {
        foreach (var go in highlightButtons) if (go) Destroy(go);
        highlightButtons.Clear();

        if (!highlightButtonTemplate || !highlightBar) return;

        var elements = new List<string>();
        if (state.molecules != null)
            foreach (var m in state.molecules)
            {
                if (m?.atoms == null) continue;
                foreach (var a in m.atoms)
                    if (a != null && !string.IsNullOrEmpty(a.type) && !elements.Contains(a.type))
                        elements.Add(a.type);
            }

        foreach (var element in elements)
        {
            var go = Instantiate(highlightButtonTemplate, highlightBar);
            go.name = "BtnHighlight_" + element;
            go.SetActive(true);
            highlightButtons.Add(go);

            var label = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label) label.text = element;

            string captured = element;
            var btn = go.GetComponent<Button>();
            if (btn) btn.onClick.AddListener(() => Apply(string.Format(HIGHLIGHT_FMT, captured)));
        }

        if (btnQuitarResaltado) btnQuitarResaltado.gameObject.SetActive(elements.Count > 0);
    }

    void ToggleView() => Apply(cameraLocked ? CMD_LIBERAR : CMD_FIJAR);

    void Show(string alias)
        => Apply(string.Format(SHOW_FMT, alias, addMode ? "false" : "true"));

    /// <summary>Con el modo agregar encendido, los botones de molécula SUMAN a la escena
    /// en vez de reemplazarla. Es lo que permite montar "sal disuelta en agua".</summary>
    void ToggleAddMode()
    {
        addMode = !addMode;
        RefreshAddModeLabel();
    }

    void RefreshAddModeLabel()
    {
        if (lblModoAgregar) lblModoAgregar.text = addMode ? "Modo: agregar" : "Modo: reemplazar";
    }

    // ── Sesión de clase ────────────────────────────────────────────────────────
    void StartClass()
    {
        if (busy) return;
        busy = true;

        ApiManager.Instance.StartClass(ClassContext.ClassId,
            onSuccess: _ => { busy = false; LoadState(); },
            onError:   (code, detail) => { busy = false; ShowNotice(MapError(code, detail)); });
    }

    void StopClass()
    {
        if (busy) return;
        busy = true;
        if (stopModal) stopModal.SetActive(false);

        ApiManager.Instance.StopClass(ClassContext.ClassId,
            onSuccess: _ => { busy = false; LoadState(); },
            onError:   (code, detail) => { busy = false; ShowNotice(MapError(code, detail)); });
    }

    // ── Roster ─────────────────────────────────────────────────────────────────
    IEnumerator RosterLoop()
    {
        while (!leaving)
        {
            ApiManager.Instance.GetClassRoster(ClassContext.ClassId,
                onSuccess: r =>
                {
                    if (rosterLabel && r != null)
                        rosterLabel.text = $"{r.connected} de {r.total} conectados";
                },
                onError: (code, detail) => { /* el roster es informativo: no interrumpe la clase */ });

            yield return new WaitForSeconds(rosterSeconds);
        }
    }

    // ── Varios ─────────────────────────────────────────────────────────────────
    void Leave()
    {
        leaving = true;
        ClassContext.Clear();
        SceneManager.LoadScene(escenaClases);
    }

    void ShowNotice(string msg)
    {
        if (!noticeRoot || !noticeText) { Debug.LogWarning("[Docente] " + msg); return; }
        noticeText.text = msg;
        noticeRoot.SetActive(true);
        if (noticeCo != null) StopCoroutine(noticeCo);
        noticeCo = StartCoroutine(HideNoticeAfter());
    }

    IEnumerator HideNoticeAfter()
    {
        yield return new WaitForSeconds(NOTICE_SECONDS);
        noticeCo = null;
        HideNotice();
    }

    void HideNotice()
    {
        if (noticeRoot) noticeRoot.SetActive(false);
    }

    static string MapError(int code, string detail)
    {
        switch (detail)
        {
            case "ERR_STALE_SCENE":        return "La escena cambió. Vuelve a intentarlo.";
            case "ERR_SCENE_FULL":         return "La pizarra está llena (4 moléculas). Limpia antes de añadir.";
            case "ERR_UNKNOWN_MOLECULE":   return "No reconocí esa molécula.";
            case "ERR_MOLECULE_TOO_BIG":   return "Esa molécula es demasiado grande para la pizarra.";
            case "ERR_SCENE_REFERENCE":    return "Eso no está en la escena.";
            case "ERR_INVALID_ACTION":     return "No se pudo interpretar la acción.";
            case "ERR_CLASS_ALREADY_ENDED":return "La clase ya terminó.";
            case "ERR_CLASS_NOT_RUNNING":  return "La clase todavía no ha empezado.";
            case "ERR_NOT_CLASS_OWNER":    return "Esta clase es de otro docente.";
            case "ERR_NOT_TEACHER":        return "Tu cuenta no es de docente.";
            case "ERR_RATE_LIMITED":       return "Vas muy rápido. Espera un momento.";
        }
        if (code == 0 || code >= 500) return "Sin conexión con el servidor.";
        return "No se pudo aplicar el comando.";
    }
}
