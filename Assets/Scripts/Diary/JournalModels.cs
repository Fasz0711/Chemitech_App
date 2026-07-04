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
}

[Serializable]
public class JournalAtom
{
    public string      type;      // símbolo del elemento: "O", "C", "H"...
    public JournalVec3 position;   // x,y,z normalizados
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
    public int beginAtomId;
    public int endAtomId;
    public int order;             // 1 simple, 2 doble, 3 triple
}

/// <summary>Envoltorio para parsear el arreglo de nivel raíz con JsonUtility.</summary>
[Serializable]
public class JournalListWrapper
{
    public JournalEntry[] items;
}
