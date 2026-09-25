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
    [SerializeField] private Button        btnModoSumar;
    [SerializeField] private TextMeshProUGUI lblModoSumar;

    [Header("Instrucción en lenguaje natural")]
    [SerializeField] private TMP_InputField  inputPrompt;
    [SerializeField] private Button          btnEnviarPrompt;

    [Header("Pensando")]
    [SerializeField] private GameObject thinkingIndicator;

    [Header("Vista previa")]
    [SerializeField] private GameObject      previewPanel;
    [SerializeField] private TextMeshProUGUI previewText;
    [SerializeField] private Button          btnPreviewConfirm;
    [SerializeField] private Button          btnPreviewDiscard;

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
    // append=true SUMA al resaltado en vez de reemplazarlo. El guion lo necesita: cuando
    // el sodio entrega su electrón al cloro hay que ver los dos marcados a la vez, y con
    // un solo selector no se puede porque son elementos distintos.
    const string HIGHLIGHT_FMT =
        @"[{{""action"":""highlight"",""selector"":{{""by"":""element"",""value"":""{0}""}},""append"":{1}}}]";

    const float NOTICE_SECONDS = 3.5f;
    static readonly string NEWLINE = System.Environment.NewLine;

    ClassSceneRenderer sceneRenderer;
    readonly List<GameObject> highlightButtons = new List<GameObject>();

    int       currentVersion = -1;
    bool      busy;
    bool      addMode;      // los botones de molécula suman en vez de reemplazar

    // Lo que devolvió el intérprete y está esperando confirmación. El actionsJson se
    // guarda SIN parsear: se reenvía tal cual, que es lo único que garantiza que se
    // aplique exactamente lo que el docente vio en la vista previa.
    bool      sumMode;        // los botones de resaltar suman en vez de reemplazar
    Coroutine thinkingCo;

    // El indicador no aparece de inmediato: las frases del guion las resuelven las reglas
    // en milisegundos y un indicador que parpadea se lee como un defecto.
    const float THINKING_DELAY = 0.25f;

    string pendingActions = "";
    string pendingPrompt  = "";
    int    pendingBaseVersion;
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
        if (btnEnviarPrompt)     btnEnviarPrompt.onClick.AddListener(SendPrompt);
        if (btnModoSumar)        btnModoSumar.onClick.AddListener(ToggleSumMode);
        if (btnPreviewConfirm)   btnPreviewConfirm.onClick.AddListener(ConfirmPreview);
        if (btnPreviewDiscard)   btnPreviewDiscard.onClick.AddListener(DiscardPreview);
        if (inputPrompt)         inputPrompt.onSubmit.AddListener(_ => SendPrompt());
        if (btnLimpiar)          btnLimpiar.onClick.AddListener(() => Apply(CMD_LIMPIAR));
        if (btnQuitarResaltado)  btnQuitarResaltado.onClick.AddListener(() => Apply(CMD_SIN_RESALTADO));
        if (btnFijarVista)       btnFijarVista.onClick.AddListener(ToggleView);
        if (btnIniciar)          btnIniciar.onClick.AddListener(StartClass);
        if (btnTerminar)         btnTerminar.onClick.AddListener(() => { if (stopModal) stopModal.SetActive(true); });
        if (btnStopCancel)       btnStopCancel.onClick.AddListener(() => { if (stopModal) stopModal.SetActive(false); });
        if (btnStopConfirm)      btnStopConfirm.onClick.AddListener(StopClass);

        if (highlightButtonTemplate) highlightButtonTemplate.SetActive(false);
        if (stopModal) stopModal.SetActive(false);
        DiscardPreview();
        StopThinking();
        RefreshSumModeLabel();
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
            if (btn) btn.onClick.AddListener(() =>
                Apply(string.Format(HIGHLIGHT_FMT, captured, sumMode ? "true" : "false")));
        }

        if (btnQuitarResaltado) btnQuitarResaltado.gameObject.SetActive(elements.Count > 0);
    }

    // ── Instrucción en lenguaje natural ────────────────────────────────────────

    /// <summary>Manda lo escrito al intérprete. NO cambia nada: solo trae la traducción
    /// para que el docente la revise. Nada llega a los alumnos hasta que confirme.</summary>
    void SendPrompt()
    {
        if (busy) return;

        string text = inputPrompt ? inputPrompt.text.Trim() : "";
        if (string.IsNullOrEmpty(text)) return;

        busy = true;
        StartThinking();

        ApiManager.Instance.InterpretCommand(ClassContext.ClassId, text, currentVersion,
            onSuccess: resp =>
            {
                busy = false;
                StopThinking();
                if (resp == null) { ShowNotice("No se pudo interpretar la instrucción."); return; }

                // Sin acciones = no se entendió. El motivo viene del servidor, que sabe por
                // qué: mejor decírselo al docente que un "no funcionó" genérico.
                if (string.IsNullOrEmpty(resp.actionsJson))
                {
                    ShowNotice(string.IsNullOrEmpty(resp.reason)
                        ? "No se pudo interpretar la instrucción."
                        : resp.reason);
                    return;
                }

                pendingActions     = resp.actionsJson;
                pendingPrompt      = text;
                pendingBaseVersion = resp.baseVersion;
                ShowPreview(resp.preview);
            },
            onError: (code, detail) =>
            {
                busy = false;
                StopThinking();

                // Sin intérprete la clase NO se detiene: los botones hacen lo mismo sin
                // pasar por el proveedor. Por eso el aviso dice qué hacer, no solo qué falló.
                if (code == 503)
                {
                    ShowNotice("Servicio de interpretación no disponible. Usa los botones.");
                    return;
                }
                ShowNotice(MapError(code, detail));
            });
    }

    void ShowPreview(string[] lines)
    {
        if (previewText)
            previewText.text = (lines == null || lines.Length == 0)
                ? "(sin descripción)"
                : string.Join(NEWLINE, lines);

        if (previewPanel) previewPanel.SetActive(true);
    }

    /// <summary>Aplica lo que el docente acaba de aprobar. Se manda el actionsJson TAL
    /// CUAL vino, y con la baseVersion de la interpretación: si la escena cambió entre
    /// ver la vista previa y confirmar, el servidor responde 409 y no aplica nada.</summary>
    void ConfirmPreview()
    {
        if (busy || string.IsNullOrEmpty(pendingActions)) return;

        string actions = pendingActions;
        string prompt  = pendingPrompt;
        int    baseV   = pendingBaseVersion;
        DiscardPreview();

        busy = true;
        ApiManager.Instance.ApplyCommand(ClassContext.ClassId, actions, baseV,
            onSuccess: state =>
            {
                busy = false;
                if (inputPrompt) inputPrompt.text = "";
                if (state != null) Paint(state);
            },
            onError: (code, detail) =>
            {
                busy = false;
                if (detail == "ERR_STALE_SCENE")
                {
                    ShowNotice("La escena cambió. Se recargó: vuelve a escribirlo.");
                    LoadState();
                    return;
                }
                ShowNotice(MapError(code, detail));
            },
            source: "prompt", promptText: prompt);
    }

    /// <summary>Descarta la traducción. Como interpretar no tuvo efecto, no hay nada que
    /// deshacer: basta con olvidarla.</summary>
    void DiscardPreview()
    {
        pendingActions = "";
        pendingPrompt  = "";
        if (previewPanel) previewPanel.SetActive(false);
    }

    /// <summary>Solo se deshabilita el botón de enviar: ni la pantalla ni el campo de
    /// texto se bloquean. El docente está frente a 25 alumnos y tiene que poder seguir
    /// tocando botones mientras el asistente piensa.</summary>
    void StartThinking()
    {
        if (btnEnviarPrompt) btnEnviarPrompt.interactable = false;
        if (thinkingCo != null) StopCoroutine(thinkingCo);
        thinkingCo = StartCoroutine(ShowThinkingAfterDelay());
    }

    IEnumerator ShowThinkingAfterDelay()
    {
        yield return new WaitForSeconds(THINKING_DELAY);
        if (thinkingIndicator) thinkingIndicator.SetActive(true);
        thinkingCo = null;
    }

    void StopThinking()
    {
        if (thinkingCo != null) { StopCoroutine(thinkingCo); thinkingCo = null; }
        if (thinkingIndicator)  thinkingIndicator.SetActive(false);
        if (btnEnviarPrompt)    btnEnviarPrompt.interactable = true;
    }

    void ToggleSumMode()
    {
        sumMode = !sumMode;
        RefreshSumModeLabel();
    }

    void RefreshSumModeLabel()
    {
        if (lblModoSumar) lblModoSumar.text = sumMode ? "Resaltar: sumando" : "Resaltar: solo uno";
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
