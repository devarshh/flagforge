using System.Net.Http.Json;
using FlagForge.Application.Common;

namespace FlagForge.Testing;

/// <summary>JSON helpers that use the application's JSON contract.</summary>
public static class HttpJson
{
    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T body, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(url, body, JsonDefaults.Options, cancellationToken);

    public static Task<HttpResponseMessage> PutJsonAsync<T>(this HttpClient client, string url, T body, CancellationToken cancellationToken) =>
        client.PutAsJsonAsync(url, body, JsonDefaults.Options, cancellationToken);

    public static Task<HttpResponseMessage> PatchJsonAsync<T>(this HttpClient client, string url, T body, CancellationToken cancellationToken) =>
        client.PatchAsJsonAsync(url, body, JsonDefaults.Options, cancellationToken);

    public static Task<HttpResponseMessage> DeleteJsonAsync<T>(this HttpClient client, string url, T body, CancellationToken cancellationToken) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, url) { Content = JsonContent.Create(body, options: JsonDefaults.Options) }, cancellationToken);

    /// <summary>Reads the body as <typeparamref name="T"/>, failing with the response text when the status is not a success.</summary>
    public static async Task<T> ReadJsonAsync<T>(this HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"{(int)response.StatusCode} {response.StatusCode} from {response.RequestMessage?.RequestUri}: {body}");
        }

        return await response.Content.ReadFromJsonAsync<T>(JsonDefaults.Options, cancellationToken)
            ?? throw new InvalidOperationException("The response body was empty.");
    }
}
