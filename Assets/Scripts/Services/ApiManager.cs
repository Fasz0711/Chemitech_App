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
    // const string BASE_URL = "http://192.168.18.10:8000/api"; 
    const string BASE_URL = "http://127.0.0.1:8000/api";


    // Tiempo de espera (s) para la detección de moléculas. Si el servicio de IA no
    // responde dentro de este margen, el request falla con code 0 (pérdida de conexión).
    const int DETECT_TIMEOUT_SECONDS = 8;

    // Varias rutas de clase no llevan cuerpo, pero se mandan como POST con
    // Content-Type json: un objeto vacío es lo que FastAPI acepta sin quejarse.
    const string EMPTY_BODY = "{}";

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

    // ── Modo clase (Fase 1) ────────────────────────────────────────────────────
    // Contrato: docs/CONTRATO_CLASES_FASE1.txt. Todas exigen Bearer y {id} es el
    // publicId de la clase. Los 403 de clase (ERR_NOT_TEACHER, ERR_NOT_CLASS_OWNER,
    // ERR_NOT_CLASS_MEMBER) NO son sesión muerta: SendAuthed ya solo expulsa en 401.

    public void GetMyClasses(Action<MyClassesResponse> onSuccess, Action<int, string> onError)
    {
        Debug.Log($"[API] GET {BASE_URL}/classes/mine");
        StartCoroutine(GetAuthed("/classes/mine",
            json => onSuccess?.Invoke(JsonUtility.FromJson<MyClassesResponse>(json)),
            onError));
    }

    /// <summary>Crea la clase Y las cuentas en una sola llamada (todo o nada).
    /// OJO: las contraseñas de la respuesta son irrepetibles.</summary>
    public void CreateClass(string name, string section, int studentCount,
                            Action<ClassCreatedResponse> onSuccess, Action<int, string> onError)
    {
        string body = JsonUtility.ToJson(new CreateClassRequest
        {
            name = name, section = section, studentCount = studentCount
        });
        Debug.Log($"[API] POST {BASE_URL}/classes - {body}");
        StartCoroutine(PostAuthed("/classes", body,
            json => onSuccess?.Invoke(JsonUtility.FromJson<ClassCreatedResponse>(json)),
            onError));
    }

    /// <summary>Alumnos que llegan tarde. Continúa la numeración y devuelve SOLO
    /// las cuentas nuevas, con la misma forma que CreateClass.</summary>
    public void AddClassStudents(string classId, int count,
                                 Action<ClassCreatedResponse> onSuccess, Action<int, string> onError)
    {
        string body = JsonUtility.ToJson(new AddStudentsRequest { count = count });
        Debug.Log($"[API] POST {BASE_URL}/classes/{classId}/students - {body}");
        StartCoroutine(PostAuthed($"/classes/{classId}/students", body,
            json => onSuccess?.Invoke(JsonUtility.FromJson<ClassCreatedResponse>(json)),
            onError));
    }

    /// <summary>Repone la contraseña de un alumno que la olvidó. Cierra sus sesiones
    /// abiertas: tiene que volver a entrar con la nueva.</summary>
    public void ReplaceStudentPassword(string classId, string code,
                                       Action<PasswordReplacedResponse> onSuccess, Action<int, string> onError)
    {
        Debug.Log($"[API] POST {BASE_URL}/classes/{classId}/students/{code}/password");
        StartCoroutine(PostAuthed($"/classes/{classId}/students/{code}/password", EMPTY_BODY,
            json => onSuccess?.Invoke(JsonUtility.FromJson<PasswordReplacedResponse>(json)),
            onError));
    }

    public void StartClass(string classId, Action<ClassStatusResponse> onSuccess, Action<int, string> onError)
        => SendClassStatus($"/classes/{classId}/start", onSuccess, onError);

    /// <summary>TERMINAR ES DEFINITIVO: la clase no se puede reabrir. Confirmar antes.</summary>
    public void StopClass(string classId, Action<ClassStatusResponse> onSuccess, Action<int, string> onError)
        => SendClassStatus($"/classes/{classId}/stop", onSuccess, onError);

    void SendClassStatus(string endpoint, Action<ClassStatusResponse> onSuccess, Action<int, string> onError)
    {
        Debug.Log($"[API] POST {BASE_URL}{endpoint}");
        StartCoroutine(PostAuthed(endpoint, EMPTY_BODY,
            json => onSuccess?.Invoke(JsonUtility.FromJson<ClassStatusResponse>(json)),
            onError));
    }

    /// <summary>Sondeo. 'sinceVersion' es la última versión recibida; un valor negativo
    /// pide el estado completo. Un since roto nunca da 400, así que no hay que validarlo.</summary>
    public void GetClassState(string classId, int sinceVersion,
                              Action<ClassStateResponse> onSuccess, Action<int, string> onError)
    {
        StartCoroutine(GetAuthed($"/classes/{classId}/state?since={sinceVersion}",
            json => onSuccess?.Invoke(JsonUtility.FromJson<ClassStateResponse>(json)),
            onError));
    }

    /// <summary>El docente conduce la escena. 'actionsJson' es un string OPACO: se manda
    /// tal cual y el cliente nunca lo parsea (el vocabulario es polimórfico y JsonUtility
    /// perdería campos en silencio).
    ///
    /// La respuesta es un estado COMPLETO, igual que el sondeo: hay que pintarlo al
    /// instante y guardar SU versión. Si se espera al siguiente sondeo para actualizarla,
    /// dos botones seguidos chocan con 409 contra la acción anterior del propio docente.</summary>
    public void ApplyCommand(string classId, string actionsJson, int baseVersion,
                             Action<ClassStateResponse> onSuccess, Action<int, string> onError,
                             string source = "button", string promptText = "")
    {
        string body = JsonUtility.ToJson(new ApplyCommandRequest
        {
            actionsJson = actionsJson,
            baseVersion = baseVersion,
            source      = source,       // "button" o "prompt"
            promptText  = promptText,   // lo que escribió el docente; queda como evidencia
        });
        Debug.Log($"[API] POST {BASE_URL}/classes/{classId}/commands/apply - {actionsJson}");

        StartCoroutine(PostAuthed($"/classes/{classId}/commands/apply", body,
            json => onSuccess?.Invoke(JsonUtility.FromJson<ClassStateResponse>(json)),
            onError));
    }

    /// <summary>Traduce lo que escribió el docente a acciones, SIN aplicar nada. Está
    /// separado de /apply justamente para que se pueda mostrar una vista previa: si
    /// interpretar tuviera efecto, no habría nada que confirmar.</summary>
    public void InterpretCommand(string classId, string promptText, int baseVersion,
                                 Action<InterpretResponse> onSuccess, Action<int, string> onError)
    {
        string body = JsonUtility.ToJson(new InterpretCommandRequest
        {
            prompt      = promptText,
            baseVersion = baseVersion,
        });
        Debug.Log($"[API] POST {BASE_URL}/classes/{classId}/commands/interpret - {promptText}");

        StartCoroutine(PostAuthed($"/classes/{classId}/commands/interpret", body,
            json => onSuccess?.Invoke(JsonUtility.FromJson<InterpretResponse>(json)),
            onError));
    }

    /// <summary>Quién está conectado. La presencia vive en memoria del servidor: si el
    /// backend reinicia, se reconstruye sola en un par de sondeos.</summary>
    public void GetClassRoster(string classId,
                               Action<RosterResponse> onSuccess, Action<int, string> onError)
    {
        StartCoroutine(GetAuthed($"/classes/{classId}/roster",
            json => onSuccess?.Invoke(JsonUtility.FromJson<RosterResponse>(json)),
            onError));
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
    // vez y se reintenta. Solo se expulsa al login si el refresh es RECHAZADO; un
    // fallo temporal (429, red caída) mantiene la sesión viva.
    bool _redirectingToLogin;
    bool _refreshing;

    /// <summary>Por qué terminó un refresh. Distinguir Retryable de Dead es lo que
    /// evita expulsar a medio salón cuando 25 celulares refrescan a la vez y el
    /// servidor responde 429.</summary>
    enum RefreshResult { Ok, Retryable, Dead }

    RefreshResult _lastRefreshResult;
    int    _lastRefreshCode;
    string _lastRefreshDetail = "";

    const int   REFRESH_MAX_ATTEMPTS    = 3;
    const float REFRESH_BACKOFF_SECONDS = 1f;

    IEnumerator SendAuthed(Func<UnityWebRequest> build, Action<string> onSuccess, Action<int, string> onError)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            using var req = build();
            if (!string.IsNullOrEmpty(SessionData.AccessToken))
                req.SetRequestHeader("Authorization", "Bearer " + SessionData.AccessToken);
            req.timeout = 10; // evita conexiones colgadas indefinidamente (especialmente importante en celular)

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
                var refresh = RefreshResult.Dead;
                yield return RefreshTokenRoutine(r => refresh = r);

                if (refresh == RefreshResult.Ok) continue;   // reintenta con el token nuevo

                if (refresh == RefreshResult.Dead)
                {
                    EndSession();
                    onError?.Invoke(code, detail);
                }
                else
                {
                    // Temporal (429 o red): el refresh token sigue siendo bueno, solo no
                    // pudimos canjearlo ahora. Se reporta el fallo REAL para que el caller
                    // reintente, y la sesión NO se cierra.
                    Debug.LogWarning("[API] Refresh no disponible ahora (temporal). La sesión se mantiene.");
                    onError?.Invoke(_lastRefreshCode, _lastRefreshDetail);
                }
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
    // Ante un fallo temporal espera y reintenta antes de rendirse.
    IEnumerator RefreshTokenRoutine(Action<RefreshResult> done)
    {
        if (_refreshing)
        {
            while (_refreshing) yield return null;
            done(_lastRefreshResult);
            yield break;
        }

        string rt = SessionData.RefreshToken;
        if (string.IsNullOrEmpty(rt)) { done(RefreshResult.Dead); yield break; }

        _refreshing = true;
        _lastRefreshResult = RefreshResult.Dead;
        _lastRefreshCode   = 0;
        _lastRefreshDetail = "";

        string body = JsonUtility.ToJson(new RefreshRequest { refreshToken = rt });

        for (int attempt = 0; attempt < REFRESH_MAX_ATTEMPTS; attempt++)
        {
            if (attempt > 0)
            {
                // Backoff con jitter: si 25 celulares chocan con el mismo 429, reintentar
                // todos en el mismo instante los vuelve a chocar. El jitter los desparrama.
                float wait = REFRESH_BACKOFF_SECONDS * Mathf.Pow(2f, attempt - 1);
                yield return new WaitForSeconds(wait * UnityEngine.Random.Range(0.6f, 1.4f));
            }

            using var req = new UnityWebRequest(BASE_URL + "/session/refresh", "POST");
            req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Accept", "application/json");
            req.timeout = 10; // evita conexiones colgadas indefinidamente

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var resp = JsonUtility.FromJson<LoginResponse>(req.downloadHandler.text);
                // El refresh token es deslizante: puede venir uno nuevo (o no).
                string newRefresh   = string.IsNullOrEmpty(resp.refreshToken) ? rt : resp.refreshToken;
                string newTokenType = string.IsNullOrEmpty(resp.tokenType) ? SessionData.TokenType : resp.tokenType;
                SessionData.SetTokens(resp.accessToken, newRefresh, newTokenType, resp.expiresIn);
                SessionData.SetRole(resp.role);

                if (!string.IsNullOrEmpty(resp.accessToken))
                {
                    _lastRefreshResult  = RefreshResult.Ok;
                    _redirectingToLogin = false;
                    Debug.Log("[API] Access token refrescado.");
                }
                else
                {
                    // 200 sin token: no se arregla reintentando.
                    _lastRefreshResult = RefreshResult.Dead;
                    Debug.LogWarning("[API] Refresh devolvió 200 sin accessToken → sesión muerta.");
                }
                break;
            }

            _lastRefreshCode   = (int)req.responseCode;
            _lastRefreshDetail = TryParseDetail(req.downloadHandler != null ? req.downloadHandler.text : "");

            if (IsTemporaryFailure(_lastRefreshCode, _lastRefreshDetail))
            {
                _lastRefreshResult = RefreshResult.Retryable;
                Debug.LogWarning($"[API] Refresh temporal · code={_lastRefreshCode} · detail={_lastRefreshDetail} · intento {attempt + 1}/{REFRESH_MAX_ATTEMPTS}");
                continue;
            }

            _lastRefreshResult = RefreshResult.Dead;
            Debug.LogWarning($"[API] Refresh rechazado · code={_lastRefreshCode} · detail={_lastRefreshDetail} → sesión muerta.");
            break;
        }

        _refreshing = false;
        done(_lastRefreshResult);
    }

    /// <summary>El refresh token sigue siendo válido; solo no pudimos canjearlo ahora.
    /// 429 = límite de peticiones (un salón entero detrás de una sola IP), code 0 = red
    /// caída o timeout, 5xx = el servidor se cayó. Nada de eso es culpa de la sesión.</summary>
    static bool IsTemporaryFailure(int code, string detail)
        => code == 429 || detail == "ERR_RATE_LIMITED" || code == 0 || code >= 500;

    // Sesión muerta: limpiar y volver al login (una sola vez, aunque fallen varias peticiones).
    // Solo se llama cuando el servidor RECHAZA las credenciales, nunca por un 429 ni por
    // falta de red: expulsar por eso vaciaría el salón a mitad de la clase.
    void EndSession()
    {
        if (_redirectingToLogin) return;
        _redirectingToLogin = true;
        Debug.LogWarning("[API] Sesión finalizada (credenciales rechazadas) → LoginScene.");
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
    [Serializable] class CreateClassRequest { public string name; public string section; public int studentCount; }
    [Serializable] class AddStudentsRequest { public int count; }
    [Serializable] class InterpretCommandRequest
    {
        // El contrato lo llama "prompt" aquí y "promptText" en /apply. Si no coincide
        // exacto, el servidor recibe una cadena vacía y no hay error que lo delate.
        public string prompt;
        public int    baseVersion;
    }

    [Serializable] class ApplyCommandRequest
    {
        public string actionsJson;   // JSON dentro de un string; JsonUtility lo escapa solo
        public int    baseVersion;
        public string source;
        public string promptText;
    }
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
        public string role;          // "student" | "teacher"; llega en login Y en refresh
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

        /// <summary>Lo que el alumno conectó: SE DIBUJA SIEMPRE, sea la molécula válida
        /// o no. Mientras está incompleta todos vienen simples (dos C solos no tienen
        /// orden real todavía: con 6 H son etano, con 4 eteno). Nunca llega null.</summary>
        public BondDTO[]   bonds;

        /// <summary>Solo si isValid: fórmula, propiedades y descubrimiento. null si la
        /// molécula está a medias.</summary>
        public MoleculeDTO molecule;
    }
}
