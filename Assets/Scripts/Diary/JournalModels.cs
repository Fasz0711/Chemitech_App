using System;

/// <summary>
/// DTOs del diario — respuesta de GET /api/journal/{userPublicId}.
/// El backend ya incluye, por cada entrada, el mismo detalle químico que
/// /detection/by-smiles dentro de "molecule", así que la pantalla se arma
/// con una sola llamada. "molecule" puede venir null en casos raros.
/// </summary>
[Serializable]
public class JournalEntry
{
    // Campos propios del diario (siempre presentes)
    public string canonicalSmiles;
    public string molecularFormula;
    public bool   isKnown;
    public int    rediscoveryCount;
    public string firstDiscoveredAt;
    public string lastDiscoveredAt;

    // Detalle químico (puede ser null / con campos en null si es exótica)
    public JournalMolecule molecule;
}

[Serializable]
public class JournalMolecule
{
    public string canonicalSmiles;
    public string molecularFormula;
    public string inchikey;
    public string name;          // null en moléculas exóticas
    public string iupacName;
    public string description;
    public string category;
    public bool   isKnown;
    public float  molarMass;
    public string polarity;
    public float  logP;
    public string aqueousSolubility;
    public float  aqueousSolubilityLogS;
    public JournalComposition[] composition;
    public JournalStructure     structure;

    /// <summary>Si esta molécula tiene un guion de animación. La inmensa mayoría NO, y
    /// eso no es un error: el modal se abre igual, con capas y tarjeta, sin el botón de
    /// reproducir.</summary>
    public bool hasAnimation;

    /// <summary>El guion. Llega PRESENTE PERO VACÍO cuando no hay, nunca null: JsonUtility
    /// instancia un objeto vacío y el cliente no podría distinguir "vacío" de "ausente".
    /// Por eso manda 'hasAnimation' y no la comprobación del objeto.
    ///
    /// OJO: SOLO VIENE POR /detection/by-smiles. El listado del diario NO la trae, a
    /// propósito: incrusta el detalle completo por entrada y cada molécula arrastraría su
    /// guion entero, multiplicando una respuesta que ya es cara. Si aparece ahí, es un
    /// error del servidor.</summary>
    public MoleculeAnimation animation;
}

/// <summary>Un guion de animación: una escenita AUTOCONTENIDA con sus propios átomos.
///
/// POR QUÉ NO REUSA moleculeId: ese identificador existe solo porque la escena de clase
/// tiene varias moléculas que el servidor debe poder nombrar ENTRE SONDEOS. Una
/// explicación se pide entera de una vez, así que dentro de ella un índice plano basta y
/// no hay nada que sincronizar.
///
/// Y POR ESO 'atoms' PUEDE SER MÁS GRANDE que la estructura de la molécula: el puente de
/// hidrógeno necesita DOS aguas. El alumno toca una y la animación trae las dos.</summary>
[Serializable]
public class MoleculeAnimation
{
    public string id;
    public string title;

    // Misma forma que JournalStructure, para que el cliente use un solo renderizador.
    public JournalAtom[] atoms;
    public JournalBond[] bonds;

    public AnimationStep[] steps;

    public bool Has => steps != null && steps.Length > 0 && atoms != null && atoms.Length > 0;
}

/// <summary>Un paso. SE APLICA SOBRE EL ANTERIOR, en orden desde el estado base: así
/// saltar al paso 3 es aplicar 1, 2 y 3 sin animar, que es lo que se hace al adelantar.</summary>
[Serializable]
public class AnimationStep
{
    public string caption;       // lo que se explica en este paso; es media explicación
    public int    durationMs;
    public AnimationPrimitive[] primitives;
}

/// <summary>Una primitiva del guion.
///
/// NO ES el StepPrimitiveDTO de la escena de clase, aunque se parezcan. Aquella es la
/// FORMA DE CABLE del estado de una escena viva, donde el destino de un 'move' es la
/// posición guardada del estado final. Esta es CONTENIDO autorado, autocontenido, y por
/// eso necesita coordenadas explícitas. Quedó escrito en el contrato para que nadie
/// intente unificarlas más adelante.
///
/// 'fromAtom' y 'toAtom' son ÍNDICES en MoleculeAnimation.atoms. Se llaman así y no
/// 'from'/'to' porque 'from' es palabra reservada en Python y el servidor tendría que
/// declararla con un alias — justo la indirección donde se cuelan los errores de nombre
/// que ya costaron una clase de depuración.</summary>
[Serializable]
public class AnimationPrimitive
{
    public string kind;          // "move" | "transfer" | "bond" | "attract"
    public int    fromAtom;
    public int    toAtom;
    public JournalVec3 toPosition;   // solo "move": a dónde va
    public int    order;             // solo "bond"; 0 lo quita
    public int    durationMs;
}

[Serializable]
public class JournalComposition
{
    public string element;
    public int    count;
}

[Serializable]
public class JournalStructure
{
    public JournalAtom[] atoms;
    public JournalBond[] bonds;

    /// <summary>Coordenadas de la fórmula estructural PLANA, en el MISMO orden que
    /// 'atoms': atoms2D[i] es el mismo átomo que atoms[i]. Esa paridad es lo que permite
    /// señalar un átomo en la tarjeta y encenderlo en el 3D.
    ///
    /// OJO CON EL NOMBRE: "atoms2D" con D MAYÚSCULA, igual que "structure2D" en las capas.
    /// JsonUtility compara los nombres EXACTAMENTE y devuelve una lista vacía si no
    /// coinciden, sin avisar de nada.
    ///
    /// LLEGA VACÍA A PROPÓSITO en la sal: el libro escribe la unidad fórmula (Na+ Cl-),
    /// que son 2 átomos contra los 27 de la red 3D, y eso rompería la paridad. Vacía
    /// significa "esta molécula no tiene tarjeta", no "falló algo".</summary>
    public JournalVec2[] atoms2D;
}

[Serializable]
public class JournalVec2
{
    public float x;
    public float y;
}

[Serializable]
public class JournalAtom
{
    public string      type;      // símbolo del elemento: "O", "C", "H"...
    public JournalVec3 position;   // x,y,z normalizados
    public float       en;         // electronegatividad de Pauling; 0 = no llegó
    public int         charge;     // carga formal: +1 en el Na, -1 en el Cl
}

[Serializable]
public class JournalVec3
{
    public float x;
    public float y;
    public float z;
}

[Serializable]
public class JournalBond
{
    public int    beginAtomId;
    public int    endAtomId;
    public int    order;          // 1 simple, 2 doble, 3 triple
    public string kind;           // "nonpolar" | "polar" | "ionic"; "" = no llegó
    public int    negativeEnd;    // ÍNDICE del átomo con δ−, o -1 si no es polar
}

/// <summary>Envoltorio para parsear el arreglo de nivel raíz con JsonUtility.</summary>
[Serializable]
public class JournalListWrapper
{
    public JournalEntry[] items;
}
