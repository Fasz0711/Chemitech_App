using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Lista de clases. Una sola pantalla con dos caras, decididas por el rol que
/// devuelve /classes/mine (no por lo que diga el dispositivo):
///   ALUMNO  → sus clases, con su código; tocar una entra.
///   DOCENTE → sus clases, con Crear clase, Iniciar y Terminar.
///
/// El rol que manda es el de la RESPUESTA, no el de SessionData: si a alguien lo
/// hacen docente entre dos sesiones, la pantalla se adapta sin esperar al login.
/// </summary>
public class MisClasesManager : MonoBehaviour
{
    [Header("Header")]
    [SerializeField] private Button btnAtras;

    [Header("Estados de la pantalla")]
    [SerializeField] private GameObject      loadingGroup;
    [SerializeField] private GameObject      listGroup;
    [SerializeField] private GameObject      emptyStateGroup;
    [SerializeField] private TextMeshProUGUI emptyLabel;

    [Header("Lista")]
    [SerializeField] private RectTransform listContent;
    [SerializeField] private GameObject    cardTemplate;   // inactivo; se clona

    [Header("Docente")]
    [SerializeField] private GameObject teacherBar;        // barra con "Crear clase"
    [SerializeField] private Button     btnCrearClase;

    [Header("Modal: crear clase")]
    [SerializeField] private GameObject      createModal;
    [SerializeField] private TMP_InputField  inputName;
    [SerializeField] private TMP_InputField  inputSection;
    [SerializeField] private TMP_InputField  inputCount;
    [SerializeField] private Button          btnCreateConfirm;
    [SerializeField] private Button          btnCreateCancel;
    [SerializeField] private TextMeshProUGUI createError;

    [Header("Modal: códigos generados")]
    [SerializeField] private GameObject      codesModal;
    [SerializeField] private TextMeshProUGUI codesTitle;
    [SerializeField] private RectTransform   codesContent;
    [SerializeField] private GameObject      codeRowTemplate;  // inactivo; se clona
    [SerializeField] private Button          btnCodesClose;

    [Header("Modal: confirmar terminar")]
    [SerializeField] private GameObject      stopModal;
    [SerializeField] private TextMeshProUGUI stopMessage;
    [SerializeField] private Button          btnStopConfirm;
    [SerializeField] private Button          btnStopCancel;

    [Header("Aviso")]
    [SerializeField] private GameObject      noticeRoot;
    [SerializeField] private TextMeshProUGUI noticeText;

    [Header("Escenas")]
    [SerializeField] private string escenaMenu   = "SampleScene";
    [SerializeField] private string escenaEspera = "ClaseEsperaScene";
    [SerializeField] private string escenaClaseDocente = "ClaseDocenteScene";

    readonly List<GameObject> spawnedCards = new List<GameObject>();
    bool      isTeacher;
    bool      busy;
    string    pendingStopId = "";
    Coroutine noticeCo;

    RectTransform listRT;
    RectTransform teacherBarRT;
    Vector2       listOffsetMaxTeacher;   // el que dejó el ajuste manual de la escena

    const float NOTICE_SECONDS = 3.5f;

    void Start()
    {
        if (btnAtras)         btnAtras.onClick.AddListener(() => SceneManager.LoadScene(escenaMenu));
        if (btnCrearClase)    btnCrearClase.onClick.AddListener(OpenCreateModal);
        if (btnCreateCancel)  btnCreateCancel.onClick.AddListener(CloseCreateModal);
        if (btnCreateConfirm) btnCreateConfirm.onClick.AddListener(OnCreateConfirm);
        if (btnCodesClose)    btnCodesClose.onClick.AddListener(CloseCodesModal);
        if (btnStopCancel)    btnStopCancel.onClick.AddListener(CloseStopModal);
        if (btnStopConfirm)   btnStopConfirm.onClick.AddListener(OnStopConfirm);

        if (cardTemplate)    cardTemplate.SetActive(false);
        if (codeRowTemplate) codeRowTemplate.SetActive(false);
        CloseCreateModal();
        CloseCodesModal();
        CloseStopModal();
        HideNotice();
        CacheListLayout();

        Refresh();
    }

