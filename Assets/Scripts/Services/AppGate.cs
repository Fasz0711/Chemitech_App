using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// EL CANDADO PREVIO AL TALLER.
///
/// El APK se reparte días antes para que nadie pierda tiempo instalando el día de la
/// sesión. Ese reparto abre un agujero en la validación: un alumno que explora la app
/// antes llega al pretest sabiendo lo que el pretest mide, y su grupo deja de ser
/// comparable con el otro. El candado cierra ese agujero: la app se instala, se abre,
/// agradece y no deja pasar hasta que el docente levanta el interruptor en el servidor.
///
/// LAS CUATRO REGLAS, que son el diseño entero:
///
///   1. SOLO UN "false" EXPLÍCITO BLOQUEA. Un 404, una clave ausente o un JSON que no
///      se entiende abren la app. Si el candado se equivoca, que se equivoque dejando
///      pasar: una app bloqueada por una errata del contrato es el taller perdido, y
///      JsonUtility devuelve false —o sea BLOQUEADO— ante cualquier nombre que no
///      coincida, sin un solo error.
///
///   2. SIN RESPUESTA = COMO ESTABA. Una instalación nueva nunca ha oído un "sí", así
///      que arranca cerrada: sin esto, el modo avión se salta el candado, y muchos
///      alumnos no tienen datos en casa. Un aparato que ya se abrió alguna vez arranca
///      abierto: sin esto, un corte de red del colegio deja 25 teléfonos muertos a
///      mitad de clase.
///
///   3. NUNCA SE VUELVE A CERRAR EN MARCHA. Una vez que el servidor dijo que sí en esta
///      ejecución, ya no se cierra aunque deje de responder o cambie de idea. Un
///      interruptor tocado por error a mitad de la lección no puede apagar el aula.
///
///   4. SE ABRE SOLO. Mientras está cerrada, vuelve a preguntar cada pocos segundos, así
///      que cuando el docente levanta el interruptor los teléfonos se abren sin que
///      nadie cierre ni reinstale nada. Sin esto, el minuto 0 del taller son 25 alumnos
///      forzando el cierre de la app.
///
/// NO ES UNA CAJA FUERTE, ES UNA CORTINA. Frena la exploración casual, que es la
/// amenaza real. Un aparato con root o un APK modificado se lo saltan, y da igual: ese
/// no es el alumno que contamina la muestra.
/// </summary>
public class AppGate : MonoBehaviour
{
    public static AppGate Instance { get; private set; }

    /// <summary>Si la app sigue cerrada. Lo consulta la pantalla de login para no
    /// ofrecer caminos que no llevan a ninguna parte mientras dura el candado.</summary>
    public static bool IsClosed => Instance && Instance.locked && !Instance.unlockedThisRun;

    /// <summary>Si el aparato ya vio un "sí" alguna vez. Es del APARATO, no de la cuenta:
    /// el candado no distingue usuarios, distingue si la clase ya empezó.</summary>
    public const string PREF_OPEN = "chemitech_gate_open";

    // Solo en el Editor (ver CandadoTool): finge un servidor que dice que no, para poder
    // ver y ensayar esta pantalla sin depender del backend.
    public const string PREF_FORCE_LOCK = "chemitech_gate_force_lock";

    const float POLL_SECONDS = 10f;

    // Cuántas veces se insiste cuando el aparato arrancó ABIERTO y el servidor no
    // contesta. Se insiste poco: ya está abierto, y sondear para siempre en segundo
    // plano gasta batería durante la clase sin decidir nada.
    const int MAX_SILENT_TRIES = 3;

    const string TITLE       = "¡Gracias por descargar ChemiTech!";
    const string BODY        = "Te esperamos para la clase.";
    const string BTN_LOGIN   = "Ya tengo mi código";
    const string OFFLINE     = "Sin conexión. Reintentando...";

    static readonly Color BG      = new Color(0.039f, 0.055f, 0.153f);   // #0A0E27
    static readonly Color CYAN    = new Color(0.098f, 0.655f, 0.808f);   // #19A7CE
    static readonly Color SOFT    = new Color(0.749f, 0.914f, 0.949f);   // #BFE9F2
    static readonly Color FAINT   = new Color(0.42f,  0.48f,  0.63f);

    GameObject       root;
    TextMeshProUGUI  bodyLabel, extraLabel, statusLabel;
    GameObject       loginButton;

