using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// Persistencia local de universos, separada por cuenta.
///
///  • Con sesión iniciada: archivo JSON por cuenta en
///    Application.persistentDataPath/universes_{hash(email)}.json
///    Al cambiar de cuenta cambia el archivo → cada cuenta ve solo los suyos.
///
///  • Modo invitado (sin token): colección solo en memoria. Persiste mientras
///    el juego está abierto y se borra al cerrarlo.
///
/// La identidad activa se decide con SessionData.IsLoggedIn.
/// </summary>
public static class UniverseStore
{
    // Caché en memoria para invitado (se pierde al cerrar el juego).
    static UniverseCollection _guestCache;

    static bool IsGuest => !SessionData.IsLoggedIn;

    static string FilePath
    {
        get
        {
            string key = Hash(SessionData.Email.Trim().ToLowerInvariant());
            return Path.Combine(Application.persistentDataPath, $"universes_{key}.json");
        }
    }

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

    // Hash estable del email para no exponerlo en el nombre del archivo.
    static string Hash(string s)
    {
        using var md5 = MD5.Create();
        byte[] bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(s));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (byte b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
