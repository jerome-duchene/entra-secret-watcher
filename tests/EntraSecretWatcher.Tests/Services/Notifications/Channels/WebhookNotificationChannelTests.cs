using System.Net;
using System.Text.Json;
using EntraSecretWatcher.Configuration;
using EntraSecretWatcher.Models;
using EntraSecretWatcher.Services.Notifications.Channels;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace EntraSecretWatcher.Tests.Services.Notifications.Channels;

public class WebhookNotificationChannelTests
{
    [Fact]
    public async Task SendAsync_PostsOneRequestPerCredential_WithKeyId()
    {
        var handler = new RecordingHandler();
        var channel = CreateChannel(handler, new WebhookOptions { Enabled = true, Url = "https://example.com/hook" });

        await channel.SendAsync(ScanResultWith(Credential("key-1", 5), Credential("key-2", -2)));

        handler.Bodies.Should().HaveCount(2);
        var first = JsonDocument.Parse(handler.Bodies[0]).RootElement;
        first.GetProperty("event").GetString().Should().Be("credential.expiring");
        first.GetProperty("tenantName").GetString().Should().Be("Contoso");
        var cred = first.GetProperty("credential");
        cred.GetProperty("keyId").GetString().Should().Be("key-1");
        cred.GetProperty("type").GetString().Should().Be("secret");
        cred.GetProperty("status").GetString().Should().Be("expiringSoon");
        cred.GetProperty("daysLeft").GetInt32().Should().Be(5);
        JsonDocument.Parse(handler.Bodies[1]).RootElement
            .GetProperty("credential").GetProperty("status").GetString().Should().Be("expired");
    }

    [Fact]
    public async Task SendAsync_WithSecret_SendsConfiguredHeader()
    {
        var handler = new RecordingHandler();
        var channel = CreateChannel(handler, new WebhookOptions
        {
            Enabled = true, Url = "https://example.com/hook", Secret = "s3cret", SecretHeaderName = "X-Automation-Webhook-Token"
        });

        await channel.SendAsync(ScanResultWith(Credential("key-1", 5)));

        handler.Requests.Single().Headers.GetValues("X-Automation-Webhook-Token").Should().Equal("s3cret");
    }

    [Fact]
    public async Task SendAsync_WithoutSecret_SendsNoSecretHeader()
    {
        var handler = new RecordingHandler();
        var channel = CreateChannel(handler, new WebhookOptions { Enabled = true, Url = "https://example.com/hook" });

        await channel.SendAsync(ScanResultWith(Credential("key-1", 5)));

        handler.Requests.Single().Headers.Contains(WebhookOptions.DefaultSecretHeaderName).Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_OneCallFails_OthersAreStillSent_AndExceptionIsThrown()
    {
        var handler = new RecordingHandler(failOnCall: 1);
        var channel = CreateChannel(handler, new WebhookOptions { Enabled = true, Url = "https://example.com/hook" });

        var act = () => channel.SendAsync(ScanResultWith(Credential("a", 5), Credential("b", 6), Credential("c", 7)));

        await act.Should().ThrowAsync<AggregateException>();
        handler.Bodies.Should().HaveCount(3);
    }

    [Fact]
    public async Task SendAsync_RejectedCall_ReportsTheResponseBodyOfTheReceiver()
    {
        var handler = new RecordingHandler(failOnCall: 0, failureBody: "{\"errorMessages\":[\"Missing token\"]}");
        var channel = CreateChannel(handler, new WebhookOptions { Enabled = true, Url = "https://example.com/hook" });

        var act = () => channel.SendAsync(ScanResultWith(Credential("a", 5)));

        var thrown = await act.Should().ThrowAsync<AggregateException>();
        thrown.Which.InnerExceptions.Single().Message
            .Should().Contain("500").And.Contain("https://example.com/hook").And.Contain("Missing token");
    }

    [Fact]
    public void IsEnabled_ReflectsOptions()
    {
        var handler = new RecordingHandler();
        CreateChannel(handler, new WebhookOptions { Enabled = true, Url = "https://x" }).IsEnabled.Should().BeTrue();
        CreateChannel(handler, new WebhookOptions { Enabled = false }).IsEnabled.Should().BeFalse();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static WebhookNotificationChannel CreateChannel(RecordingHandler handler, WebhookOptions webhook)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler, disposeHandler: false));
        return new WebhookNotificationChannel(
            Options.Create(new NotificationOptions { Webhook = webhook }),
            factory,
            Substitute.For<ILogger<WebhookNotificationChannel>>());
    }

    private static ExpiringCredential Credential(string keyId, int daysLeft) => new()
    {
        AppName = "App",
        AppId = "00000000-0000-0000-0000-000000000001",
        KeyId = keyId,
        Type = CredentialType.Secret,
        CredentialName = "secret",
        ExpiresOn = DateTimeOffset.UtcNow.AddDays(daysLeft),
        DaysLeft = daysLeft
    };

    private static ScanResult ScanResultWith(params ExpiringCredential[] credentials) => new()
    {
        TenantName = "Contoso",
        TenantId = "tenant-id",
        ScannedAt = DateTimeOffset.UtcNow,
        TotalApplicationsScanned = 3,
        Credentials = credentials.ToList().AsReadOnly()
    };

    private sealed class RecordingHandler(int? failOnCall = null, string failureBody = "") : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            Bodies.Add(await request.Content!.ReadAsStringAsync(ct));
            return Requests.Count - 1 == failOnCall
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent(failureBody) }
                : new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
