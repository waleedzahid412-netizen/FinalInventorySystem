using Microsoft.Extensions.Options;
using InventorySystem.Configuration;
using InventorySystem.Services.Implementations;
using Xunit;

namespace InventorySystem.Tests
{
    public class PasswordPolicyValidatorTests
    {
        private static PasswordPolicyValidator CreateValidator()
        {
            var settings = Options.Create(new SecuritySettings());
            return new PasswordPolicyValidator(settings);
        }

        [Fact]
        public void Validate_RejectsShortPassword()
        {
            var validator = CreateValidator();

            var errors = validator.Validate("Short1!");

            Assert.Contains(errors, e => e.Contains("at least 12 characters"));
        }

        [Fact]
        public void Validate_RejectsMissingUppercase()
        {
            var validator = CreateValidator();

            var errors = validator.Validate("securepass1!");

            Assert.Contains(errors, e => e.Contains("uppercase"));
        }

        [Fact]
        public void Validate_RejectsMissingSpecialCharacter()
        {
            var validator = CreateValidator();

            var errors = validator.Validate("SecurePass1234");

            Assert.Contains(errors, e => e.Contains("special character"));
        }

        [Fact]
        public void Validate_AcceptsCompliantPassword()
        {
            var validator = CreateValidator();

            var errors = validator.Validate("SecurePass1!");

            Assert.Empty(errors);
        }

        [Fact]
        public void GetRequirementsDescription_IncludesMinimumLength()
        {
            var validator = CreateValidator();

            var description = validator.GetRequirementsDescription();

            Assert.Contains("12 characters", description);
        }
    }
}
