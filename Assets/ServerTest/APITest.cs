using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class APITest:MonoBehaviour
{
    public void OnButton()
    {
        String baseUrl = "http://localhost:3900";
        String regUrl = baseUrl + "/auth/register";
        String jsonBody =
            "{ \"email\": \"player@example.com\", \"password\": \"at least twelve chars\", \"displayName\": \"Player_1\" }";
        StartCoroutine(Post(regUrl, jsonBody, (res) =>
        {
            Debug.Log(res);
        }));
    }

    IEnumerator Post(string url, string bodyJsonString, Action<string> onComplete)
    {
        var request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(bodyJsonString);
        request.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        yield return request.SendWebRequest();
        onComplete?.Invoke(request.downloadHandler.text);
    }
}

