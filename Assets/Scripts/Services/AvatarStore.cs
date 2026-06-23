using UnityEngine;

/// <summary>
/// Persistencia local del avatar elegido, asociada a la cuenta (por userId),
/// siguiendo el mismo criterio que UniverseStore:
///   • Con userId: se guarda por cuenta (PlayerPrefs "avatar_{userId}").
///   • Invitado (sin userId): solo en memoria; se pierde al cerrar.
/// Se guarda únicamente el índice del avatar (el visual se reconstruye desde él).
/// </summary>
public static class AvatarStore
{
    static int _guestCache = -1;

    static bool IsGuest => string.IsNullOrEmpty(SessionData.UserId);
    static string Key   => $"avatar_{SessionData.UserId}";

    public static int Load(int fallback)
    {
        if (IsGuest) return _guestCache >= 0 ? _guestCache : fallback;
        return PlayerPrefs.GetInt(Key, fallback);
    }

    public static void Save(int index)
    {
        if (IsGuest) { _guestCache = index; return; }
        PlayerPrefs.SetInt(Key, index);
        PlayerPrefs.Save();
    }
}
