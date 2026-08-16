using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Preferencias asociadas a la CUENTA, siguiendo el mismo criterio que
/// AvatarStore y UniverseStore:
///
///   • Con userId: PlayerPrefs "{prefijo}_{userId}". Como PlayerPrefs ya es por
///     dispositivo, la clave significa "lo que el usuario X eligió EN ESTE
///     aparato": una cuenta que juega en una tablet potente y en un teléfono
///     flojo guarda un valor distinto en cada uno, que es lo deseable.
///   • Invitado (sin userId): solo en memoria; aplica durante la sesión y se
///     pierde al cerrar, así que siempre vuelve a arrancar en los valores por
///     defecto.
///
/// Centraliza el patrón para que audio, gráficos y lo que venga después no
/// repitan la misma lógica de invitado/cuenta cada uno por su lado.
/// </summary>
public static class AccountPrefs
{
    static readonly Dictionary<string, int> _guestCache = new Dictionary<string, int>();

    // Último usuario visto. Sirve para tirar la caché de invitado cuando cambia
    // la sesión: si alguien tocó los ajustes como invitado y luego inicia sesión
    // (o al revés), esos valores no deben filtrarse a la sesión siguiente.
    static string _lastUser;

    static bool IsGuest => string.IsNullOrEmpty(SessionData.UserId);

    static void SyncUser()
    {
        string current = SessionData.UserId ?? "";
        if (_lastUser == current) return;
        _guestCache.Clear();
        _lastUser = current;
    }

    public static int GetInt(string prefix, int fallback)
    {
        SyncUser();
        if (IsGuest)
            return _guestCache.TryGetValue(prefix, out int v) ? v : fallback;
        return PlayerPrefs.GetInt($"{prefix}_{SessionData.UserId}", fallback);
    }

    public static void SetInt(string prefix, int value)
    {
        SyncUser();
        if (IsGuest) { _guestCache[prefix] = value; return; }
        PlayerPrefs.SetInt($"{prefix}_{SessionData.UserId}", value);
    }

    /// <summary>
    /// Vuelca a disco. Se llama al salir de Ajustes, no en cada cambio:
    /// PlayerPrefs.Save() reescribe el archivo entero y arrastrar un slider
    /// dispararía decenas de escrituras por segundo.
    /// </summary>
    public static void Flush() => PlayerPrefs.Save();
}
