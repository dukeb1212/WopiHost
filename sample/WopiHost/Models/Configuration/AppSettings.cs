namespace WopiHost.Models.Configuration;

public class FileStorageSettings
{
    public string RootPath { get; set; } = string.Empty;
}

public class WopiDiscoverySettings
{
    public string Url { get; set; } = string.Empty;
}

public class JwtSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 60;
}
