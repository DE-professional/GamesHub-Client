using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// The result of one <see cref="ApiClient"/> request. Requests never throw for HTTP,
/// network, or JSON failures; check <see cref="Ok"/> or <see cref="Outcome"/> instead.
/// </summary>
/// <typeparam name="T">
/// The type the response body is parsed into. Must be a <c>[Serializable]</c> class or
/// struct with public fields, as required by <see cref="JsonUtility"/>.
/// </typeparam>
public readonly struct ApiResult<T>
{
    /// <summary>Which kind of result this is.</summary>
    public readonly ApiOutcome Outcome;

    /// <summary>The HTTP response code, or 0 when <see cref="Outcome"/> is <see cref="ApiOutcome.NetworkError"/>.</summary>
    public readonly long Status;

    /// <summary>
    /// The parsed response body. Only meaningful when <see cref="Ok"/> is true; otherwise
    /// <c>default(T)</c> (null for classes). Also <c>default(T)</c> on success when the body was empty.
    /// </summary>
    public readonly T Data;

    /// <summary>True when <see cref="Outcome"/> is <see cref="ApiOutcome.Success"/>.</summary>
    public bool Ok => Outcome == ApiOutcome.Success;


    ApiResult(ApiOutcome outcome, long status, T data)
    {   
        Outcome = outcome;
        Status = status;
        Data = data;
    }

    /// <summary>Creates a successful result.</summary>
    /// <param name="status">HTTP response code (2xx).</param>
    /// <param name="data">Parsed body, or <c>default</c> if the body was empty.</param>
    public static ApiResult<T> Success(long status, T data) => new(ApiOutcome.Success,status,data);

    /// <summary>Creates an <see cref="ApiOutcome.HttpError"/> result.</summary>
    /// <param name="status">HTTP response code returned by the server.</param>
    public static ApiResult<T> Fail(long status) => new (ApiOutcome.HttpError,status,default);

    /// <summary>Creates an <see cref="ApiOutcome.ParseError"/> result for a 2xx response whose body was not valid JSON.</summary>
    /// <param name="status">HTTP response code returned by the server.</param>
    public static ApiResult<T> ParseError(long status) => new (ApiOutcome.ParseError, status, default);

    /// <summary>Creates an <see cref="ApiOutcome.NetworkError"/> result with status 0.</summary>
    public static ApiResult<T> Network() => new (ApiOutcome.NetworkError,0,default);
}

/// <summary>The kinds of result an <see cref="ApiClient"/> request can produce.</summary>
public enum ApiOutcome
{
    /// <summary>The server answered 2xx and the body, if any, parsed.</summary>
    Success,

    /// <summary>The server answered with an error code, or Unity could not process the response.</summary>
    HttpError,

    /// <summary>No response arrived: offline, DNS failure, refused connection, or the 15-second timeout.</summary>
    NetworkError,

    /// <summary>The server answered 2xx but the body was not valid JSON.</summary>
    ParseError
}

/// <summary>
/// Sends JSON requests to the GamesHub backend with <see cref="UnityWebRequest"/> and
/// returns each response as an <see cref="ApiResult{T}"/>.
/// </summary>
/// <remarks>
/// <para>Bodies are serialized and responses parsed with <see cref="JsonUtility"/>.</para>
/// <para>
/// The returned <see cref="Awaitable{T}"/> instances are pooled by Unity: await each one
/// exactly once and do not store it. Continuations resume on the main thread.
/// </para>
/// </remarks>
public class ApiClient
{
    readonly string baseUrl;

    /// <summary>Creates a client for the given server.</summary>
    /// <param name="baseUrl">
    /// Server root, e.g. <c>http://localhost:3900</c>. A trailing <c>/</c> is removed, so
    /// paths passed to the request methods must start with <c>/</c>.
    /// </param>
    public ApiClient(string baseUrl) => this.baseUrl = baseUrl.TrimEnd('/');

    /// <summary>
    /// Bearer token. When non-empty it is sent as <c>Authorization: Bearer &lt;token&gt;</c>
    /// on every request made with <c>authenticate</c> set to true. Set to null to send all
    /// requests unauthenticated.
    /// </summary>
    public string AuthToken { get; set; }

