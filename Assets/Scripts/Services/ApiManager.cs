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
        StartCoroutine(GetRaw($"/users/profile/{publicId}", publicId,
            onSuccess: json => onSuccess?.Invoke(JsonUtility.FromJson<ProfileResponse>(json)),
            onError: onError));
    }

    public void Login(string email, string password,
                      Action<LoginResponse> onSuccess, Action<int, string> onError)
    {
        string body = JsonUtility.ToJson(new LoginRequest { email = email, password = password });

        StartCoroutine(PostRaw("/session/login", body,
            onSuccess: json =>
            {
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
        StartCoroutine(PatchRaw(endpoint, userPublicId, null,
            json => onSuccess?.Invoke(JsonUtility.FromJson<JournalStatsResponse>(json)),
            onError));
    }

    public void DecrementCreatedUniverse(string userPublicId,
                                         Action<JournalStatsResponse> onSuccess, Action<int, string> onError)
    {
        string endpoint = $"/journal/{userPublicId}/created-universes/decrement";
        Debug.Log($"[API] PATCH {BASE_URL}{endpoint}");
        StartCoroutine(PatchRaw(endpoint, userPublicId, null,
            json => onSuccess?.Invoke(JsonUtility.FromJson<JournalStatsResponse>(json)),
            onError));
    }

    /// <summary>Suma 'seconds' al tiempo total jugado de la cuenta (endpoint incremental).</summary>
    public void AddTimePlayed(string userPublicId, int seconds,
                              Action<JournalStatsResponse> onSuccess, Action<int, string> onError)
    {
        string endpoint = $"/journal/{userPublicId}/time-played";
        string body = JsonUtility.ToJson(new TimePlayedRequest { seconds = seconds });
        Debug.Log($"[API] PATCH {BASE_URL}{endpoint}\n{body}");
        StartCoroutine(PatchRaw(endpoint, userPublicId, body,
            json => onSuccess?.Invoke(JsonUtility.FromJson<JournalStatsResponse>(json)),
            onError));
    }

    public void DetectMolecule(string userPublicId, AtomDTO[] atoms, BondDTO[] bonds,
                               Action<DetectResponse> onSuccess, Action<int, string> onError)
    {
        string body = JsonUtility.ToJson(new DetectRequest
        {
            userPublicId = userPublicId, atoms = atoms, bonds = bonds
        });
        Debug.Log($"[API] POST {BASE_URL}/detection/molecule\n{body}");

        StartCoroutine(PostRaw("/detection/molecule", body,
            onSuccess: json => { Debug.Log($"[API] respuesta OK:\n{json}"); onSuccess?.Invoke(JsonUtility.FromJson<DetectResponse>(json)); },
            onError: onError));
    }

    // ── Core HTTP ─────────────────────────────────────────────────────────────

    // Variante que entrega el campo "message" ya parseado.
    IEnumerator Post(string endpoint, string jsonBody, Action<string> onSuccess, Action<int, string> onError)
    {
        return PostRaw(endpoint, jsonBody,
            json => onSuccess?.Invoke(JsonUtility.FromJson<MessageResponse>(json).message),
            onError);
    }

    // GET crudo. Envía el public_id como header (según contrato del backend).
    IEnumerator GetRaw(string endpoint, string publicIdHeader, Action<string> onSuccess, Action<int, string> onError)
    {
        string url = BASE_URL + endpoint;

        using var req = UnityWebRequest.Get(url);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Accept", "application/json");
        if (!string.IsNullOrEmpty(publicIdHeader))
            req.SetRequestHeader("public_id", publicIdHeader);

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
            Debug.LogWarning($"[API] GET {url} FALLÓ · result={req.result} · code={code} · error='{req.error}' · body='{responseText}'");
            onError?.Invoke(code, detail);
        }
    }

    // PATCH crudo. Envía el user_public_id como header; el body es opcional.
    IEnumerator PatchRaw(string endpoint, string userPublicIdHeader, string jsonBody,
                         Action<string> onSuccess, Action<int, string> onError)
    {
        string url = BASE_URL + endpoint;

        using var req = new UnityWebRequest(url, "PATCH");
        if (!string.IsNullOrEmpty(jsonBody))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            req.SetRequestHeader("Content-Type", "application/json");
        }
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Accept", "application/json");
        if (!string.IsNullOrEmpty(userPublicIdHeader))
            req.SetRequestHeader("user_public_id", userPublicIdHeader);

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
            Debug.LogWarning($"[API] PATCH {url} FALLÓ · result={req.result} · code={code} · error='{req.error}' · body='{responseText}'");
            onError?.Invoke(code, detail);
        }
    }

    // Variante que entrega el cuerpo crudo (JSON) para que el caller lo parsee.
    IEnumerator PostRaw(string endpoint, string jsonBody, Action<string> onSuccess, Action<int, string> onError)
    {
        string url = BASE_URL + endpoint;
        byte[] raw = Encoding.UTF8.GetBytes(jsonBody);

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler   = new UploadHandlerRaw(raw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Accept", "application/json");

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
    [Serializable] class DetectRequest  { public string userPublicId; public AtomDTO[] atoms; public BondDTO[] bonds; }

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
