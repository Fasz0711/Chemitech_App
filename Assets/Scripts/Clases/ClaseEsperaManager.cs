using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Sala de espera del alumno. Sondea el estado de la clase y entra sola cuando el
/// docente la inicia.
///
/// NO muestra nada de la lección, y no es una decisión estética: si el alumno pudiera
/// ver el contenido antes del taller, el grupo con app tendría exposición previa y la
/// comparación con el otro grupo se caería. El servidor tampoco lo entrega mientras la
/// sesión no esté iniciada, así que son dos cerrojos.
/// </summary>
public class ClaseEsperaManager : MonoBehaviour
{
    [Header("Textos")]
    [SerializeField] private TextMeshProUGUI className;
    [SerializeField] private TextMeshProUGUI message;
    [SerializeField] private TextMeshProUGUI connectionLabel;

    [Header("Botones")]
    [SerializeField] private Button btnSalir;

    [Header("Ajustes")]
    [Tooltip("Segundos entre sondeos. El límite del servidor son 60 por minuto.")]
    [SerializeField] private float pollSeconds = 2.5f;

    [Header("Escenas")]
    [SerializeField] private string escenaClases = "MisClasesScene";
    [SerializeField] private string escenaClase  = "ClaseEstudianteScene";  // llega en la Fase 2

    // -1 pide el estado completo: el primer sondeo siempre trae changed = true.
    int  lastVersion = -1;
    bool polling;      // una sola petición en vuelo; si no, con la red lenta se encolan
    bool leaving;

    const string MSG_WAITING = "Espera a que tu docente inicie la clase.";
    const string MSG_ENDED   = "La clase terminó.";
    const string MSG_STARTED = "¡La clase empezó!";

    void Start()
    {
        if (btnSalir) btnSalir.onClick.AddListener(Leave);

        if (className) className.text = ClassContext.HasClass ? ClassContext.ClassName : "Clase";
        if (message)   message.text   = MSG_WAITING;
        SetConnection(true);

        if (!ClassContext.HasClass)
        {
            // Sin clase en el contexto no hay nada que sondear: se entró por error.
            if (message) message.text = "No se pudo abrir la clase.";
            return;
        }

        StartCoroutine(PollLoop());
    }

    IEnumerator PollLoop()
    {
        // Jitter: sin él los 25 celulares sondean en el mismo tic y esa ráfaga es la
        // que dispara la latencia. Repartirlos cuesta una línea (medido por el backend).
        while (!leaving)
        {
            var wait = new WaitForSeconds(pollSeconds + Random.Range(-0.5f, 0.5f));
            if (!polling) Poll();
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
                SetConnection(true);

                if (state == null) return;

                // ORDEN DE LECTURA del contrato: con changed == false el resto del
                // cuerpo es relleno, así que leer 'status' ahí daría un valor falso.
                if (!state.changed) return;

                lastVersion = state.version;
                ApplyStatus(state.status);
            },
            onError: (code, detail) =>
            {
                polling = false;

                // Sin conexión no hay nada que hacer salvo avisar y seguir reintentando:
                // el bucle ya vuelve a sondear solo.
                if (code == 0 || code >= 500 || detail == "ERR_RATE_LIMITED")
                {
                    SetConnection(false);
                    return;
                }

                // 401 = la cuenta desapareció con la clase borrada. ApiManager ya manda
                // al login; esto solo deja dicho por qué.
                if (code == 401) ClassContext.ExitNotice = "La clase terminó.";

                // Permiso o clase inexistente: no tiene sentido insistir.
                if (code == 403 || code == 404)
                {
                    leaving = true;
                    if (message) message.text = MapError(detail);
                }
            });
    }

    void ApplyStatus(string status)
    {
        switch (status)
        {
            case "running":
                EnterClass();
                break;

            case "ended":
                leaving = true;
                if (message) message.text = MSG_ENDED;
                break;

            default: // "waiting"
                if (message) message.text = MSG_WAITING;
                break;
        }
    }

    void EnterClass()
    {
        // La escena de la clase llega en la Fase 2. Hasta entonces nos quedamos aquí en
        // vez de saltar a una escena que no está en Build Settings (sería un error duro).
        if (!string.IsNullOrEmpty(escenaClase) && Application.CanStreamedLevelBeLoaded(escenaClase))
        {
            leaving = true;   // nos vamos: ya no hace falta seguir sondeando
            SceneManager.LoadScene(escenaClase);
            return;
        }

        // OJO: aquí NO se detiene el sondeo. Si se detuviera, el alumno se quedaría
        // mirando "la clase empezó" para siempre y no se enteraría de que el docente
        // la terminó. El bucle sigue hasta que llegue 'ended'.
        if (message) message.text = MSG_STARTED;
    }

    void Leave()
    {
        leaving = true;
        ClassContext.Clear();
        SceneManager.LoadScene(escenaClases);
    }

    void SetConnection(bool ok)
    {
        if (!connectionLabel) return;
        connectionLabel.text  = ok ? "Conectado" : "Sin conexión. Reintentando…";
        connectionLabel.color = ok ? new Color(0.30f, 0.80f, 0.44f) : new Color(0.96f, 0.65f, 0.14f);
    }

    static string MapError(string detail)
    {
        switch (detail)
        {
            case "ERR_NOT_CLASS_MEMBER": return "No estás en esta clase.";
            case "ERR_CLASS_NOT_FOUND":  return "Esta clase ya no existe.";
            default:                     return "No se pudo abrir la clase.";
        }
    }
}
