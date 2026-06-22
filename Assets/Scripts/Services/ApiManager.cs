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

    // Variante que entrega el cuerpo crudo (JSON) para que el caller lo parsee.
    IEnumerator PostRaw(string endpoint, string jsonBody, Action<string> onSuccess, Action<int, string> onError)
    {
        string url = BASE_URL + endpoint;
        byte[] raw = Encoding.UTF8.GetBytes(jsonBody);

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler   = new UploadHandlerRaw(raw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

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
    [Serializable] class MessageResponse { public string message; }
    [Serializable] class DetailResponse  { public string detail; }

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
