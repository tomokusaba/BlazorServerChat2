using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace BlazorServerChat2.Client;

/// <summary>
/// HttpClient の拡張メソッド（WASM用に認証Cookieを含める）
/// </summary>
public static class HttpClientExtensions
{
    /// <summary>
    /// 認証Cookie付きでGETリクエストを送信
    /// </summary>
    public static async Task<T?> GetWithCredentialsAsync<T>(this HttpClient client, string requestUri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<T>();
    }

    /// <summary>
    /// 認証Cookie付きでPOSTリクエストを送信し、レスポンスをデシリアライズして返す
    /// </summary>
    public static async Task<TResponse?> PostWithCredentialsAsync<TResponse>(this HttpClient client, string requestUri, object content)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        request.Content = JsonContent.Create(content);
        
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<TResponse>();
    }

    /// <summary>
    /// 認証Cookie付きでPUTリクエストを送信し、レスポンスをデシリアライズして返す
    /// </summary>
    public static async Task<TResponse?> PutWithCredentialsAsync<TResponse>(this HttpClient client, string requestUri, object content)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, requestUri);
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        request.Content = JsonContent.Create(content);
        
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<TResponse>();
    }
}
