using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class ApiManager : MonoBehaviour
{
    public static ApiManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("ApiManager");
                _instance = go.AddComponent<ApiManager>();
            }
            return _instance;
        }
    }
    static ApiManager _instance;

    //MOBILE
    // const string BASE_URL = "http://192.168.18.26:8000/api"; // Example for mobile
        
    //LAPTOP
    const string BASE_URL = "http://127.0.0.1:8000/api";

    // Tiempo de espera (s) para la detección de moléculas. Si el servicio de IA no
    // responde dentro de este margen, el request falla con code 0 (pérdida de conexión).
    const int DETECT_TIMEOUT_SECONDS = 8;

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Endpoints ─────────────────────────────────────────────────────────────

    public void ValidateEmail(string email, Action<string> onSuccess, Action<int, string> onError)
    {
        string body = JsonUtility.ToJson(new EmailRequest { email = email });
        StartCoroutine(Post("/authentication/validate/email", body, onSuccess, onError));
    }

    public void ValidatePassword(string password, Action<string> onSuccess, Action<int, string> onError)
    {
        string body = JsonUtility.ToJson(new PasswordRequest { password = password });
        StartCoroutine(Post("/authentication/validate/password", body, onSuccess, onError));
    }

    public void ValidateUsername(string username, Action<string> onSuccess, Action<int, string> onError)
    {
        string body = JsonUtility.ToJson(new UsernameRequest { username = username });
        StartCoroutine(Post("/authentication/validate/username", body, onSuccess, onError));
    }

    public void CreateAccount(string email, string password, string username,
                              Action<string> onSuccess, Action<int, string> onError)
    {
        string body = JsonUtility.ToJson(new AccountRequest
        {
            email = email, password = password, username = username
        });

        StartCoroutine(PostRaw("/authentication/account", body,
            onSuccess: json =>
            {
                string userId = JsonUtility.FromJson<AccountResponse>(json).userId;
                onSuccess?.Invoke(userId);
            },
            onError: onError));
    }

    public void GetProfile(string publicId,
                           Action<ProfileResponse> onSuccess, Action<int, string> onError)
    {
        StartCoroutine(GetAuthed($"/users/profile/{publicId}",
            onSuccess: json => onSuccess?.Invoke(JsonUtility.FromJson<ProfileResponse>(json)),
            onError: onError));
    }

    public void GetJournal(string userPublicId,
                           Action<JournalEntry[]> onSuccess, Action<int, string> onError)
    {
        StartCoroutine(GetAuthed($"/journal/{userPublicId}",
            onSuccess: json =>
            {
                JournalEntry[] items;
                try
                {
                    // JsonUtility no parsea arrays de nivel raíz → se envuelve en un objeto.
                    var wrapped = JsonUtility.FromJson<JournalListWrapper>("{\"items\":" + json + "}");
                    items = (wrapped != null && wrapped.items != null) ? wrapped.items : new JournalEntry[0];
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[API] No se pudo parsear el diario: {ex.Message}");
                    items = new JournalEntry[0];
                }
                onSuccess?.Invoke(items);
            },
            onError: onError));
    }

    public void Login(string email, string password,
                      Action<LoginResponse> onSuccess, Action<int, string> onError)
    {
        string body = JsonUtility.ToJson(new LoginRequest { email = email, password = password });

        StartCoroutine(PostRaw("/session/login", body,
            onSuccess: json =>
            {
                _redirectingToLogin = false; // nueva sesión válida
                var resp = JsonUtility.FromJson<LoginResponse>(json);
                onSuccess?.Invoke(resp);
            },
            onError: onError));
    }

    public void Logout(string refreshToken, Action<string> onSuccess, Action<int, string> onError)
    {
        string body = JsonUtility.ToJson(new LogoutRequest { refreshToken = refreshToken });
        Debug.Log($"[API] POST {BASE_URL}/session/logout\n{body}");
        StartCoroutine(Post("/session/logout", body, onSuccess, onError));
    }

    public void AddCreatedUniverse(string userPublicId,
                                   Action<JournalStatsResponse> onSuccess, Action<int, string> onError)
    {
        string endpoint = $"/journal/{userPublicId}/created-universes";
        Debug.Log($"[API] PATCH {BASE_URL}{endpoint}");
        StartCoroutine(PatchAuthed(endpoint, null,
            json => onSuccess?.Invoke(JsonUtility.FromJson<JournalStatsResponse>(json)),
            onError));
    }

    public void DecrementCreatedUniverse(string userPublicId,
                                         Action<JournalStatsResponse> onSuccess, Action<int, string> onError)
    {
        string endpoint = $"/journal/{userPublicId}/created-universes/decrement";
        Debug.Log($"[API] PATCH {BASE_URL}{endpoint}");
        StartCoroutine(PatchAuthed(endpoint, null,
            json => onSuccess?.Invoke(JsonUtility.FromJson<JournalStatsResponse>(json)),
            onError));
    }

    // El accessToken se ignora (se toma de SessionData para permitir refresh+reintento);
    // se mantiene en la firma por compatibilidad con las pantallas que ya lo pasan.
    public void DeleteAccount(string accessToken, Action<string> onSuccess, Action<int, string> onError)
    {
        Debug.Log($"[API] DELETE {BASE_URL}/users/me");
        StartCoroutine(DeleteAuthed("/users/me",
            json => onSuccess?.Invoke(JsonUtility.FromJson<MessageResponse>(json).message),
            onError));
    }

    // El accessToken se ignora (se toma de SessionData). Ver nota en DeleteAccount.
    public void ChangePassword(string accessToken, string currentPassword, string newPassword,
                               Action<string> onSuccess, Action<int, string> onError)
    {
        string body = JsonUtility.ToJson(new ChangePasswordRequest
        {
            currentPassword = currentPassword, newPassword = newPassword
        });
        Debug.Log($"[API] PATCH {BASE_URL}/authentication/password");
        StartCoroutine(PatchAuthed("/authentication/password", body,
            json => onSuccess?.Invoke(JsonUtility.FromJson<MessageResponse>(json).message),
            onError));
    }

    // ── Recuperación de contraseña (olvidé mi contraseña) ───────────────────────

    /// <summary>Paso 1/3: pide que se envíe un código de 6 caracteres al correo.
    /// El backend SIEMPRE responde 200 con el mismo mensaje, exista o no la cuenta.</summary>
    public void RequestPasswordReset(string email, Action<string> onSuccess, Action<int, string> onError)
    {
        string body = JsonUtility.ToJson(new EmailRequest { email = email });
        StartCoroutine(Post("/authentication/password/reset/request", body, onSuccess, onError));
    }

    /// <summary>Paso 2/3: verifica el código sin consumirlo (se puede validar antes
    /// de mostrar la pantalla de nueva contraseña).</summary>
    public void VerifyPasswordResetCode(string email, string code,
                                        Action<string> onSuccess, Action<int, string> onError)
    {
        string body = JsonUtility.ToJson(new ResetVerifyRequest { email = email, code = code });
        StartCoroutine(Post("/authentication/password/reset/verify", body, onSuccess, onError));
    }

    /// <summary>Paso 3/3: cambia la contraseña. Al completarse, el código queda
    /// consumido y se cierran TODAS las sesiones activas del usuario.</summary>
    public void ResetPassword(string email, string code, string newPassword,
                              Action<string> onSuccess, Action<int, string> onError)
    {
        string body = JsonUtility.ToJson(new ResetPasswordRequest
        {
            email = email, code = code, newPassword = newPassword
        });
        StartCoroutine(Post("/authentication/password/reset", body, onSuccess, onError));
    }

    /// <summary>Suma 'seconds' al tiempo total jugado de la cuenta (endpoint incremental).</summary>
    public void AddTimePlayed(string userPublicId, int seconds,
                              Action<JournalStatsResponse> onSuccess, Action<int, string> onError)
    {
        string endpoint = $"/journal/{userPublicId}/time-played";
        string body = JsonUtility.ToJson(new TimePlayedRequest { seconds = seconds });
        Debug.Log($"[API] PATCH {BASE_URL}{endpoint}\n{body}");
        StartCoroutine(PatchAuthed(endpoint, body,
            json => onSuccess?.Invoke(JsonUtility.FromJson<JournalStatsResponse>(json)),
            onError));
    }

    public void DetectMolecule(string userPublicId, AtomDTO[] atoms, BondDTO[] bonds,
                               Action<DetectResponse> onSuccess, Action<int, string> onError)
    {
        // Logeado = hay userId y access token. Invitado = sin ambos.
        bool logged = !string.IsNullOrEmpty(userPublicId) && !string.IsNullOrEmpty(SessionData.AccessToken);

        // Invitado: sin token y sin userPublicId (mandarlo sin token daría 401).
        // Logeado: con token; userPublicId en el body coincide con el del token (permitido).
        string body = logged
            ? JsonUtility.ToJson(new DetectRequest { userPublicId = userPublicId, atoms = atoms, bonds = bonds })
            : JsonUtility.ToJson(new DetectRequestGuest { atoms = atoms, bonds = bonds });
        Debug.Log($"[API] POST {BASE_URL}/detection/molecule (logged={logged})\n{body}");

        void OnOk(string json) { Debug.Log($"[API] respuesta OK:\n{json}"); onSuccess?.Invoke(JsonUtility.FromJson<DetectResponse>(json)); }

        if (logged)
            StartCoroutine(PostAuthed("/detection/molecule", body, OnOk, onError, DETECT_TIMEOUT_SECONDS));
        else
            StartCoroutine(PostRaw("/detection/molecule", body, OnOk, onError, DETECT_TIMEOUT_SECONDS));
    }

    // ── Core HTTP ─────────────────────────────────────────────────────────────

    // Variante que entrega el campo "message" ya parseado.
    IEnumerator Post(string endpoint, string jsonBody, Action<string> onSuccess, Action<int, string> onError)
    {
        return PostRaw(endpoint, jsonBody,
            json => onSuccess?.Invoke(JsonUtility.FromJson<MessageResponse>(json).message),
            onError);
    }

    // ── HTTP autenticado (Authorization: Bearer + refresh automático) ───────────
    // El access token vence a los 30 min. Ante 401 ERR_TOKEN_EXPIRED se refresca una
    // vez y se reintenta; si el token es inválido/ausente o el refresh falla, la
    // sesión muere y se vuelve al login.
    bool _redirectingToLogin;
    bool _refreshing;
    bool _lastRefreshOk;

    IEnumerator SendAuthed(Func<UnityWebRequest> build, Action<string> onSuccess, Action<int, string> onError)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            using var req = build();
            if (!string.IsNullOrEmpty(SessionData.AccessToken))
                req.SetRequestHeader("Authorization", "Bearer " + SessionData.AccessToken);

            yield return req.SendWebRequest();

            string responseText = req.downloadHandler != null ? req.downloadHandler.text : "";
            if (req.result == UnityWebRequest.Result.Success)
            {
                onSuccess?.Invoke(responseText);
                yield break;
            }

            int code = (int)req.responseCode;
            string detail = TryParseDetail(responseText);
            Debug.LogWarning($"[API] {req.method} {req.url} FALLÓ · result={req.result} · code={code} · detail={detail}");

            // Access token vencido → refrescar una vez y reintentar.
            if (code == 401 && detail == "ERR_TOKEN_EXPIRED" && attempt == 0)
            {
                bool refreshed = false;
                yield return RefreshTokenRoutine(ok => refreshed = ok);
                if (refreshed) continue;    // reintenta con el token nuevo
                EndSession();
                onError?.Invoke(code, detail);
                yield break;
            }

            // Token ausente o inválido (incluye mandar el refresh por error) → sesión muerta.
            if (code == 401 && (detail == "ERR_TOKEN_MISSING" || detail == "ERR_TOKEN_INVALID"))
            {
                EndSession();
                onError?.Invoke(code, detail);
                yield break;
            }

            if (code == 403 && detail == "ERR_NOT_RESOURCE_OWNER")
                Debug.LogWarning("[API] ERR_NOT_RESOURCE_OWNER: se pidió un recurso de otra cuenta (bug de cliente, no se reintenta).");

            onError?.Invoke(code, detail);
            yield break;
        }
    }

    // Refresca el access token con POST /session/refresh. Single-flight: si ya hay uno
    // en curso, espera su resultado en vez de disparar otro (el refresh token es de un solo uso).
    IEnumerator RefreshTokenRoutine(Action<bool> done)
    {
        if (_refreshing)
        {
            while (_refreshing) yield return null;
            done(_lastRefreshOk);
            yield break;
        }

        string rt = SessionData.RefreshToken;
        if (string.IsNullOrEmpty(rt)) { done(false); yield break; }

        _refreshing = true;
        _lastRefreshOk = false;

        string body = JsonUtility.ToJson(new RefreshRequest { refreshToken = rt });
        using (var req = new UnityWebRequest(BASE_URL + "/session/refresh", "POST"))
        {
            req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Accept", "application/json");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var resp = JsonUtility.FromJson<LoginResponse>(req.downloadHandler.text);
                // El refresh token es deslizante: puede venir uno nuevo (o no).
                string newRefresh   = string.IsNullOrEmpty(resp.refreshToken) ? rt : resp.refreshToken;
                string newTokenType = string.IsNullOrEmpty(resp.tokenType) ? SessionData.TokenType : resp.tokenType;
                SessionData.SetTokens(resp.accessToken, newRefresh, newTokenType, resp.expiresIn);
                _lastRefreshOk = !string.IsNullOrEmpty(resp.accessToken);
                if (_lastRefreshOk) { _redirectingToLogin = false; Debug.Log("[API] Access token refrescado."); }
            }
            else
            {
                Debug.LogWarning($"[API] Refresh falló · code={(int)req.responseCode} · {req.error}");
            }
        }

        _refreshing = false;
        done(_lastRefreshOk);
    }

    // Sesión muerta: limpiar y volver al login (una sola vez, aunque fallen varias peticiones).
    void EndSession()
    {
        if (_redirectingToLogin) return;
        _redirectingToLogin = true;
        Debug.LogWarning("[API] Sesión finalizada (token inválido o refresh fallido) → LoginScene.");
        SessionData.Clear();
        UnityEngine.SceneManagement.SceneManager.LoadScene("LoginScene");
    }

    // ── Constructores de petición autenticada ──────────────────────────────────
    IEnumerator GetAuthed(string endpoint, Action<string> onSuccess, Action<int, string> onError)
    {
        return SendAuthed(() =>
        {
            var req = UnityWebRequest.Get(BASE_URL + endpoint);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Accept", "application/json");
            return req;
        }, onSuccess, onError);
    }

    IEnumerator PatchAuthed(string endpoint, string jsonBody, Action<string> onSuccess, Action<int, string> onError)
    {
        return SendAuthed(() =>
        {
            var req = new UnityWebRequest(BASE_URL + endpoint, "PATCH");
            if (!string.IsNullOrEmpty(jsonBody))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                req.SetRequestHeader("Content-Type", "application/json");
            }
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Accept", "application/json");
            return req;
        }, onSuccess, onError);
    }

    IEnumerator DeleteAuthed(string endpoint, Action<string> onSuccess, Action<int, string> onError)
    {
        return SendAuthed(() =>
        {
            var req = new UnityWebRequest(BASE_URL + endpoint, "DELETE");
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Accept", "application/json");
            return req;
        }, onSuccess, onError);
    }

    IEnumerator PostAuthed(string endpoint, string jsonBody, Action<string> onSuccess, Action<int, string> onError, int timeoutSeconds = 0)
    {
        return SendAuthed(() =>
        {
            var req = new UnityWebRequest(BASE_URL + endpoint, "POST");
            req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Accept", "application/json");
            if (timeoutSeconds > 0) req.timeout = timeoutSeconds;
            return req;
        }, onSuccess, onError);
    }

    // Variante que entrega el cuerpo crudo (JSON) para que el caller lo parsee.
    IEnumerator PostRaw(string endpoint, string jsonBody, Action<string> onSuccess, Action<int, string> onError, int timeoutSeconds = 0)
    {
        string url = BASE_URL + endpoint;
        byte[] raw = Encoding.UTF8.GetBytes(jsonBody);

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler   = new UploadHandlerRaw(raw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Accept", "application/json");
        if (timeoutSeconds > 0) req.timeout = timeoutSeconds;

        yield return req.SendWebRequest();

        string responseText = req.downloadHandler.text;

        if (req.result == UnityWebRequest.Result.Success)
        {
            onSuccess?.Invoke(responseText);
        }
        else
        {
            int code = (int)req.responseCode;
            string detail = TryParseDetail(responseText);
            Debug.LogWarning($"[API] POST {url} FALLÓ · result={req.result} · code={code} · error='{req.error}' · body='{responseText}'");
            onError?.Invoke(code, detail);
        }
    }

    static string TryParseDetail(string json)
    {
        try { return JsonUtility.FromJson<DetailResponse>(json).detail; }
        catch { return "ERR_UNKNOWN"; }
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────

    [Serializable] class EmailRequest    { public string email; }
    [Serializable] class PasswordRequest { public string password; }
    [Serializable] class UsernameRequest { public string username; }
    [Serializable] class AccountRequest  { public string email; public string password; public string username; }
    [Serializable] class AccountResponse { public string userId; }
    [Serializable] class LoginRequest    { public string email; public string password; }
    [Serializable] class LogoutRequest     { public string refreshToken; }
    [Serializable] class RefreshRequest    { public string refreshToken; }
    [Serializable] class ChangePasswordRequest { public string currentPassword; public string newPassword; }
    [Serializable] class ResetVerifyRequest     { public string email; public string code; }
    [Serializable] class ResetPasswordRequest   { public string email; public string code; public string newPassword; }
    [Serializable] class TimePlayedRequest { public int seconds; }
    [Serializable] class MessageResponse   { public string message; }
    [Serializable] class DetailResponse    { public string detail; }

    [Serializable]
    public class JournalStatsResponse
    {
        public string message;
        public int    totalDiscoveries;
        public int    totalAtomsPlaced;
        public int    totalAtomsRemoved;
        public int    totalPlayTimeSeconds;
        public int    totalNanometersWalked;
        public int    totalCreatedUniverses;
        public string firstSessionAt;
        public string lastActiveAt;
    }

    [Serializable]
    public class ProfileResponse
    {
        public string message;
        public string username;
        public string email;
        public int    moleculesDiscovered;
        public int    playTimeSeconds;
        public int    createdUniverses;
        public string memberSince;
    }

    [Serializable]
    public class LoginResponse
    {
        public string message;
        public string accessToken;
        public string refreshToken;
        public string tokenType;
        public int    expiresIn;
        public string userId;        // incluido por el backend en el login
        public string userPublicId;  // alias por si el campo se llama así
    }

    // ── Detección de moléculas ──────────────────────────────────────────────
    [Serializable] public class AtomDTO { public int id; public string element; public float x; public float y; public float z; }
    [Serializable] public class BondDTO { public int beginAtomId; public int endAtomId; public int order; }
    [Serializable] class DetectRequest      { public string userPublicId; public AtomDTO[] atoms; public BondDTO[] bonds; }
    [Serializable] class DetectRequestGuest { public AtomDTO[] atoms; public BondDTO[] bonds; } // sin userPublicId (invitado)

    [Serializable]
    public class MoleculeDTO
    {
        public string name;
        public string canonicalSmiles;
        public string molecularFormula;
        public string inchikey;
        public float  molarMass;
        public string polarity;
        public float  logP;
        public string aqueousSolubility;
        public float  aqueousSolubilityLogS;
        public bool   isKnown;
        public bool   isNewDiscovery;
        public BondDTO[] bonds;   // enlaces reales inferidos/validados por el backend
    }

    [Serializable]
    public class DetectResponse
    {
        public string      message;
        public bool        isValid;
        public string      invalidityReason;
        public MoleculeDTO molecule;
    }
}
