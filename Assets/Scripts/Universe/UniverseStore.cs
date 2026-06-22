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

    public static UniverseCollection Load()
    {
        if (IsGuest)
            return _guestCache ??= new UniverseCollection();

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
        if (IsGuest)
        {
            _guestCache = col;
            return;
        }

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