    // ── Carga ──────────────────────────────────────────────────────────────────
    void Refresh()
    {
        ShowOnly(loading: true);

        ApiManager.Instance.GetMyClasses(
            onSuccess: resp =>
            {
                isTeacher = resp != null && resp.role == SessionData.ROLE_TEACHER;
                if (teacherBar) teacherBar.SetActive(isTeacher);
                LayoutListForRole();

                var classes = (resp != null && resp.classes != null) ? resp.classes : new ClassroomDTO[0];
                if (classes.Length == 0) { ShowEmpty(); return; }

                ShowOnly(list: true);
                PopulateList(classes);
            },
            onError: (code, detail) =>
            {
                LayoutListForRole();
                ShowEmpty();
                ShowNotice(MapError(code, detail));
            });
    }

    void ShowEmpty()
    {
        ShowOnly(empty: true);
        if (emptyLabel)
            emptyLabel.text = isTeacher
                ? "Todavía no has creado ninguna clase."
                : "No tienes clases asignadas.\nTu docente te dará un código.";
    }

    void ShowOnly(bool loading = false, bool list = false, bool empty = false)
    {
        if (loadingGroup)    loadingGroup.SetActive(loading);
        if (listGroup)       listGroup.SetActive(list);
        if (emptyStateGroup) emptyStateGroup.SetActive(empty);
    }

    // ── Alto de la lista según rol ─────────────────────────────────────────────
    /// <summary>El ListState de la escena está ajustado a mano para el docente: su borde
    /// superior deja hueco a la barra de "Crear clase". Al alumno esa barra no se le
    /// muestra, así que sin esto la lista queda flotando sobre el hueco vacío.</summary>
    void CacheListLayout()
    {
        listRT       = listGroup  ? listGroup.GetComponent<RectTransform>()  : null;
        teacherBarRT = teacherBar ? teacherBar.GetComponent<RectTransform>() : null;
        if (listRT) listOffsetMaxTeacher = listRT.offsetMax;
    }

    /// <summary>El destino del alumno se lee del rect de la propia barra, no de un número
    /// fijo: si la barra se mueve a mano, la cara del alumno la sigue sin tocar código.
    /// Los dos offsetMax.y se miden desde el mismo borde (anchorMax.y = 1 en ambos), así
    /// que se pueden copiar directo.</summary>
    void LayoutListForRole()
    {
        if (!listRT) return;

        // Docente: la barra ocupa su sitio, la lista se queda donde la dejaron.
        if (isTeacher || !teacherBarRT) { listRT.offsetMax = listOffsetMaxTeacher; return; }

        // Alumno: sin barra, la lista empieza donde empezaría la barra.
        listRT.offsetMax = new Vector2(listOffsetMaxTeacher.x, teacherBarRT.offsetMax.y);
    }

    void PopulateList(ClassroomDTO[] classes)
    {
        foreach (var go in spawnedCards) if (go) Destroy(go);
        spawnedCards.Clear();

        foreach (var c in classes) BuildCard(c);
    }

    void BuildCard(ClassroomDTO c)
    {
        if (!cardTemplate || !listContent) return;

        var card = Instantiate(cardTemplate, listContent);
        card.name = "Card_" + c.section;
        card.SetActive(true);
        spawnedCards.Add(card);

        SetText(card, "Name",   c.name);
        SetText(card, "Status", StatusLabel(c.status));

        // Lo único que cambia por rol: al alumno le importa quién dicta la clase;
        // al docente, cuántos alumnos tiene.
        SetText(card, "Subtitle", isTeacher
            ? c.section + " · " + c.studentCount + " alumnos"
            : c.section + " · " + c.teacherName);

        // El código solo existe para el alumno (al docente le llega vacío).
        SetText(card, "Code", string.IsNullOrEmpty(c.code) ? "" : "Tu código: " + c.code);

        var btnEnter = FindButton(card, "BtnEnter");
        var btnStart = FindButton(card, "BtnStart");
        var btnStop  = FindButton(card, "BtnStop");

        bool ended   = c.status == "ended";
        bool running = c.status == "running";

        string id   = c.publicId;
        string name = c.name;

        if (btnEnter)
        {
            // Entrar es del alumno; el docente usa Conducir, que abre la misma escena
            // con el panel de conducción.
            btnEnter.gameObject.SetActive(!isTeacher);
            btnEnter.interactable = !ended;
            btnEnter.onClick.AddListener(() => EnterClass(id, name, ended));
        }

        if (btnStart)
        {
            btnStart.gameObject.SetActive(isTeacher && !running && !ended);
            btnStart.onClick.AddListener(() => DoStart(id));
        }

        if (btnStop)
        {
            btnStop.gameObject.SetActive(isTeacher && running);
            btnStop.onClick.AddListener(() => AskStop(id, name));
        }

        var btnConducir = FindButton(card, "BtnConducir");
        if (btnConducir)
        {
            // Conducir sirve también con la clase sin iniciar: el docente prepara la
            // primera molécula antes de que entren los alumnos, y al iniciar la ven de
            // golpe. Con la clase terminada ya no hay nada que conducir.
            btnConducir.gameObject.SetActive(isTeacher && !ended);
            btnConducir.onClick.AddListener(() => Conduct(id, name));
        }
    }

