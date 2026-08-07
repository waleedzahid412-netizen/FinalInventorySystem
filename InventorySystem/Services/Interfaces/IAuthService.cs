using System.Threading.Tasks;
using InventorySystem.DTOs;

namespace InventorySystem.Services.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResultDTO> LoginAsync(LoginRequestDTO request);
        Task<AuthResultDTO> ValidateTokenAsync(string token);
    }
}
