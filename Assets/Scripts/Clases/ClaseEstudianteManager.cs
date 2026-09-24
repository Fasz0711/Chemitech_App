using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// La escena de la clase vista por el alumno: SOLO LECTURA.
///
/// Dibuja lo que manda el docente y nada más. No hay hotbar, ni selector de átomos, ni
/// colocación: el alumno puede mirar y mover su cámara, pero no editar. Tampoco corre
/// detección — la química llega ya resuelta del servidor.
///
/// Lo que el alumno hace con su cámara es LOCAL: no viaja al servidor ni afecta a nadie.
/// Si el docente fija la vista (camera.locked), la suya se alinea y deja de ser libre.
/// </summary>
public class ClaseEstudianteManager : MonoBehaviour
{
    [Header("Cámara")]
    [SerializeField] private OrbitCameraController cam;

    [Header("Mundo")]
    [SerializeField] private Transform sceneRoot;       // padre de átomos y enlaces
    [SerializeField] private Material  atomMaterial;
    [SerializeField] private Material  bondMaterial;

    [Header("HUD")]
    [SerializeField] private TextMeshProUGUI className;
    [SerializeField] private TextMeshProUGUI syncLabel;
    [SerializeField] private GameObject      followingBadge;   // "Siguiendo la vista del docente"
    [SerializeField] private Button          btnVolverVista;
    [SerializeField] private Button          btnSalir;
    [SerializeField] private Button          btnCopiar;
    [SerializeField] private GameObject      emptyHint;        // "El docente aún no ha puesto nada"
    [SerializeField] private GameObject      noticeRoot;
    [SerializeField] private TextMeshProUGUI noticeText;

    [Header("Render")]
    [SerializeField] private float worldScale    = 6f;
    [SerializeField] private float atomSize      = 0.9f;
    [SerializeField] private float bondThickness = 0.09f;
    [SerializeField] private float bondSpacing   = 0.20f;

    [Header("Copiar a un universo")]
    [Tooltip("Ångström -> unidades del universo. Un enlace simple mide ~1 Å y en la zona " +
             "de juego los átomos se colocan a ~1.5 unidades, que es lo que agrupa el detector.")]
    [SerializeField] private float copyScale  = 1.6f;
    [Tooltip("Altura mínima sobre la plataforma, para que nada quede enterrado.")]
    [SerializeField] private float copyFloorY = 0.6f;

    [Header("Sondeo")]
    [Tooltip("Segundos entre sondeos. El límite del servidor son 60 por minuto.")]
    [SerializeField] private float pollSeconds = 3f;

    [Header("Escenas")]
    [SerializeField] private string escenaEspera = "ClaseEsperaScene";
    [SerializeField] private string escenaClases = "MisClasesScene";

    ClassSceneRenderer sceneRenderer;
    SceneMoleculeDTO[] lastMolecules;   // lo último dibujado, que es lo que se copia

    int  lastVersion = -1;
    bool polling;
    bool leaving;
    bool paused;          // app en segundo plano: se deja de sondear
    Coroutine noticeCo;

    const float NOTICE_SECONDS = 3.5f;

    // Última vista que mandó el docente, para el botón "volver a su vista".
    float docYaw = 35f, docPitch = 28f, docDistance = 16f;
    bool  cameraLocked;

    void Start()
    {
        if (btnSalir)        btnSalir.onClick.AddListener(Leave);
        if (btnVolverVista)  btnVolverVista.onClick.AddListener(SnapToTeacherView);
        if (btnCopiar)       btnCopiar.onClick.AddListener(CopyToUniverse);

        if (className) className.text = ClassContext.HasClass ? ClassContext.ClassName : "Clase";
        SetSync(true);
        if (followingBadge) followingBadge.SetActive(false);
        HideNotice();

        if (!sceneRoot) sceneRoot = transform;
        sceneRenderer = new ClassSceneRenderer(sceneRoot, atomMaterial, bondMaterial,
                                          worldScale, atomSize, bondThickness, bondSpacing);

        if (!ClassContext.HasClass) { Leave(); return; }

        StartCoroutine(PollLoop());
    }

    void Update()
    {
        sceneRenderer?.UpdateVisuals();

        // Con la vista fijada, la cámara del alumno se mantiene en la del docente.
        if (cameraLocked && cam) cam.SetView(docYaw, docPitch, docDistance);
    }

    void OnDestroy() => sceneRenderer?.Dispose();

    // Con la pantalla bloqueada o la app de fondo no hay nadie mirando: seguir sondeando
    // solo gastaría batería y datos, y son 25 celulares a la vez.
    void OnApplicationPause(bool pause) => paused = pause;
    void OnApplicationFocus(bool focus) => paused = !focus;

    // ── Sondeo ─────────────────────────────────────────────────────────────────
    IEnumerator PollLoop()
    {
        // Jitter: sin él los 25 celulares sondean en el mismo tic y esa ráfaga es la
        // que dispara la latencia. Repartirlos cuesta una línea (medido por el backend).
        while (!leaving)
        {
            var wait = new WaitForSeconds(pollSeconds + Random.Range(-0.5f, 0.5f));
            if (!polling && !paused) Poll();
            yield return wait;
        }
    }

