using System.ComponentModel.DataAnnotations;
using EntraSecretWatcher.Configuration;
using FluentAssertions;

namespace EntraSecretWatcher.Tests.Configuration;

public class WatcherOptionsValidationTests
{
    [Fact]
    public void DefaultOptions_AreValid()
    {
        Validate(new WatcherOptions()).Should().BeEmpty();
    }

    [Fact]
    public void ManualTriggerSecret_Empty_IsValid_EndpointDisabled()
    {
        Validate(new WatcherOptions { ManualTriggerSecret = "" }).Should().BeEmpty();
    }

    [Fact]
    public void ManualTriggerSecret_LongEnough_IsValid()
    {
        Validate(new WatcherOptions { ManualTriggerSecret = new string('x', 16) }).Should().BeEmpty();
    }

    [Fact]
    public void ManualTriggerSecret_TooShort_FailsValidation()
    {
        Validate(new WatcherOptions { ManualTriggerSecret = "short" })
            .Should().ContainSingle(r => r.MemberNames.Contains(nameof(WatcherOptions.ManualTriggerSecret)));
    }

    [Fact]
    public void RunOnStartup_IsDisabledByDefault()
    {
        new WatcherOptions().RunOnStartup.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(366)]
    public void ThresholdDays_OutOfRange_FailsValidation(int days)
    {
        var errors = Validate(new WatcherOptions { ThresholdDays = days });
        errors.Should().ContainSingle(r => r.MemberNames.Contains(nameof(WatcherOptions.ThresholdDays)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(365)]
    public void ThresholdDays_InRange_IsValid(int days)
    {
        Validate(new WatcherOptions { ThresholdDays = days }).Should().BeEmpty();
    }

    [Fact]
    public void CronSchedule_WhenNull_FailsRequired()
    {
        var options = new WatcherOptions { CronSchedule = null! };
        var errors = Validate(options);
        errors.Should().ContainSingle(r => r.MemberNames.Contains(nameof(WatcherOptions.CronSchedule)));
    }

    [Fact]
    public void CronSchedule_WhenEmpty_FailsRequired()
    {
        var options = new WatcherOptions { CronSchedule = string.Empty };
        var errors = Validate(options);
        errors.Should().ContainSingle(r => r.MemberNames.Contains(nameof(WatcherOptions.CronSchedule)));
    }

    private static IList<ValidationResult> Validate<T>(T instance)
    {
        var ctx = new ValidationContext(instance!);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance!, ctx, results, validateAllProperties: true);
        return results;
    }
}
