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
