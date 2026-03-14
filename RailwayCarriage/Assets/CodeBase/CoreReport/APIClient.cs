using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Collections.Generic;
using UnityEngine.Networking;
using UnityEngine;

public class APIClient : IAPIClient
{
    private readonly string _baseUrl;
    private readonly int _timeout;
    private readonly List<KeyValuePair<string, string>> _defaultHeaders;

    public APIClient(string baseUrl, int timeoutSeconds = 30)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _timeout = timeoutSeconds;
        _defaultHeaders = new List<KeyValuePair<string, string>>();

        AddHeader("Accept", "application/json");
    }

    public async Task<T> GetAsync<T>(string endpoint)
    {
        using var request = UnityWebRequest.Get($"{_baseUrl}/{endpoint}");
        return await SendRequest<T>(request);
    }

    public async Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest data)
    {
        var json = JsonUtility.ToJson(data);
        using var request = UnityWebRequest.Post($"{_baseUrl}/{endpoint}", "POST");

        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        return await SendRequest<TResponse>(request);
    }

    public async Task PostAsync<TRequest>(string endpoint, TRequest data)
    {
        var json = JsonUtility.ToJson(data);
        using var request = UnityWebRequest.Post($"{_baseUrl}/{endpoint}", "POST");

        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        await SendRequest(request);
    }

    public async Task<TResponse> PutAsync<TRequest, TResponse>(string endpoint, TRequest data)
    {
        var json = JsonUtility.ToJson(data);
        using var request = UnityWebRequest.Put($"{_baseUrl}/{endpoint}", json);
        request.SetRequestHeader("Content-Type", "application/json");

        return await SendRequest<TResponse>(request);
    }

    public async Task PutAsync<TRequest>(string endpoint, TRequest data)
    {
        var json = JsonUtility.ToJson(data);
        using var request = UnityWebRequest.Put($"{_baseUrl}/{endpoint}", json);
        request.SetRequestHeader("Content-Type", "application/json");

        await SendRequest(request);
    }

    public async Task DeleteAsync(string endpoint)
    {
        using var request = UnityWebRequest.Delete($"{_baseUrl}/{endpoint}");
        await SendRequest(request);
    }

    public void SetBearerToken(string token)
    {
        AddHeader("Authorization", $"Bearer {token}");
    }

    public void AddHeader(string name, string value)
    {
        _defaultHeaders.Add(new KeyValuePair<string, string>(name, value));
    }

    private async Task<T> SendRequest<T>(UnityWebRequest request)
    {
        var response = await SendRequestInternal(request);

        if (!string.IsNullOrEmpty(response))
        {
            return JsonUtility.FromJson<T>(response);
        }

        return default(T);
    }

    private async Task SendRequest(UnityWebRequest request)
    {
        await SendRequestInternal(request);
    }

    private async Task<string> SendRequestInternal(UnityWebRequest request)
    {
        foreach (var header in _defaultHeaders)
        {
            request.SetRequestHeader(header.Key, header.Value);
        }

        request.timeout = _timeout;

        var operation = request.SendWebRequest();

        while (!operation.isDone)
        {
            await Task.Yield();
        }

        if (request.result != UnityWebRequest.Result.Success)
        {
            throw new Exception($"HTTP Error: {request.error}\nResponse: {request.downloadHandler?.text}");
        }

        return request.downloadHandler?.text;
    }
}

