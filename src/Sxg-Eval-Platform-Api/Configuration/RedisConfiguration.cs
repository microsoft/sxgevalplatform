namespace SxgEvalPlatformApi.Configuration;

public class RedisConfiguration
{
    public string Hostname { get; set; } = string.Empty;
    public string? User { get; set; } // Optional User/Object ID for Microsoft Entra Authentication
    public CacheConfiguration Cache { get; set; } = new();
}

public class CacheConfiguration
{
    public string KeyPrefix { get; set; } = string.Empty;
    public int DefaultTtlMinutes { get; set; } = 60;
    public int MaxMemoryMB { get; set; } = 500;
    public bool EnableCompression { get; set; } = false;
}