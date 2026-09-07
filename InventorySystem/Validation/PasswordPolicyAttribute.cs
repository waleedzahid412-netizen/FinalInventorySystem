using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Validation
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class PasswordPolicyAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is null or "")
            {
                return ValidationResult.Success;
            }

            var validator = validationContext.GetService(typeof(IPasswordPolicyValidator)) as IPasswordPolicyValidator;
            if (validator == null)
            {
                return ValidationResult.Success;
            }

            var errors = validator.Validate(value.ToString()!);
            if (errors.Count == 0)
            {
                return ValidationResult.Success;
            }

            return new ValidationResult(string.Join(" ", errors));
        }
    }
}
