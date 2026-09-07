using System.Collections.Generic;

namespace InventorySystem.Services.Interfaces
{
    public interface IPasswordPolicyValidator
    {
        IReadOnlyList<string> Validate(string password);
        string GetRequirementsDescription();
    }
}
