using UnityEngine;

public class ApiClientSmokeTest : MonoBehaviour
{
    [System.Serializable] class Empty { }

    async void Start()
    {
        var api = new ApiClient("http://localhost:3900");
        int unauthorized = 0;
        api.Unauthorized += () => unauthorized++;

        Log("unknown game", await api.Get<Empty>("/games/nope"));     // HttpError 404 game_not_found
        Log("bad slug",     await api.Get<Empty>("/games/Bad_Slug")); // HttpError 400 invalid_slug
        Log("no route",     await api.Get<Empty>("/nope"));           // HttpError 404 route_not_found
        Log("no token",     await api.Get<Empty>("/auth/me"));        // HttpError 401 authentication_required

        api.AuthToken = new string('a', 43);                           // well-formed, unknown
        Log("bad token",    await api.Get<Empty>("/auth/me"));        // HttpError 401 invalid_token
        Debug.Log($"Unauthorized raised {unauthorized} time(s); expected 1");

        var pending = api.Get<Empty>("/auth/me");  // sent with the 'a' token
        api.AuthToken = new string('b', 43);       // simulate a new login while in flight
        Log("stale token",  await pending);        // HttpError 401 invalid_token
        Debug.Log($"Unauthorized raised {unauthorized} time(s); expected still 1");
    }

    static void Log<T>(string label, ApiResult<T> r) =>
        Debug.Log($"{label}: {r.Outcome} {r.Status} {r.ErrorCode ?? "(no code)"}");
}