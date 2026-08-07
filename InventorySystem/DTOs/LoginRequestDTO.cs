using System.ComponentModel.DataAnnotations;

namespace InventorySystem.DTOs
{
    public class LoginRequestDTO
    {
        [Required(ErrorMessage = "Username is required.")]
        [MaxLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [MaxLength(100)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; } = false;
    }
}
