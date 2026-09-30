namespace FlagForge.ManagementApi.IntegrationTests;

internal static class Cookies
{
    public const string Refresh = "ff_refresh";

    /// <summary>The raw Set-Cookie header for the refresh cookie, or null.</summary>
    public static string? SetCookieHeader(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith($"{Refresh}=", StringComparison.Ordinal))
            : null;

    /// <summary>The refresh token value set by the response, or null when the cookie was cleared or not set.</summary>
    public static string? RefreshToken(HttpResponseMessage response)
    {
        var header = SetCookieHeader(response);
        if (header is null)
        {
            return null;
        }

        var value = header[(Refresh.Length + 1)..].Split(';')[0];
        return value.Length == 0 ? null : value;
    }

    public static HttpRequestMessage WithRefreshCookie(HttpMethod method, string url, string? token)
    {
        var request = new HttpRequestMessage(method, url);
        if (token is not null)
        {
            request.Headers.Add("Cookie", $"{Refresh}={token}");
        }

        return request;
    }
}
