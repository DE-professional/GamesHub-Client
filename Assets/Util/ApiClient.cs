using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public readonly struct ApiResult<T>
{
    public readonly ApiOutcome Outcome;
    public readonly long Status;
    public readonly T Data;
    public bool Ok => Outcome == ApiOutcome.Success;


    ApiResult(ApiOutcome outcome, long status, T data)
    {   
        Outcome = outcome;
        Status = status;
        Data = data;
    }
    public static ApiResult<T> Success(long status, T data) => new(ApiOutcome.Success,status,data);
    public static ApiResult<T> Fail(long status) => new (ApiOutcome.HttpError,status,default);
    public static ApiResult<T> ParseError(long status) => new (ApiOutcome.ParseError, status, default);
    public static ApiResult<T> Network() => new (ApiOutcome.NetworkError,0,default);
}

public enum ApiOutcome
{
    Success,
    HttpError,
    NetworkError,
    ParseError
}

public class ApiClient
{
    readonly string baseUrl;
    public ApiClient(string baseUrl) => this.baseUrl = baseUrl.TrimEnd('/');
    public string AuthToken { get; set; }
    public event Action Unauthorized;

    public Awaitable<ApiResult<T>> Get<T>(string path) => Send<T>("GET", path, null);
    public Awaitable<ApiResult<T>> Post<T>(string path, object body) => Send<T>("POST", path,body);
    public Awaitable<ApiResult<T>> Put<T>(string path, object body) => Send<T>("PUT", path,body);
    public Awaitable<ApiResult<T>> Delete<T>(string path) => Send<T>("DELETE", path, null);
    
    async Awaitable<ApiResult<T>> Send<T>(string method, string path, object body)
    {
        using var req = new UnityWebRequest(baseUrl + path, method);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.timeout = 15;

        if (body != null)
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
            req.SetRequestHeader("Content-Type", "application/json");
        }

        bool hadToken = !string.IsNullOrEmpty(AuthToken);
        if (hadToken) req.SetRequestHeader("Authorization","Bearer " + AuthToken);
        await req.SendWebRequest();
        if (req.result == UnityWebRequest.Result.ConnectionError)
            return ApiResult<T>.Network();

        if (req.responseCode == 401 && hadToken)
            Unauthorized?.Invoke();

        if (req.result != UnityWebRequest.Result.Success)
            return ApiResult<T>.Fail(req.responseCode);
        
        string text = req.downloadHandler.text;
        
        T data;
        if (string.IsNullOrEmpty(text))
        {
            data = default;
        }
        else
        {
            try
            {
                data = JsonUtility.FromJson<T>(text);
            }
            catch (ArgumentException e)
            {
                Debug.LogWarning($"Bad JSON from {method} {path}: {e.Message}");
                return ApiResult<T>.ParseError(req.responseCode);
            }
        }
        return ApiResult<T>.Success(req.responseCode, data);
    }
}
