using System;
using System.Collections.Generic;

[Serializable]
public class UniverseData
{
    public string id;
    public string name;
    public int    iconIndex;
    public int    colorIndex;
    public long   createdAtTicks; // DateTime.UtcNow.Ticks

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
