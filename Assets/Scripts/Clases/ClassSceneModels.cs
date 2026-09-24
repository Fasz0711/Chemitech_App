using System;

/// <summary>
/// Contenido de la escena de clase (Fase 2 en adelante). Separado de ClassModels.cs
/// para no mezclar el contrato de administración (crear clase, listar) con el de la
/// escena, que es lo que viaja en cada sondeo.
///
/// Se declara el contrato COMPLETO aunque la Fase 2 solo dibuje parte: los nombres
/// tienen que coincidir exacto con el JSON y un campo mal escrito se lee como valor
/// por defecto sin ningún aviso. Tenerlos todos desde ahora evita descubrirlo tarde
/// (ya pasó con structure2D, que lleva D mayúscula).
///
/// Ningún campo llega null: los objetos opcionales vienen siempre presentes y se
/// acompañan de un booleano hasX. Los "no aplica" son centinelas: -1 en los índices,
/// "" en los ids.
/// </summary>

[Serializable]
public class Vec3DTO
{
    public float x, y, z;

    public UnityEngine.Vector3 ToVector3() => new UnityEngine.Vector3(x, y, z);
}

[Serializable]
public class SceneAtomDTO
{
    public string  type;      // elemento ("O", "H"…), como en /by-smiles
    public Vec3DTO position;  // normalizada a [0,1] dentro de su molécula
    public float   en;        // electronegatividad de Pauling
    public int     charge;    // carga formal; 0 es un dato real, no un "no aplica"
}

[Serializable]
public class SceneBondDTO
{
    public int    beginAtomId;
    public int    endAtomId;
    public int    order;        // 1, 2 o 3
    public string kind;         // "nonpolar" | "polar" | "ionic"
    public int    negativeEnd;  // índice del átomo con delta-; -1 si no es polar
}

[Serializable]
public class SceneMoleculeDTO
{
    public string id;              // "m1", "m2"… lo asigna el servidor y es estable
    public string name;
    public string formula;         // SIEMPRE en ASCII ("H2O", no "H₂O")
    public string canonicalSmiles;

    public Vec3DTO offset;         // dónde se ubica la molécula en la escena
    public float   scale;          // tamaño real (Å) con que se normalizó; conserva
                                   // la proporción entre moléculas distintas

    public SceneAtomDTO[] atoms;
    public SceneBondDTO[] bonds;
}

/// <summary>Atracción entre átomos de moléculas distintas (puente de hidrógeno).
/// Es ESTADO, no animación: quien entra tarde tiene que verla sin haber visto el paso
/// que la creó.</summary>
[Serializable]
public class AttractionDTO
{
    public string fromMoleculeId;
    public int    fromAtomId;
    public string toMoleculeId;
    public int    toAtomId;
}

[Serializable]
public class HighlightDTO
{
    public string moleculeId;
    public int[]  atomIds;
    public int[]  bondIds;
}

[Serializable]
public class OverlaysDTO
{
    public bool labels;
    public bool electronegativity;
    public bool bondTypes;
    public bool structure2D;   // OJO: D MAYÚSCULA, así viene en el JSON
}

[Serializable]
public class AnnotationDTO
{
    public string text;
    public string moleculeId;  // "" = anclada a la escena entera
    public int    atomId;      // -1 = anclada a la molécula entera
}

[Serializable]
public class CameraDTO
{
    public bool  locked;       // true = todos siguen la vista del docente
    public float yaw, pitch, distance;

    // A qué mira la cámara cuando el docente enfoca algo. Se ignoran en la Fase 2,
    // pero se declaran para que el contrato no cambie al usarlos en la Fase 3.
    public string focusMoleculeId;   // "" = sin foco
    public int    focusAtomId;       // -1 = sin foco
}

// ── Interacciones y predicción: llegan en la Fase 5, pero el contrato ya es este ──

[Serializable]
public class StepPrimitiveDTO
{
    public string kind;            // "move" | "transfer" | "bond" | "attract"
    public string fromMoleculeId;
    public int    fromAtomId;      // -1 = la molécula entera
    public string toMoleculeId;
    public int    toAtomId;
    public int    order;           // solo "bond"; 0 quita el enlace
    public int    durationMs;
}

[Serializable]
public class InteractionDTO
{
    public string id;
    public int    step;
    public int    totalSteps;
    public string caption;
    public StepPrimitiveDTO[] primitives;
}

[Serializable]
public class PredictionOptionDTO
{
    public string id;
    public string text;
}

[Serializable]
public class PredictionDTO
{
    public string id;
    public string question;
    public PredictionOptionDTO[] options;
}

[Serializable]
public class RevealDTO
{
    public string predictionId;
    public string correctOptionId;
}

// ── Roster (panel del docente) ───────────────────────────────────────────────

[Serializable]
public class RosterStudentDTO
{
    public string code;
    public bool   online;
    public int    lastSeenSeconds;
}

[Serializable]
public class RosterResponse
{
    public string message;
    public int    connected;
    public int    total;
    public RosterStudentDTO[] students;
}
