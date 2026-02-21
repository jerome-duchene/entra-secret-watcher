namespace EntraSecretWatcher.Models;

internal enum CredentialType
{
    Secret,
    Certificate
}

internal enum CredentialStatus
{
    Expired,
    ExpiringSoon,
    Valid
}

internal record ExpiringCredential
{
    public required string AppName { get; init; }
    public required string AppId { get; init; }
    public required CredentialType Type { get; init; }
    public required string CredentialName { get; init; }
    public required DateTimeOffset ExpiresOn { get; init; }
    public required int DaysLeft { get; init; }

    public CredentialStatus Status => DaysLeft switch
    {
        < 0 => CredentialStatus.Expired,
        <= 30 => CredentialStatus.ExpiringSoon,
        _ => CredentialStatus.Valid
    };

    public string StatusLabel => Status switch
    {
        CredentialStatus.Expired => $"EXPIRED since {Math.Abs(DaysLeft)} day(s)",
        CredentialStatus.ExpiringSoon => $"Expires in {DaysLeft} day(s)",
        _ => "Valid"
    };
}

internal record ScanResult
{
    public required string TenantName { get; init; }
    public required string TenantId { get; init; }
    public required DateTimeOffset ScannedAt { get; init; }
    public required IReadOnlyList<ExpiringCredential> Credentials { get; init; }
    public int TotalApplicationsScanned { get; init; }

    public bool HasExpiring => Credentials.Count > 0;
    public int ExpiredCount => Credentials.Count(c => c.Status == CredentialStatus.Expired);
    public int ExpiringSoonCount => Credentials.Count(c => c.Status == CredentialStatus.ExpiringSoon);
}
