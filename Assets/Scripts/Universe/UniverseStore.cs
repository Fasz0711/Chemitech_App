using System.IO;
using UnityEngine;

/// <summary>
/// Persistencia local de universos en un archivo JSON dentro de
/// Application.persistentDataPath/universes.json
/// </summary>
public static class UniverseStore
{
    static string FilePath => Path.Combine(Application.persistentDataPath, "universes.json");

    public static UniverseCollection Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new UniverseCollection();
            string json = File.ReadAllText(FilePath);
            var col = JsonUtility.FromJson<UniverseCollection>(json);
            return col ?? new UniverseCollection();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[UniverseStore] No se pudo cargar: {e.Message}");
            return new UniverseCollection();
        }
    }

    public static void Save(UniverseCollection col)
    {
        try
        {
            string json = JsonUtility.ToJson(col, true);
            File.WriteAllText(FilePath, json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[UniverseStore] No se pudo guardar: {e.Message}");
        }
    }

    public static void Add(UniverseData universe)
    {
        var col = Load();
        col.universes.Add(universe);
        Save(col);
    }
}
