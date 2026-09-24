using System;

/// <summary>
/// Formas de las respuestas del modo clase. Calcadas de docs/CONTRATO_CLASES_FASE1.txt,
/// que a su vez está volcado de los modelos reales del backend.
///
/// Reglas del contrato que hacen que JsonUtility pueda leerlas sin comprobaciones:
/// ningún campo llega null y ninguno falta. Los nombres tienen que coincidir EXACTO:
/// si uno no coincide, JsonUtility lo deja en su valor por defecto sin avisar.
/// </summary>

/// <summary>Una clase. La clave en el JSON es "classroom", no "class": esa última es
/// palabra reservada en C# y no se puede declarar un campo con ese nombre.</summary>
[Serializable]
public class ClassroomDTO
{
    public string publicId;      // UUID; es el {id} de todas las URLs
    public string name;
    public string section;       // en MAYÚSCULAS, como la guarda el backend
    public string status;        // "waiting" | "running" | "ended"
    public int    studentCount;

    // Solo vienen con contenido en /classes/mine:
    public string teacherName;   // para la tarjeta del alumno
    public string code;          // el código del alumno; vacío ("") si eres docente
}

/// <summary>Credenciales de un alumno. Las contraseñas SOLO se ven una vez, cuando se
/// crean: el backend guarda el hash y no puede volver a mostrarlas.</summary>
[Serializable]
public class StudentCredentialDTO
{
    public string code;          // "3A_07"
    public string password;      // "Luna-4827"
}

/// <summary>POST /classes y POST /classes/{id}/students comparten esta forma.
/// En el segundo, "students" trae SOLO las cuentas nuevas.</summary>
[Serializable]
public class ClassCreatedResponse
{
    public string                message;   // OK_CLASS_CREATED | OK_STUDENTS_CREATED
    public ClassroomDTO          classroom;
    public StudentCredentialDTO[] students;
}

/// <summary>POST /classes/{id}/students/{code}/password</summary>
[Serializable]
public class PasswordReplacedResponse
{
    public string               message;   // OK_PASSWORD_REPLACED
    public StudentCredentialDTO student;
}

/// <summary>POST /classes/{id}/start y /stop. Repetirlos no es error: responden 200
/// y la versión no sube.</summary>
[Serializable]
public class ClassStatusResponse
{
    public string message;   // OK_CLASS_STARTED | OK_CLASS_STOPPED
    public string status;    // "waiting" | "running" | "ended"
    public int    version;
}

/// <summary>GET /classes/mine. UNA sola forma para los dos roles: una respuesta distinta
/// por rol sería polimorfismo y JsonUtility no lo soporta.</summary>
[Serializable]
public class MyClassesResponse
{
    public string         message;   // OK_CLASSES_FOUND
    public string         role;      // "teacher" | "student"
    public ClassroomDTO[] classes;   // vacío si la cuenta no tiene clases
}

/// <summary>GET /classes/{id}/state. En la Fase 1 solo se leen estos cuatro campos:
/// el contenido de la escena (molecules, overlays, camera…) llega en la Fase 2 y se
/// añadirá aquí entonces. JsonUtility ignora sin quejarse los campos que no declaramos.
///
/// ORDEN DE LECTURA obligatorio: primero 'changed', luego 'status'. Si se lee el
/// contenido sin mirar 'changed', una respuesta "sin cambios" borra la escena.</summary>
[Serializable]
public class ClassStateResponse
{
    public string message;   // OK_STATE_FOUND | OK_STATE_UNCHANGED
    public bool   changed;
    public int    version;
    public string status;    // "waiting" | "running" | "ended"

    // ── Contenido de la escena (vacío mientras la clase no esté "running") ──
    public SceneMoleculeDTO[] molecules;
    public AttractionDTO[]    attractions;
    public HighlightDTO[]     highlights;
    public OverlaysDTO        overlays;
    public AnnotationDTO[]    annotations;
    public CameraDTO          camera;

    // Objetos opcionales: van siempre presentes y se preguntan por su booleano.
    // Nunca comprobar "!= null": JsonUtility jamás deja null un objeto, crea uno
    // vacío, así que esa comprobación sería siempre cierta.
    public bool           hasInteraction;
    public InteractionDTO interaction;
    public bool           hasPrediction;
    public PredictionDTO  prediction;
    public bool           hasReveal;
    public RevealDTO      reveal;
}
