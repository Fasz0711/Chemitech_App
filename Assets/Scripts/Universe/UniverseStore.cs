using System.IO;
using UnityEngine;

/// <summary>
/// Persistencia local de universos, separada por cuenta (por userId).
///
///  • Con userId: archivo JSON por cuenta en
///    Application.persistentDataPath/universes_{userId}.json
///    El userId viene del response de LOGIN (uso normal) o del response de
///    CREAR CUENTA (al registrarse). Al cambiar de cuenta cambia el archivo.
///
///  • Invitado (sin userId): colección solo en memoria; se borra al cerrar.
/// </summary>
public static class UniverseStore
{
    // Caché en memoria para invitado (se pierde al cerrar el juego).
    static UniverseCollection _guestCache;

    static bool IsGuest => string.IsNullOrEmpty(SessionData.UserId);

    static string FilePath =>
        Path.Combine(Application.persistentDataPath, $"universes_{SessionData.UserId}.json");

    public static UniverseCollection Load() => Load(out _);

    /// <summary>corrupt = true si el archivo existe pero no se pudo leer/parsear.</summary>
    public static UniverseCollection Load(out bool corrupt)
    {
        corrupt = false;
        if (IsGuest)
            return _guestCache ??= new UniverseCollection();

        try
        {
            if (!File.Exists(FilePath)) return new UniverseCollection();
            string json = File.ReadAllText(FilePath);
            var col = JsonUtility.FromJson<UniverseCollection>(json);
            if (col == null) { corrupt = true; return new UniverseCollection(); }
            return col;
        }
        catch (System.Exception e)
        {
            corrupt = true;
            Debug.LogWarning($"[UniverseStore] No se pudo cargar: {e.Message}");
            return new UniverseCollection();
        }
    }

    /// <summary>Devuelve false si no se pudo guardar (p. ej. disco lleno).</summary>
    public static bool Save(UniverseCollection col)
    {
        if (IsGuest)
        {
            _guestCache = col;
            return true;
        }

        try
        {
            string json = JsonUtility.ToJson(col, true);
            // Escritura atómica: si el disco está lleno, falla en el .tmp y el
            // archivo original queda intacto (el listado no se daña).
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(FilePath)) File.Delete(FilePath);
            File.Move(tmp, FilePath);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[UniverseStore] No se pudo guardar: {e.Message}");
            return false;
        }
    }

    public static bool Add(UniverseData universe)
    {
        var col = Load();
        col.universes.Add(universe);
        return Save(col);
    }

    public static void Update(UniverseData universe)
    {
        if (universe == null) return;
        var col = Load();
        int idx = col.universes.FindIndex(u => u.id == universe.id);
        if (idx >= 0) col.universes[idx] = universe;
        else          col.universes.Add(universe);
        Save(col);
    }

    public static void Remove(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        var col = Load();
        col.universes.RemoveAll(u => u.id == id);
        Save(col);
    }
}
