using System;
using System.Collections.Generic;

/// <summary>Un átomo guardado del estado de un universo (elemento + posición 3D).</summary>
[Serializable]
public class AtomSave
{
    public string element;
    public float  x, y, z;
}

/// <summary>Un enlace guardado: a/b son índices en la lista de atoms; order = 1/2/3.</summary>
[Serializable]
public class BondSave
{
    public int a, b, order;
}

[Serializable]
public class UniverseData
{
    public string id;
    public string name;
    public int    iconIndex;
    public int    colorIndex;
    public long   createdAtTicks; // DateTime.UtcNow.Ticks

    // Estado de juego guardado
    public long           playSeconds;                 // tiempo acumulado jugado
    public List<AtomSave> atoms = new List<AtomSave>(); // átomos colocados
    public List<BondSave> bonds = new List<BondSave>(); // enlaces detectados (para no re-descubrir)

    public static UniverseData New(string name, int iconIndex, int colorIndex)
    {
        return new UniverseData
        {
            id             = Guid.NewGuid().ToString(),
            name           = name,
            iconIndex      = iconIndex,
            colorIndex     = colorIndex,
            createdAtTicks = DateTime.UtcNow.Ticks,
        };
    }
}

[Serializable]
public class UniverseCollection
{
    public List<UniverseData> universes = new List<UniverseData>();
}