    static string StatusLabel(string status)
    {
        switch (status)
        {
            case "running": return "En curso";
            case "ended":   return "Terminada";
            default:        return "Sin iniciar";
        }
    }

    // ── Alumno: entrar ─────────────────────────────────────────────────────────
    void EnterClass(string id, string name, bool ended)
    {
        if (ended) { ShowNotice("Esa clase ya terminó."); return; }

        ClassContext.Set(id, name);
        SceneManager.LoadScene(escenaEspera);
    }

    void Conduct(string id, string name)
    {
        ClassContext.Set(id, name);
        SceneManager.LoadScene(escenaClaseDocente);
    }

    // ── Docente: iniciar / terminar ────────────────────────────────────────────
    void DoStart(string id)
    {
        if (busy) return;
        busy = true;

        ApiManager.Instance.StartClass(id,
            onSuccess: _ => { busy = false; Refresh(); },
            onError:   (code, detail) => { busy = false; ShowNotice(MapError(code, detail)); });
    }

    void AskStop(string id, string name)
    {
        pendingStopId = id;
        if (stopMessage)
            stopMessage.text = "Vas a terminar \"" + name + "\".\n\nEsto es definitivo: la clase no se puede volver a abrir.";
        if (stopModal) stopModal.SetActive(true);
    }

    void OnStopConfirm()
    {
        if (busy || string.IsNullOrEmpty(pendingStopId)) return;
        busy = true;
        string id = pendingStopId;
        CloseStopModal();

        ApiManager.Instance.StopClass(id,
            onSuccess: _ => { busy = false; Refresh(); },
            onError:   (code, detail) => { busy = false; ShowNotice(MapError(code, detail)); });
    }

    void CloseStopModal()
    {
        pendingStopId = "";
        if (stopModal) stopModal.SetActive(false);
    }

    // ── Docente: crear clase ───────────────────────────────────────────────────
    void OpenCreateModal()
    {
        if (inputName)    inputName.text    = "";
        if (inputSection) inputSection.text = "";
        if (inputCount)   inputCount.text   = "25";
        if (createError)  createError.text  = "";
        if (createModal)  createModal.SetActive(true);
    }

    void CloseCreateModal()
    {
        if (createModal) createModal.SetActive(false);
    }

    void OnCreateConfirm()
    {
        if (busy) return;

        string name    = inputName    ? inputName.text.Trim()    : "";
        string section = inputSection ? inputSection.text.Trim() : "";
        string countTx = inputCount   ? inputCount.text.Trim()   : "";

        // Validación mínima, solo para no gastar un viaje al servidor con el formulario
        // vacío. El backend vuelve a validar todo y sus mensajes son los que mandan.
        if (string.IsNullOrEmpty(name))    { SetCreateError("Ponle un nombre a la clase."); return; }
        if (string.IsNullOrEmpty(section)) { SetCreateError("Indica la sección (por ejemplo 3A)."); return; }
        if (!int.TryParse(countTx, out int count) || count < 1)
        {
            SetCreateError("La cantidad de alumnos debe ser un número.");
            return;
        }

        busy = true;
        SetCreateError("");

        ApiManager.Instance.CreateClass(name, section, count,
            onSuccess: resp =>
            {
                busy = false;
                CloseCreateModal();
                ShowCodes(resp);
                Refresh();
            },
            onError: (code, detail) =>
            {
                busy = false;
                SetCreateError(MapError(code, detail));
            });
    }

