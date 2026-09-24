using System.Collections;
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
    [SerializeField] private GameObject      emptyHint;        // "El docente aún no ha puesto nada"

    [Header("Render")]
    [SerializeField] private float worldScale    = 6f;
    [SerializeField] private float atomSize      = 0.9f;
    [SerializeField] private float bondThickness = 0.09f;
    [SerializeField] private float bondSpacing   = 0.20f;

    [Header("Sondeo")]
    [Tooltip("Segundos entre sondeos. El límite del servidor son 60 por minuto.")]
    [SerializeField] private float pollSeconds = 3f;

    [Header("Escenas")]
    [SerializeField] private string escenaEspera = "ClaseEsperaScene";
    [SerializeField] private string escenaClases = "MisClasesScene";

    ClassSceneRenderer sceneRenderer;

    int  lastVersion = -1;
    bool polling;
    bool leaving;
    bool paused;          // app en segundo plano: se deja de sondear

    // Última vista que mandó el docente, para el botón "volver a su vista".
    float docYaw = 35f, docPitch = 28f, docDistance = 16f;
    bool  cameraLocked;

    void Start()
    {
        if (btnSalir)        btnSalir.onClick.AddListener(Leave);
        if (btnVolverVista)  btnVolverVista.onClick.AddListener(SnapToTeacherView);

        if (className) className.text = ClassContext.HasClass ? ClassContext.ClassName : "Clase";
        SetSync(true);
        if (followingBadge) followingBadge.SetActive(false);

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

        sceneRenderer.Render(state.molecules);

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

    void SetSync(bool ok)
    {
        if (!syncLabel) return;
        syncLabel.text  = ok ? "Sincronizado" : "Sin conexión. Reintentando…";
        syncLabel.color = ok ? new Color(0.30f, 0.80f, 0.44f) : new Color(0.96f, 0.65f, 0.14f);
    }
}
