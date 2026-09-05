using UnityEngine;

/// <summary>
/// Sesión del usuario. Vive en memoria y se PERSISTE en el dispositivo, para que
/// cerrar la app no obligue a volver a iniciar sesión.
///
/// Lo que de verdad importa guardar es el refreshToken: el access token vence a
/// los 30 minutos, pero con el refresh ApiManager consigue uno nuevo solo. Como
/// ese refresh es de un solo uso y ApiManager lo rota llamando a SetTokens, aquí
/// se vuelve a guardar en cada rotación; si no, al siguiente arranque usaríamos
/// uno ya consumido y la sesión moriría.
///
/// Se restaura de forma OPTIMISTA, sin validar contra el servidor: si el token
/// resultara muerto, la primera petición autenticada devuelve 401 y ApiManager ya
/// sabe refrescar o, si no puede, cerrar la sesión y mandar al login.
///
/// LIMITACIÓN: PlayerPrefs guarda en claro (registro en Windows, un XML privado
/// de la app en Android). En un dispositivo con root o vía copia de seguridad, el
/// refresh token es legible. Protegerlo de verdad requiere EncryptedSharedPreferences
/// o el Keystore de Android, que necesitan un plugin nativo.
/// </summary>
public static class SessionData
{
    public static string UserId   { get; private set; } = "";
    public static string Username { get; private set; } = "";
    public static string Email    { get; private set; } = "";

    public static string AccessToken  { get; private set; } = "";
    public static string RefreshToken { get; private set; } = "";
    public static string TokenType    { get; private set; } = "";
    public static int    ExpiresIn    { get; private set; } = 0;

    public static bool IsLoggedIn => !string.IsNullOrEmpty(AccessToken);

    // ── Persistencia ──────────────────────────────────────────────────────────

    const string KEY_USER_ID  = "chemitech_session_userid";
    const string KEY_USERNAME = "chemitech_session_username";
    const string KEY_EMAIL    = "chemitech_session_email";
    const string KEY_ACCESS   = "chemitech_session_access";
    const string KEY_REFRESH  = "chemitech_session_refresh";
    const string KEY_TYPE     = "chemitech_session_tokentype";
    const string KEY_EXPIRES  = "chemitech_session_expires";

    /// <summary>
    /// Se ejecuta BeforeSplashScreen, es decir ANTES que los managers de audio y
    /// gráficos (que arrancan en BeforeSceneLoad). Ese orden importa: esos leen
    /// sus preferencias por cuenta usando UserId, así que si la sesión no
    /// estuviera restaurada todavía, cargarían los valores de invitado.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    static void Restore()
    {
        string userId  = PlayerPrefs.GetString(KEY_USER_ID, "");
        string refresh = PlayerPrefs.GetString(KEY_REFRESH, "");

        // Sin refreshToken la sesión no se puede renovar, así que no sirve de nada
        // restaurarla: mejor arrancar como invitado que fingir estar dentro.
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(refresh))
        {
            if (PlayerPrefs.HasKey(KEY_USER_ID)) WipeStored();
            return;
        }

        UserId       = userId;
        Username     = PlayerPrefs.GetString(KEY_USERNAME, "");
        Email        = PlayerPrefs.GetString(KEY_EMAIL, "");
        AccessToken  = PlayerPrefs.GetString(KEY_ACCESS, "");
        RefreshToken = refresh;
        TokenType    = PlayerPrefs.GetString(KEY_TYPE, "");
        ExpiresIn    = PlayerPrefs.GetInt(KEY_EXPIRES, 0);

        Debug.Log($"[Session] Sesión restaurada · userId='{UserId}'");
    }

    static void Persist()
    {
        PlayerPrefs.SetString(KEY_USER_ID,  UserId);
        PlayerPrefs.SetString(KEY_USERNAME, Username);
        PlayerPrefs.SetString(KEY_EMAIL,    Email);
        PlayerPrefs.SetString(KEY_ACCESS,   AccessToken);
        PlayerPrefs.SetString(KEY_REFRESH,  RefreshToken);
        PlayerPrefs.SetString(KEY_TYPE,     TokenType);
        PlayerPrefs.SetInt   (KEY_EXPIRES,  ExpiresIn);
        PlayerPrefs.Save();
    }

    static void WipeStored()
    {
        PlayerPrefs.DeleteKey(KEY_USER_ID);
        PlayerPrefs.DeleteKey(KEY_USERNAME);
        PlayerPrefs.DeleteKey(KEY_EMAIL);
        PlayerPrefs.DeleteKey(KEY_ACCESS);
        PlayerPrefs.DeleteKey(KEY_REFRESH);
        PlayerPrefs.DeleteKey(KEY_TYPE);
        PlayerPrefs.DeleteKey(KEY_EXPIRES);
        PlayerPrefs.Save();
    }

    // ── Mutadores ─────────────────────────────────────────────────────────────

    public static void SetSession(string userId, string username, string email)
    {
        UserId   = userId;
        Username = username;
        Email    = email;
        Persist();
    }

    public static void SetUsername(string username)
    {
        Username = username ?? "";
        Persist();
    }

    /// <summary>
    /// Guarda los tokens. La llama el login y también cada refresh de ApiManager,
    /// que es lo que mantiene al día el refreshToken rotado en el dispositivo.
    /// </summary>
    public static void SetTokens(string accessToken, string refreshToken, string tokenType, int expiresIn)
    {
        AccessToken  = accessToken;
        RefreshToken = refreshToken;
        TokenType    = tokenType;
        ExpiresIn    = expiresIn;
        Persist();
    }

    public static void Clear()
    {
        UserId = Username = Email = "";
        AccessToken = RefreshToken = TokenType = "";
        ExpiresIn = 0;
        WipeStored();
    }
}