    void Poll()
    {
        polling = true;

        ApiManager.Instance.GetClassState(ClassContext.ClassId, lastVersion,
            onSuccess: state =>
            {
                polling = false;
                SetSync(true);

                if (state == null) return;

                // ORDEN DE LECTURA: con changed == false el resto del cuerpo es relleno.
                // Leerlo aquí borraría la escena en cada sondeo.
                if (!state.changed) return;

                lastVersion = state.version;
                Apply(state);
            },
            onError: (code, detail) =>
            {
                polling = false;

                // Sin conexión se conserva en pantalla la última escena recibida y se
                // avisa que no está sincronizada: borrarla no ayudaría a nadie.
                if (code == 0 || code >= 500 || detail == "ERR_RATE_LIMITED")
                {
                    SetSync(false);
                    return;
                }

                if (code == 403 || code == 404) BackToWaiting();
            });
    }

    void Apply(ClassStateResponse state)
    {
        // El docente cerró la clase: se vuelve a la sala de espera, que ya sabe mostrar
        // el aviso de que terminó.
        if (state.status != "running") { BackToWaiting(); return; }

        lastMolecules = state.molecules;
        sceneRenderer.Render(state.molecules);
        sceneRenderer.ApplyHighlights(state.highlights);

        bool empty = state.molecules == null || state.molecules.Length == 0;
        if (emptyHint) emptyHint.SetActive(empty);

        ApplyCamera(state.camera);
    }

    void ApplyCamera(CameraDTO camera)
    {
        if (camera == null) return;

        docYaw       = camera.yaw;
        docPitch     = camera.pitch;
        docDistance  = camera.distance;
        cameraLocked = camera.locked;

        if (followingBadge) followingBadge.SetActive(cameraLocked);
        // Con la vista fija no tiene sentido ofrecer "volver a ella": ya estás.
        if (btnVolverVista) btnVolverVista.gameObject.SetActive(!cameraLocked);

        if (cameraLocked) SnapToTeacherView();
    }

    void SnapToTeacherView()
    {
        if (cam) cam.SetView(docYaw, docPitch, docDistance);
    }

    // ── Copiar a un universo ───────────────────────────────────────────────────

    /// <summary>Se lleva la escena tal como está a un universo NUEVO del alumno. La copia
    /// es independiente: si el docente cambia la pizarra después, esto no cambia.
    ///
    /// Reutiliza el camino de CrearUniversoScene (UniverseStore + el contador del diario),
    /// así el universo copiado es idéntico a cualquier otro.</summary>
    void CopyToUniverse()
    {
        if (lastMolecules == null || lastMolecules.Length == 0)
        {
            ShowNotice("Todavía no hay nada que copiar.");
            return;
        }

        var universe = UniverseData.New(NextUniverseName(), 0, 0);

        // Primera pasada: posiciones en ångströms, para saber cuánto hay que elevar.
        var positions = new List<Vector3>();
        foreach (var m in lastMolecules)
        {
            if (m?.atoms == null) continue;
            Vector3 offset = m.offset != null ? m.offset.ToVector3() : Vector3.zero;
            float   mScale = m.scale > 0f ? m.scale : 1f;

            foreach (var a in m.atoms)
            {
                Vector3 local = a?.position != null ? a.position.ToVector3() : Vector3.zero;
                positions.Add(((local - Vector3.one * 0.5f) * mScale + offset) * copyScale);
            }
        }

        // Las posiciones vienen centradas en 0, así que la mitad de la molécula queda con
        // Y negativa: sin elevarla, el alumno abriría su universo con los átomos enterrados.
        float minY = float.MaxValue;
        foreach (var p in positions) minY = Mathf.Min(minY, p.y);
        float lift = copyFloorY - minY;

        int index = 0;
        foreach (var m in lastMolecules)
        {
            if (m?.atoms == null) continue;
            int baseId = index;

            foreach (var a in m.atoms)
            {
                var p = positions[index];
                universe.atoms.Add(new AtomSave
                {
                    element = a != null ? a.type : "",
                    x = p.x, y = p.y + lift, z = p.z,
                });
                index++;
            }

            if (m.bonds == null) continue;
            foreach (var b in m.bonds)
            {
                if (b == null) continue;
                if (b.beginAtomId < 0 || b.beginAtomId >= m.atoms.Length) continue;
                if (b.endAtomId   < 0 || b.endAtomId   >= m.atoms.Length) continue;
                universe.bonds.Add(new BondSave
                {
                    a = baseId + b.beginAtomId,
                    b = baseId + b.endAtomId,
                    order = b.order,
                });
            }
        }

        if (!UniverseStore.Add(universe))
        {
            ShowNotice("No se pudo guardar el universo.");
            return;
        }

        // El contador del diario, igual que al crear un universo desde su pantalla.
        if (!string.IsNullOrEmpty(SessionData.UserId))
            ApiManager.Instance.AddCreatedUniverse(SessionData.UserId, _ => { }, (_, __) => { });

        ShowNotice($"Copiado a \"{universe.name}\". Lo tienes en Mis universos.");
    }

    static string NextUniverseName()
    {
        var col = UniverseStore.Load();
        int n = (col != null && col.universes != null) ? col.universes.Count : 0;
        return "Universo " + (n + 1);
    }

    // ── Navegación ─────────────────────────────────────────────────────────────
    void BackToWaiting()
    {
        leaving = true;
        SceneManager.LoadScene(escenaEspera);
    }

    void Leave()
    {
        leaving = true;
        ClassContext.Clear();
        SceneManager.LoadScene(escenaClases);
    }

    void ShowNotice(string msg)
    {
        if (!noticeRoot || !noticeText) { Debug.Log("[Clase] " + msg); return; }
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

    void SetSync(bool ok)
    {
        if (!syncLabel) return;
        syncLabel.text  = ok ? "Sincronizado" : "Sin conexión. Reintentando…";
        syncLabel.color = ok ? new Color(0.30f, 0.80f, 0.44f) : new Color(0.96f, 0.65f, 0.14f);
    }
}
