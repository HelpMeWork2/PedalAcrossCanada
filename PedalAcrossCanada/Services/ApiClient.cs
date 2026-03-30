using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace PedalAcrossCanada.Services;

public class ApiClient(
    HttpClient httpClient,
    TokenService tokenService,
    AuthHttpService authHttpService)
{
    public async Task<T?> GetAsync<T>(string uri)
    {
        var response = await SendWithAuthAsync(HttpMethod.Get, uri);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }

    public async Task<HttpResponseMessage> PostAsync<T>(string uri, T data)
    {
        var response = await SendWithAuthAsync(HttpMethod.Post, uri, data);
        return response;
    }

    public async Task<HttpResponseMessage> PutAsync<T>(string uri, T data)
    {
        var response = await SendWithAuthAsync(HttpMethod.Put, uri, data);
        return response;
    }

    public async Task<HttpResponseMessage> DeleteAsync(string uri)
    {
        var response = await SendWithAuthAsync(HttpMethod.Delete, uri);
        return response;
    }

    private async Task<HttpResponseMessage> SendWithAuthAsync(HttpMethod method, string uri, object? content = null)
    {
        var request = CreateRequest(method, uri, content);
        var response = await httpClient.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var refreshed = await authHttpService.TryRefreshAsync();
            if (refreshed)
            {
                request = CreateRequest(method, uri, content);
                response = await httpClient.SendAsync(request);
            }
        }

        return response;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string uri, object? content = null)
    {
        var request = new HttpRequestMessage(method, uri);

        if (!string.IsNullOrEmpty(tokenService.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenService.AccessToken);
        }

        if (content is not null)
        {
            request.Content = JsonContent.Create(content);
        }

        return request;
    }
}