    /// <summary>
    /// Raised when a request that carried <see cref="AuthToken"/> receives HTTP 401.
    /// The request still returns an <see cref="ApiOutcome.HttpError"/> result with status 401.
    /// Requests made with <c>authenticate: false</c> never raise it.
    /// </summary>
    public event Action Unauthorized;

    /// <summary>Sends a GET request.</summary>
    /// <typeparam name="T">Type to parse the response body into.</typeparam>
    /// <param name="path">Path relative to the base URL, starting with <c>/</c>.</param>
    /// <param name="authenticate">When false, <see cref="AuthToken"/> is not sent even if set.</param>
    public Awaitable<ApiResult<T>> Get<T>(string path, bool authenticate = true) => Send<T>("GET", path, null, authenticate);

    /// <summary>Sends a POST request with a JSON body.</summary>
    /// <typeparam name="T">Type to parse the response body into.</typeparam>
    /// <param name="path">Path relative to the base URL, starting with <c>/</c>.</param>
    /// <param name="body">A <c>[Serializable]</c> object; serialized with <see cref="JsonUtility.ToJson(object)"/>. Null sends no body.</param>
    /// <param name="authenticate">When false, <see cref="AuthToken"/> is not sent even if set. Use for login and registration.</param>
    public Awaitable<ApiResult<T>> Post<T>(string path, object body, bool authenticate = true) => Send<T>("POST", path,body, authenticate);

    /// <summary>Sends a PUT request with a JSON body.</summary>
    /// <typeparam name="T">Type to parse the response body into.</typeparam>
    /// <param name="path">Path relative to the base URL, starting with <c>/</c>.</param>
    /// <param name="body">A <c>[Serializable]</c> object; serialized with <see cref="JsonUtility.ToJson(object)"/>. Null sends no body.</param>
    /// <param name="authenticate">When false, <see cref="AuthToken"/> is not sent even if set.</param>
    public Awaitable<ApiResult<T>> Put<T>(string path, object body, bool authenticate = true) => Send<T>("PUT", path,body, authenticate);

    /// <summary>Sends a DELETE request.</summary>
    /// <typeparam name="T">Type to parse the response body into.</typeparam>
    /// <param name="path">Path relative to the base URL, starting with <c>/</c>.</param>
    /// <param name="authenticate">When false, <see cref="AuthToken"/> is not sent even if set.</param>
    public Awaitable<ApiResult<T>> Delete<T>(string path, bool authenticate = true) => Send<T>("DELETE", path, null, authenticate);
    
    /// <summary>
    /// Builds, sends, and interprets one request. Shared by all public request methods.
    /// </summary>
    /// <remarks>
    /// Result mapping:
    /// <list type="bullet">
    /// <item><description>Connection error or timeout → <see cref="ApiOutcome.NetworkError"/>, status 0.</description></item>
    /// <item><description>Any other failed result (4xx/5xx, data-processing error) → <see cref="ApiOutcome.HttpError"/>.
    /// A 401 on a request that carried a token also raises <see cref="Unauthorized"/>.</description></item>
    /// <item><description>Success with empty body → <see cref="ApiOutcome.Success"/>, <c>Data = default</c>.</description></item>
    /// <item><description>Success with invalid JSON → <see cref="ApiOutcome.ParseError"/> (a warning is logged).</description></item>
    /// <item><description>Success with valid JSON → <see cref="ApiOutcome.Success"/> with parsed data.</description></item>
    /// </list>
    /// Exceptions thrown by <see cref="UnityWebRequest.SendWebRequest"/> itself are not caught.
    /// </remarks>
    async Awaitable<ApiResult<T>> Send<T>(string method, string path, object body, bool authenticate = true)
    {
        using var req = new UnityWebRequest(baseUrl + path, method);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.timeout = 15;

        if (body != null)
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
            req.SetRequestHeader("Content-Type", "application/json");
        }

        bool hadToken = authenticate && !string.IsNullOrEmpty(AuthToken);
        if (hadToken) req.SetRequestHeader("Authorization","Bearer " + AuthToken);
        await req.SendWebRequest();
        if (req.result == UnityWebRequest.Result.ConnectionError)
            return ApiResult<T>.Network();

        // Only a rejected token counts as "unauthorized"; a 401 on a request sent without a
        // token (e.g. wrong password at login) is left for the caller to interpret.
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
