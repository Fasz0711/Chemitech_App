using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// La pizarra virtual: UN UNIVERSO 3D con dos interfaces encima.
///
///   • MODO PIZARRA:  el panel de conducción (catálogo, resaltar, instrucción escrita).
///   • MODO UNIVERSO: el HUD creativo, el mismo que en "Mis universos": selector de
///     átomos, barra de ranuras, colocar, mover y borrar.
///
/// El espacio 3D y la cámara son LOS MISMOS en los dos modos; el botón junto al nombre
/// de la clase solo enciende y apaga grupos de UI. Lo que el docente construye NO llega
/// a los alumnos hasta que pulsa "Mostrar a la clase": ver armar una molécula átomo por
/// átomo es confuso, y así puede preparar la siguiente mientras habla de la actual.
/// Mientras haya algo construido y sin publicar se enciende un aviso, que es la única
/// defensa contra explicar señalando algo que en los celulares no está.
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

    [Header("Universo (modo construcción)")]
    [SerializeField] private AtomPlacementController placement;
    [SerializeField] private BondManager             bonds;
    [SerializeField] private GameObject      uiPizarra;      // panel de conducción
    [SerializeField] private GameObject      uiUniverso;     // HUD creativo
    [SerializeField] private Button          btnModo;
    [SerializeField] private TextMeshProUGUI lblModo;
    [SerializeField] private Button          btnPublicar;
    [SerializeField] private GameObject      unpublishedBadge;
    [SerializeField] private TextMeshProUGUI limitsLabel;
    [SerializeField] private TextMeshProUGUI sceneSummary;   // qué reconoció el servidor
    [SerializeField] private GameObject      editTint;       // tinte sutil al construir

    [Header("Render")]
    [SerializeField] private float worldScale    = 1f;
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

    // "Todos a mi vista": manda la orientación ACTUAL del docente una sola vez. No es un
    // candado. locked va en false a propósito: encenderlo dejaría al alumno clavado a la
    // vista del docente para siempre, que es justo lo que se quiso quitar.
    //
    // Los tres números se formatean con InvariantCulture: en un celular en español
    // saldrían con coma y el JSON dejaría de ser válido.
    const string VIEW_FMT =
        @"[{{""action"":""view"",""locked"":false,""x"":{0},""y"":{1},""z"":{2},""yaw"":{3},""pitch"":{4}}}]";
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
    bool      universeMode; // false = pizarra (el modo en el que se entra)

    // El recuento de topes se refresca a intervalos, no cada frame: agrupar es O(n²) y
    // con 60 átomos serían 1800 comparaciones por frame para un texto que cambia poco.
    const float LIMITS_INTERVAL = 0.3f;
    float nextLimitsCheck;

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
    bool      cameraRestored;   // la vista guardada se aplica una vez, al entrar
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
        if (btnFijarVista)       btnFijarVista.onClick.AddListener(PushView);
        if (lblFijarVista)       lblFijarVista.text = "Todos a mi vista";
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

        if (btnModo)      btnModo.onClick.AddListener(ToggleMode);
        if (btnPublicar)  btnPublicar.onClick.AddListener(Publish);

        // El diario del docente NO se llena de moléculas de demostración: está dando
        // clase, no jugando. Quitar el userPublicId no serviría —desde el retrofit de
        // identidad, si la petición lleva token manda el token—, así que va el flag.
        if (bonds) bonds.RecordDiscoveries = false;

        if (!sceneRoot) sceneRoot = transform;

        // Con placement, los átomos que llegan del servidor se crean COMO SUYOS y el
        // docente puede moverlos y borrarlos. Sin él (no debería pasar en esta escena)
        // el renderer cae a sus esferas de solo lectura y la pizarra sigue funcionando
        // como antes de la fusión.
        sceneRenderer = new ClassSceneRenderer(sceneRoot, atomMaterial, bondMaterial,
                                               worldScale, atomSize, bondThickness, bondSpacing,
                                               placement);

        SetMode(false);   // se entra conduciendo, no construyendo
        RefreshUnpublished();

        if (!ClassContext.HasClass) { Leave(); return; }

        LoadState();
        StartCoroutine(RosterLoop());
    }

    void Update()
    {
        sceneRenderer?.UpdateVisuals();
        RefreshUnpublished();
        RefreshLimits();
    }

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
                //
                // Salvo que hubiera un universo a medio construir: recargar lo reemplaza
                // por lo que ven los alumnos y ese trabajo SÍ se pierde. Decirlo, porque
                // "vuelve a intentarlo" daría a entender que sigue ahí.
                if (detail == "ERR_STALE_SCENE")
                {
                    bool hadUnpublished = placement && placement.Dirty;
                    ShowNotice(hadUnpublished
                        ? "La clase cambió desde otro dispositivo. Se recargó lo que ven los alumnos y se perdió lo que tenías sin publicar."
                        : "La escena cambió. Se recargó: vuelve a intentarlo.");
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
        LogState(state);

        sceneRenderer.Render(state.molecules);
        sceneRenderer.ApplyHighlights(state.highlights);

        // Los enlaces vienen resueltos por el servidor: dárselos a BondManager los dibuja
        // y, de paso, le marca esta estructura como YA DETECTADA. Sin eso volvería a
        // mandar a detectar lo que el servidor acaba de decir, y con la red lenta la
        // pizarra se quedaría unos segundos sin enlaces después de cada comando.
        if (bonds) bonds.ImportBonds(sceneRenderer.Bonds);

        // Lo que hay en pantalla es exactamente lo que ven los alumnos.
        if (placement) placement.ClearDirty();
        RefreshUnpublished();

        bool empty = state.molecules == null || state.molecules.Length == 0;
        if (emptyHint) emptyHint.SetActive(empty);

        // La vista guardada se recupera SOLO al entrar. Aplicarla en cada estado hacía
        // que la cámara del docente saltara a la posición almacenada cada vez que pulsaba
        // cualquier botón: se colocaba en un buen ángulo, mostraba agua, y la vista se le
        // iba sola. El docente es quien conduce; su cámara es suya.
        if (!cameraRestored && HasPosition(state.camera))
        {
            cameraRestored = true;
            var c = state.camera;
            if (cam) cam.SetView(new Vector3(c.x, c.y, c.z), c.yaw, c.pitch);
        }

        RefreshControls(state);
        RebuildHighlightButtons(state);
        RefreshSummary(state);
    }

    void RefreshControls(ClassStateResponse state)
    {
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

    /// <summary>Trae a todos los alumnos a la vista que el docente tiene ahora mismo.
    ///
    /// Es un EMPUJÓN, no un candado: después de saltar, cada alumno sigue pudiendo girar
    /// y acercarse por su cuenta. Poder mirar la molécula por el otro lado mientras el
    /// docente explica es media gracia de que esto sea 3D.</summary>
    void PushView()
    {
        if (!cam) { ShowNotice("No encuentro la cámara."); return; }

        var inv = System.Globalization.CultureInfo.InvariantCulture;
        Vector3 p = cam.ViewPosition;
        Apply(string.Format(VIEW_FMT,
            p.x.ToString("0.###", inv),
            p.y.ToString("0.###", inv),
            p.z.ToString("0.###", inv),
            cam.ViewYaw.ToString("0.##", inv),
            cam.ViewPitch.ToString("0.##", inv)));
    }

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

    /// <summary>Deja en la consola qué llegó de verdad: por molécula, su nombre, cuántos
    /// átomos y CUÁNTOS ENLACES.
    ///
    /// Existe porque "los enlaces desaparecieron al publicar" tiene dos causas posibles
    /// que se ven idénticas en pantalla —que el servidor no los mandara, o que el cliente
    /// no los dibujara— y sin este renglón no hay forma de saber cuál fue. El cliente no
    /// puede dibujar lo que no recibe.</summary>
    static void LogState(ClassStateResponse state)
    {
        var sb = new System.Text.StringBuilder("[Pizarra] v").Append(state.version).Append(" ->");
        if (state.molecules == null || state.molecules.Length == 0) sb.Append(" (escena vacia)");
        else
            foreach (var m in state.molecules)
            {
                if (m == null) continue;
                sb.Append(" [").Append(string.IsNullOrEmpty(m.name) ? "sin nombre" : m.name)
                  .Append(": ").Append(m.atoms != null ? m.atoms.Length : 0).Append(" atomos, ")
                  .Append(m.bonds != null ? m.bonds.Length : 0).Append(" enlaces]");
            }
        Debug.Log(sb.ToString());
    }

    /// <summary>Qué hay en la pizarra, según el SERVIDOR. Es la única forma que tiene el
    /// docente de saber si lo que construyó se reconoció como algo.
    ///
    /// Una molécula puede llegar SIN NOMBRE (cadena vacía, nunca null): es el caso normal
    /// mientras construye —dos carbonos sueltos no son una molécula completa— y no es un
    /// error. Esas no se nombran, se cuentan; publicar estructuras a medias a propósito
    /// es material didáctico legítimo y el renglón no debe dar a entender lo contrario.</summary>
    void RefreshSummary(ClassStateResponse state)
    {
        if (!sceneSummary) return;

        var named = new List<string>();
        int unnamed = 0;

        if (state.molecules != null)
            foreach (var m in state.molecules)
            {
                if (m == null) continue;
                if (string.IsNullOrEmpty(m.name)) unnamed++;
                else if (!named.Contains(m.name)) named.Add(m.name);
            }

        string text = "";
        if (named.Count > 0) text = "En la pizarra: " + string.Join(", ", named);
        if (unnamed > 0)
            text += (text.Length > 0 ? " · " : "")
                  + (unnamed == 1 ? "1 estructura sin identificar"
                                  : $"{unnamed} estructuras sin identificar");

        sceneSummary.text = text;
        sceneSummary.gameObject.SetActive(text.Length > 0);
    }

    // ── Los dos modos ──────────────────────────────────────────────────────────

    void ToggleMode() => SetMode(!universeMode);

    /// <summary>Enciende y apaga grupos de UI. NO toca el contenido 3D ni la cámara:
    /// que el universo siga exactamente donde estaba es lo que hace que el cambio se
    /// sienta como girar la vista y no como abrir otra pantalla.</summary>
    void SetMode(bool universe)
    {
        universeMode = universe;

        if (uiUniverso) uiUniverso.SetActive(universe);
        if (uiPizarra)  uiPizarra.SetActive(!universe);
        if (editTint)   editTint.SetActive(universe);

        // En pizarra se puede rotar la cámara pero no tocar los átomos: el docente está
        // conduciendo y un roce no debería moverle una molécula.
        if (placement) placement.SetInteractive(universe);

        if (lblModo) lblModo.text = universe ? "Ir a pizarra" : "Ir a universo";

        // Una traducción a medio confirmar pertenece al panel de conducción. Se descarta
        // en vez de dejarla esperando: interpretar no cambió nada, así que no hay nada
        // que deshacer.
        if (universe) DiscardPreview();

        RefreshLimits(force: true);
    }

    // ── Publicar ───────────────────────────────────────────────────────────────

    /// <summary>Manda a los alumnos el estado actual del universo. Es el ÚNICO momento
    /// en que lo que el docente construyó sale de su pantalla.</summary>
    void Publish()
    {
        if (busy || !placement) return;

        var atoms = placement.GetOrderedAtoms();

        // Solo se frena por el total de átomos, que es un número EXACTO. Los fragmentos y
        // los átomos pesados por fragmento se cuentan con el corte del cliente, que no es
        // el del servidor: bloquear con una aproximación puede negar una publicación que
        // el servidor habría aceptado, y el docente no tendría forma de saber que el "no"
        // se lo inventó su propio teléfono. Esos dos los decide el servidor, que ahora
        // devuelve un código distinto para cada uno.
        if (atoms.Count > SetAtomsCommand.MAX_TOTAL_ATOMS)
        {
            ShowNotice($"Son demasiados átomos: {atoms.Count} de {SetAtomsCommand.MAX_TOTAL_ATOMS}.");
            return;
        }

        // Un universo vacío SE PUBLICA IGUAL, como una lista de cero átomos: publicar es
        // "esto es lo que hay ahora", y lo que hay ahora es nada. Sustituirlo por 'clear'
        // funcionaba, pero hacía que vaciar la pizarra pasara por otro camino distinto al
        // de cualquier otra publicación.
        Apply(SetAtomsCommand.Build(atoms, WorldToAngstrom));
    }

    /// <summary>De unidades de mundo a ångströms. Es el INVERSO exacto del factor con el
    /// que se dibuja lo que llega del servidor, y tiene que serlo: si no, lo publicado
    /// volvería con otro tamaño y el universo daría un salto en cada comando.</summary>
    float WorldToAngstrom => worldScale > 0f ? 1f / worldScale : 1f;

    /// <summary>¿El estado trae una vista de verdad? Mientras el servidor no guarde la
    /// posición, los tres valores llegan en cero y aplicarlos teletransportaría a quien
    /// entra al origen, dentro de las moléculas. Sin posición, no se toca la cámara.</summary>
    static bool HasPosition(CameraDTO c)
        => c != null && (c.x != 0f || c.y != 0f || c.z != 0f);

    /// <summary>El mismo corte con el que se mandan los grupos a detectar, para que la
    /// cuenta de fragmentos y lo que se detecta hablen de lo mismo.</summary>
    float ClusterDistance => bonds ? bonds.ClusterDistance : 2f;

    // ── Cambios sin publicar ───────────────────────────────────────────────────

    /// <summary>El aviso honesto: mientras el universo tenga algo que los alumnos no ven,
    /// se dice. Sin esto el docente puede colocar tres átomos, olvidarse de publicar y
    /// explicar señalando algo que en los 25 celulares no está.</summary>
    void RefreshUnpublished()
    {
        bool pending = placement && placement.Dirty;
        if (unpublishedBadge && unpublishedBadge.activeSelf != pending)
            unpublishedBadge.SetActive(pending);
    }

    // ── Topes ──────────────────────────────────────────────────────────────────

    void RefreshLimits(bool force = false)
    {
        if (!limitsLabel || !placement) return;

        if (!universeMode)
        {
            if (limitsLabel.gameObject.activeSelf) limitsLabel.gameObject.SetActive(false);
            return;
        }

        if (!force && Time.time < nextLimitsCheck) return;
        nextLimitsCheck = Time.time + LIMITS_INTERVAL;

        // Se recuenta entero cada vez, sin atajar por número de átomos: separar dos que
        // ya estaban colocados no cambia cuántos hay pero sí en cuántos fragmentos caen,
        // y el tope de fragmentos es justo el que se alcanza antes construyendo a mano.
        var atoms = placement.GetOrderedAtoms();

        string msg = SetAtomsCommand.WarningFor(SetAtomsCommand.Count(atoms, ClusterDistance));
        limitsLabel.text = msg;
        limitsLabel.gameObject.SetActive(!string.IsNullOrEmpty(msg));
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
            case "ERR_SCENE_FULL":         return $"La pizarra está llena ({SetAtomsCommand.MAX_FRAGMENTS} moléculas). Junta o quita algunas.";
            case "ERR_UNKNOWN_MOLECULE":   return "Hay un elemento que el servidor no reconoce.";
            case "ERR_MOLECULE_TOO_BIG":   return $"Esa molécula es demasiado grande ({SetAtomsCommand.MAX_HEAVY_PER_FRAGMENT} átomos pesados como máximo).";
            case "ERR_SCENE_REFERENCE":    return "Eso no está en la escena.";
            // Desde el 26/09 el servidor separa los tres motivos que antes compartían
            // ERR_INVALID_ACTION. Ya no hay que deducir cuál fue por el número de átomos.
            case "ERR_TOO_MANY_ATOMS":
                return $"Son demasiados átomos: el máximo es {SetAtomsCommand.MAX_TOTAL_ATOMS}.";

            // Esto NO es culpa del docente ni de lo que construyó: el actionsJson salió
            // mal formado de aquí. Se dice tal cual para que nadie pierda la clase
            // buscando qué molécula estaba mal.
            case "ERR_ACTIONS_JSON_MALFORMED":
                return "Fallo del cliente al preparar el comando. Repórtalo: no es lo que construiste.";

            case "ERR_INVALID_ACTION":
                return "El servidor no reconoció la acción.";
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