    bool locked;
    bool unlockedThisRun;   // el servidor dijo que sí EN ESTA EJECUCIÓN (regla 3)
    bool bypassed;          // el usuario fue a identificarse; no se le interrumpe
    bool paused;            // app en segundo plano: no se sondea
    bool waiting;           // una sola petición en vuelo
    int  silentTries;
    string serverMessage = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        new GameObject("AppGate").AddComponent<AppGate>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // El docente no pasa por el candado. Su rol viaja en la sesión guardada, así que
        // esto vale también sin red, que es justo cuando hace falta: el docente que llega
        // al colegio y el WiFi todavía no lo deja salir no puede quedarse fuera de su
        // propia clase.
        if (SessionData.IsTeacher) { unlockedThisRun = true; return; }

        bool startsOpen = PlayerPrefs.GetInt(PREF_OPEN, 0) == 1;
        locked = !startsOpen;

        BuildScreen();
        Refresh();

        SceneManager.activeSceneChanged += OnSceneChanged;
        StartCoroutine(Loop());
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.activeSceneChanged -= OnSceneChanged;
    }

    void OnApplicationPause(bool isPaused) => paused = isPaused;

    // ── El sondeo ─────────────────────────────────────────────────────────────

    IEnumerator Loop()
    {
        // Un cuadro de cortesía: deja que ApiManager se cree con la escena ya cargando.
        yield return null;

        var wait = new WaitForSeconds(POLL_SECONDS);
        while (!unlockedThisRun)
        {
            if (!paused && !waiting) Ask();
            yield return wait;
        }
    }

    void Ask()
    {
#if UNITY_EDITOR
        if (PlayerPrefs.GetInt(PREF_FORCE_LOCK, 0) == 1)
        {
            serverMessage = "";
            locked = true;
            Refresh();
            return;
        }
#endif
        waiting = true;

        ApiManager.Instance.GetAppStatus(
            resp =>
            {
                waiting = false;
                silentTries = 0;

                // Regla 1: solo un "false" de verdad cierra. Si el cuerpo no traía la
                // clave, este servidor no sabe del candado y no es quién para cerrarlo.
                bool open = resp == null || !resp.answered || resp.canBeUsed;
                serverMessage = resp != null ? resp.message : "";

                if (open) Open("el servidor lo permite");
                else      Close();
            },
            (code, detail) =>
            {
                waiting = false;

                // 404: el candado todavía no existe en el servidor. No es un fallo, es
                // que no hay nada que cerrar.
                if (code == 404) { Open("el servidor no tiene candado"); return; }

                // Cualquier otro tropiezo (sin red, 500, el servidor dormido) deja las
                // cosas como estaban. Regla 2.
                Debug.Log($"[Candado] Sin respuesta (code={code}, {detail}). Se mantiene " +
                          (locked ? "cerrado." : "abierto."));
                if (!locked && ++silentTries >= MAX_SILENT_TRIES) unlockedThisRun = true;
                Refresh();
            });
    }

    void Open(string why)
    {
        if (unlockedThisRun) return;
        unlockedThisRun = true;
        locked = false;
        PlayerPrefs.SetInt(PREF_OPEN, 1);
        PlayerPrefs.Save();
        Debug.Log($"[Candado] Abierto: {why}.");
        Refresh();
    }

    void Close()
    {
        if (unlockedThisRun) return;   // regla 3
        locked = true;
        Refresh();
    }

    // ── La puerta del que sí puede entrar ─────────────────────────────────────

    /// <summary>"Ya tengo mi código" lleva al login y baja el candado MIENTRAS DURE ese
    /// camino. Hace dos trabajos con un solo botón:
    ///
    ///   - es por donde entra el docente, sin códigos secretos escondidos en el APK: se
    ///     identifica con su cuenta y el servidor lo reconoce;
    ///   - deja que el alumno compruebe su contraseña el día ANTES y vuelva al candado.
    ///     Eso quita del minuto 0 del taller el peor riesgo que tiene: 25 personas
    ///     descubriendo a la vez que alguien apuntó mal su clave.</summary>
    void GoToLogin()
    {
        bypassed = true;
        Refresh();
        SceneManager.LoadScene("LoginScene");
    }

    /// <summary>El candado vuelve en cuanto se sale del camino de identificarse. Sin
    /// esto, el botón sería el agujero: tocarlo, retroceder y explorar la app entera.</summary>
    void OnSceneChanged(Scene from, Scene to)
    {
        if (unlockedThisRun || !bypassed) return;
        if (IsAuthScene(to.name)) return;

        bypassed = false;

        if (SessionData.IsTeacher) { Open("entró un docente"); return; }

        Refresh();
        if (!waiting) Ask();   // por si el servidor ya lo reconoce con su token
    }

    static bool IsAuthScene(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        // El prompt de invitado NO entra aquí a propósito, aunque se le parezca: los
        // demás son pasos para identificarse y acaban donde empezaron, pero ese es una
        // puerta para entrar a jugar SIN cuenta, que es justo lo que el candado frena.
        return name.StartsWith("Login")
            || name.StartsWith("Register")
            || name.StartsWith("ForgotPassword")
            || name.StartsWith("CuentaCreada");
    }

    // ── La pantalla ───────────────────────────────────────────────────────────

    void Refresh()
    {
        if (!root) return;

        bool show = locked && !bypassed && !unlockedThisRun;
        if (root.activeSelf != show) root.SetActive(show);
        if (!show) return;

        if (bodyLabel)
            bodyLabel.text = string.IsNullOrEmpty(serverMessage) ? BODY : serverMessage;

        // Un alumno ya identificado no tiene nada más que hacer, y conviene decírselo:
        // si no, vuelve a intentar entrar pensando que se equivocó de contraseña.
        bool student = SessionData.IsLoggedIn && !SessionData.IsTeacher;
        if (extraLabel)
        {
            extraLabel.gameObject.SetActive(student);
            if (student)
                extraLabel.text = string.IsNullOrEmpty(SessionData.Username)
                    ? "Tu código funciona. No tienes que hacer nada más."
                    : $"Listo, {SessionData.Username}. Tu código funciona y no tienes que hacer nada más.";
        }

        if (loginButton) loginButton.SetActive(!student);
        if (statusLabel) statusLabel.text = waiting ? "" : (silentTries > 0 ? OFFLINE : "");
    }

    void BuildScreen()
    {
        root = new GameObject("CandadoCanvas");
        root.transform.SetParent(transform, false);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31000;   // por encima del velo de brillo (30000) y de todo

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight  = 0.5f;

        // Con raycaster, y a propósito: esta capa tiene que TRAGARSE los toques, o el
        // alumno estaría tocando los botones de la app por detrás de la cortina.
        root.AddComponent<GraphicRaycaster>();

        var bg = MakeRect("Fondo", root.transform, Vector2.zero, Vector2.zero);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = BG;
        bgImg.raycastTarget = true;

        MakeText("Titulo", bg.transform, TITLE, new Vector2(0f, 220f), new Vector2(900f, 220f),
                 64f, Color.white, FontStyles.Bold);

        bodyLabel = MakeText("Cuerpo", bg.transform, BODY, new Vector2(0f, 30f),
                             new Vector2(880f, 160f), 46f, SOFT, FontStyles.Normal);

        extraLabel = MakeText("Extra", bg.transform, "", new Vector2(0f, -110f),
                              new Vector2(880f, 130f), 34f, FAINT, FontStyles.Normal);

        loginButton = MakeButton("BtnLogin", bg.transform, BTN_LOGIN,
                                 new Vector2(0f, -300f), new Vector2(560f, 110f), GoToLogin);

        statusLabel = MakeText("Estado", bg.transform, "", new Vector2(0f, -470f),
                               new Vector2(880f, 60f), 28f, FAINT, FontStyles.Normal);
    }

    // ── Fábrica de trozos de interfaz ─────────────────────────────────────────
    //
    // Se construye por código y no con una escena ni un prefab porque el candado tiene
    // que existir en LAS 19 ESCENAS, incluidas las que se cargan después. Montarlo a
    // mano en cada una serían 19 oportunidades de olvidarse de una, y la que se olvide
    // es el agujero por donde se cuela el alumno.

    static GameObject MakeRect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return go;
    }

    static TextMeshProUGUI MakeText(string name, Transform parent, string text, Vector2 pos,
                                    Vector2 size, float fontSize, Color color, FontStyles style)
    {
        var go = MakeRect(name, parent, pos, size);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.font      = Font();
        tmp.fontSize  = fontSize;
        tmp.fontStyle = style;
        tmp.color     = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;
        return tmp;
    }

    static GameObject MakeButton(string name, Transform parent, string label, Vector2 pos,
                                 Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        var go = MakeRect(name, parent, pos, size);
        var img = go.AddComponent<Image>();
        img.color = CYAN;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var lbl = MakeText("Label", go.transform, label, Vector2.zero, size, 40f,
                           new Color(0.04f, 0.18f, 0.27f), FontStyles.Bold);
        lbl.enableWordWrapping = false;
        return go;
    }

    /// <summary>La fuente de la app no vive en Resources, así que en tiempo de ejecución
    /// no se puede pedir por nombre; se usa la de TextMeshPro, que siempre está y tiene
    /// todos los caracteres. Si algún día se copia "Fredoka-Medium SDF" a una carpeta
    /// Resources, esta pantalla la toma sola y sin tocar código.</summary>
    static TMP_FontAsset Font()
    {
        var f = Resources.Load<TMP_FontAsset>("Fredoka-Medium SDF");
        return f ? f : TMP_Settings.defaultFontAsset;
    }
}
