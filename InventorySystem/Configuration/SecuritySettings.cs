namespace InventorySystem.Configuration
{
    public class SecuritySettings
    {
        public const string SectionName = "SecuritySettings";

        public PasswordPolicyOptions PasswordPolicy { get; set; } = new();
        public AccountLockoutOptions AccountLockout { get; set; } = new();
        public LoginRateLimitOptions LoginRateLimit { get; set; } = new();
    }

    public class PasswordPolicyOptions
    {
        public int MinimumLength { get; set; } = 12;
        public bool RequireUppercase { get; set; } = true;
        public bool RequireLowercase { get; set; } = true;
        public bool RequireDigit { get; set; } = true;
        public bool RequireNonAlphanumeric { get; set; } = true;
    }

    public class AccountLockoutOptions
    {
        public int MaxFailedAttempts { get; set; } = 5;
        public int LockoutMinutes { get; set; } = 15;
    }

    public class LoginRateLimitOptions
    {
        public int PermitLimit { get; set; } = 10;
        public int WindowSeconds { get; set; } = 60;
    }
}
