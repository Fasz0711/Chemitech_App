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

    // ── Core HTTP ─────────────────────────────────────────────────────────────

    IEnumerator Post(string endpoint, string jsonBody, Action<string> onSuccess, Action<int, string> onError)
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
            var resp = JsonUtility.FromJson<MessageResponse>(responseText);
            onSuccess?.Invoke(resp.message);
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

    [Serializable] class EmailRequest   { public string email; }
    [Serializable] class MessageResponse { public string message; }
    [Serializable] class DetailResponse  { public string detail; }
}
