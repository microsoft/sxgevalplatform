public class RateLimitingPolicyOptions
{
    public int PermitLimit { get; set; }
    public int WindowSeconds { get; set; }
}

public class RateLimitingOptions
{
    public RateLimitingPolicyOptions IpPolicy { get; set; }
    public RateLimitingPolicyOptions UserPolicy { get; set; }
    public RateLimitingPolicyOptions SessionPolicy { get; set; }
}
