using EntraSecretWatcher.Services.Notifications.Channels;
using FluentAssertions;

namespace EntraSecretWatcher.Tests.Services.Notifications.Channels;

public class TeamsNotificationChannelTests
{
    // ── No special characters ──────────────────────────────────────────────────

    [Fact]
    public void EscapeJson_PlainString_ReturnsUnchanged()
    {
        TeamsNotificationChannel.EscapeJson("hello world").Should().Be("hello world");
    }

    [Fact]
    public void EscapeJson_EmptyString_ReturnsEmpty()
    {
        TeamsNotificationChannel.EscapeJson(string.Empty).Should().Be(string.Empty);
    }

    // ── Quote escaping ─────────────────────────────────────────────────────────

    [Fact]
    public void EscapeJson_DoubleQuote_IsEscaped()
    {
        TeamsNotificationChannel.EscapeJson("say \"hello\"").Should().Be(@"say \""hello\""");
    }

    // ── Backslash escaping ─────────────────────────────────────────────────────

    [Fact]
    public void EscapeJson_Backslash_IsEscaped()
    {
        TeamsNotificationChannel.EscapeJson(@"C:\path\file").Should().Be(@"C:\\path\\file");
    }

    [Fact]
    public void EscapeJson_Backslash_IsEscapedBeforeQuote()
    {
        // Backslash must be replaced BEFORE quote to avoid double-escaping
        // Input:   \"
        // Correct: \\\"
        // Wrong:   \\\"  (which is actually \\\" already, so this is fine)
        // The key: if we escaped " first we'd get \", then escaping \ gives \\"
        TeamsNotificationChannel.EscapeJson("\\\"")
            .Should().Be("\\\\\\\"");
    }

    // ── Newline / carriage return ──────────────────────────────────────────────

    [Fact]
    public void EscapeJson_Newline_IsEscaped()
    {
        TeamsNotificationChannel.EscapeJson("line1\nline2").Should().Be(@"line1\nline2");
    }

    [Fact]
    public void EscapeJson_CarriageReturn_IsEscaped()
    {
        TeamsNotificationChannel.EscapeJson("line1\rline2").Should().Be(@"line1\rline2");
    }

    [Fact]
    public void EscapeJson_CrLf_BothCharsAreEscaped()
    {
        TeamsNotificationChannel.EscapeJson("line1\r\nline2").Should().Be(@"line1\r\nline2");
    }

    // ── Combined cases ─────────────────────────────────────────────────────────

    [Fact]
    public void EscapeJson_AllSpecialChars_AreEscaped()
    {
        // Input: App "Name"\nDesc
        var input = "App \"Name\"\nDesc";
        var result = TeamsNotificationChannel.EscapeJson(input);
        result.Should().Be(@"App \""Name\""\nDesc");
    }

    [Fact]
    public void EscapeJson_UnicodeCharacters_ArePassedThrough()
    {
        // Emoji and unicode letters don't need JSON escaping in this implementation
        TeamsNotificationChannel.EscapeJson("App 🔐 Café")
            .Should().Be("App 🔐 Café");
    }
}
