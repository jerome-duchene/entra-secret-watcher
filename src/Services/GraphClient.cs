using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using EntraSecretWatcher.Configuration;
using Microsoft.Extensions.Options;

namespace EntraSecretWatcher.Services;

/// <summary>
/// Minimal Microsoft Graph client (client-credentials flow over plain HTTP) covering the two calls we need.
/// No retry handler on purpose: SendMail is not idempotent, a retry could send a duplicate email.
/// </summary>
internal class GraphClient(HttpClient http, IOptions<EntraOptions> options)
{
    private const string BaseUrl = "https://graph.microsoft.com/v1.0";
    private readonly EntraOptions _entra = options.Value;

    public async IAsyncEnumerable<GraphApplication> GetApplicationsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        string? url = $"{BaseUrl}/applications?$select=displayName,appId,passwordCredentials,keyCredentials&$top=999";
        while (url is not null)
        {
            var page = await SendAsync<GraphPage>(HttpMethod.Get, url, null, ct);
            foreach (var app in page!.Value)
                yield return app;
            url = page.NextLink;
        }
    }

    public Task SendMailAsync(string from, string subject, string html, IEnumerable<string> to, CancellationToken ct = default) =>
        SendAsync<object>(HttpMethod.Post, $"{BaseUrl}/users/{Uri.EscapeDataString(from)}/sendMail", new
        {
            message = new
            {
                subject,
                body = new { contentType = "HTML", content = html },
                toRecipients = to.Select(address => new { emailAddress = new { address } })
            },
            saveToSentItems = false
        }, ct);

    private async Task<T?> SendAsync<T>(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync(ct));
        if (body is not null)
            request.Content = JsonContent.Create(body);

        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Graph {method} failed: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(ct)}");

        return response.Content.Headers.ContentLength is 0 ? default : await response.Content.ReadFromJsonAsync<T>(ct);
    }

    private async Task<string> GetTokenAsync(CancellationToken ct)
    {
        using var response = await http.PostAsync(
            $"https://login.microsoftonline.com/{Uri.EscapeDataString(_entra.TenantId)}/oauth2/v2.0/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _entra.ClientId,
                ["client_secret"] = _entra.ClientSecret,
                ["scope"] = "https://graph.microsoft.com/.default"
            }), ct);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Token request failed: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(ct)}");

        return (await response.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("access_token").GetString()!;
    }
}

internal record GraphPage(List<GraphApplication> Value, [property: JsonPropertyName("@odata.nextLink")] string? NextLink);

internal record GraphApplication(
    string? DisplayName, string? AppId, List<GraphCredential>? PasswordCredentials, List<GraphCredential>? KeyCredentials);

internal record GraphCredential(Guid? KeyId, string? DisplayName, DateTimeOffset? EndDateTime);
