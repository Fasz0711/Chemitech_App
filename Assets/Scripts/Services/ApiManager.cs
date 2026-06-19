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
    [Serializable] class MessageResponse { public string message; }
    [Serializable] class DetailResponse  { public string detail; }
}
