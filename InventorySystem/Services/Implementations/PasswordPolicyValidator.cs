using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using InventorySystem.Configuration;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Services.Implementations
{
    public class PasswordPolicyValidator : IPasswordPolicyValidator
    {
        private readonly PasswordPolicyOptions _options;

        public PasswordPolicyValidator(IOptions<SecuritySettings> securitySettings)
        {
            _options = securitySettings.Value.PasswordPolicy;
        }

        public IReadOnlyList<string> Validate(string password)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(password))
            {
                errors.Add("Password is required.");
                return errors;
            }

            if (password.Length < _options.MinimumLength)
            {
                errors.Add($"Password must be at least {_options.MinimumLength} characters.");
            }

            if (_options.RequireUppercase && !password.Any(char.IsUpper))
            {
                errors.Add("Password must contain at least one uppercase letter.");
            }

            if (_options.RequireLowercase && !password.Any(char.IsLower))
            {
                errors.Add("Password must contain at least one lowercase letter.");
            }

            if (_options.RequireDigit && !password.Any(char.IsDigit))
            {
                errors.Add("Password must contain at least one digit.");
            }

            if (_options.RequireNonAlphanumeric && password.All(char.IsLetterOrDigit))
            {
                errors.Add("Password must contain at least one special character.");
            }

            return errors;
        }

        public string GetRequirementsDescription()
        {
            var parts = new List<string> { $"at least {_options.MinimumLength} characters" };

            if (_options.RequireUppercase)
            {
                parts.Add("one uppercase letter");
            }

            if (_options.RequireLowercase)
            {
                parts.Add("one lowercase letter");
            }

            if (_options.RequireDigit)
            {
                parts.Add("one digit");
            }

            if (_options.RequireNonAlphanumeric)
            {
                parts.Add("one special character");
            }

            return "Password must include " + string.Join(", ", parts) + ".";
        }
    }
}