    void SetCreateError(string msg)
    {
        if (createError) createError.text = msg;
    }

    // ── Códigos generados ──────────────────────────────────────────────────────
    /// <summary>Las contraseñas SOLO se ven aquí: el backend guarda el hash y no puede
    /// volver a mostrarlas. Si el docente cierra sin copiarlas, hay que reponerlas
    /// alumno por alumno.</summary>
    void ShowCodes(ClassCreatedResponse resp)
    {
        if (resp == null || resp.students == null || codesContent == null) return;

        for (int i = codesContent.childCount - 1; i >= 0; i--)
        {
            var child = codesContent.GetChild(i).gameObject;
            if (child != codeRowTemplate) Destroy(child);
        }

        if (codesTitle)
            codesTitle.text = "Códigos de " + (resp.classroom != null ? resp.classroom.name : "la clase");

        foreach (var s in resp.students)
        {
            if (!codeRowTemplate) break;
            var row = Instantiate(codeRowTemplate, codesContent);
            row.name = "Row_" + s.code;
            row.SetActive(true);
            SetText(row, "Code",     s.code);
            SetText(row, "Password", s.password);
        }

        if (codesModal) codesModal.SetActive(true);
    }

    void CloseCodesModal()
    {
        if (codesModal) codesModal.SetActive(false);
    }

    // ── Avisos ─────────────────────────────────────────────────────────────────
    void ShowNotice(string msg)
    {
        if (!noticeRoot || !noticeText) { Debug.LogWarning("[Clases] " + msg); return; }
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

    /// <summary>Ningún 403 de clase es sesión muerta: la sesión vale, lo que falta es
    /// permiso sobre ESA clase. ApiManager solo expulsa en 401; aquí solo se explica.</summary>
    static string MapError(int code, string detail)
    {
        switch (detail)
        {
            case "ERR_NOT_TEACHER":           return "Tu cuenta no es de docente.";
            case "ERR_NOT_CLASS_OWNER":       return "Esa clase es de otro docente.";
            case "ERR_NOT_CLASS_MEMBER":      return "No estás en esa clase.";
            case "ERR_CLASS_NOT_FOUND":       return "Esa clase ya no existe.";
            case "ERR_CLASS_ALREADY_ENDED":   return "La clase ya terminó y no se puede reabrir.";
            case "ERR_CLASS_NOT_RUNNING":     return "Esa clase todavía no ha empezado.";
            case "ERR_CLASS_CODES_TAKEN":     return "Esa sección ya tiene cuentas creadas; usa otra (por ejemplo 3A2).";
            case "ERR_CLASS_FULL":            return "La clase llegó al máximo de 99 alumnos.";
            case "ERR_CLASS_NAME_REQUIRED":   return "Ponle un nombre a la clase.";
            case "ERR_CLASS_NAME_TOO_LONG":   return "El nombre es demasiado largo (máximo 60).";
            case "ERR_SECTION_INVALID":       return "Sección inválida: 2 a 10 letras o números, sin guiones ni espacios.";
            case "ERR_STUDENT_COUNT_INVALID": return "La cantidad debe estar entre 1 y 50.";
            case "ERR_RATE_LIMITED":          return "Demasiadas peticiones. Espera un momento.";
            case "INVALID_UUID":              return "Esa clase no es válida.";
        }
        if (code == 0 || code >= 500) return "Sin conexión con el servidor. Inténtalo de nuevo.";
        return "No se pudo completar la acción. Inténtalo de nuevo.";
    }

    // ── Helpers de plantilla ───────────────────────────────────────────────────
    static void SetText(GameObject root, string childName, string value)
    {
        var t   = FindDeep(root.transform, childName);
        var tmp = t ? t.GetComponent<TextMeshProUGUI>() : null;
        if (tmp) tmp.text = value;
    }

    static Button FindButton(GameObject root, string childName)
    {
        var t = FindDeep(root.transform, childName);
        return t ? t.GetComponent<Button>() : null;
    }

    static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform c in parent)
        {
            if (c.name == name) return c;
            var r = FindDeep(c, name);
            if (r) return r;
        }
        return null;
    }
}
