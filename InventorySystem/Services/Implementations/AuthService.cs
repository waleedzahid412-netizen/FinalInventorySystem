using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using BCrypt.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using InventorySystem.Configuration;
using InventorySystem.DTOs;
using InventorySystem.Helpers;
using InventorySystem.Models.Entities;
using InventorySystem.Repositories.Interfaces;
using InventorySystem.Services.Interfaces;

namespace InventorySystem.Services.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly JwtSettings _jwtSettings;
        private readonly AccountLockoutOptions _lockoutOptions;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            IUserRepository userRepository,
            IOptions<JwtSettings> jwtSettings,
            IOptions<SecuritySettings> securitySettings,
            ILogger<AuthService> logger)
        {
            _userRepository = userRepository;
            _jwtSettings = jwtSettings.Value;
            _lockoutOptions = securitySettings.Value.AccountLockout;
            _logger = logger;
        }

        public async Task<AuthResultDTO> LoginAsync(LoginRequestDTO request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Username) ||
                    string.IsNullOrWhiteSpace(request.Password))
                {
                    return new AuthResultDTO
                    {
                        Success = false,
                        Message = "Username and password are required."
                    };
                }

                var user = await _userRepository.GetByUsernameAsync(request.Username);
                if (user == null || !user.IsActive || user.IsDeleted)
                {
                    return new AuthResultDTO
                    {
                        Success = false,
                        Message = UserFacingErrorMessages.InvalidCredentials
                    };
                }

                if (IsAccountLocked(user))
                {
                    return new AuthResultDTO
                    {
                        Success = false,
                        Message = UserFacingErrorMessages.AccountLocked
                    };
                }

                bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
                if (!isPasswordValid)
                {
                    var isLocked = await _userRepository.RecordFailedLoginAsync(
                        user.UserID,
                        _lockoutOptions.MaxFailedAttempts,
                        _lockoutOptions.LockoutMinutes);

                    return new AuthResultDTO
                    {
                        Success = false,
                        Message = isLocked
                            ? UserFacingErrorMessages.AccountLocked
                            : UserFacingErrorMessages.InvalidCredentials
                    };
                }

                await _userRepository.ResetLoginFailuresAsync(user.UserID);

                string roleName = user.Role != null ? user.Role.RoleName : "Admin";
                var token = GenerateJwtToken(user, roleName);

                return new AuthResultDTO
                {
                    Success = true,
                    Message = "Login successful.",
                    Token = token,
                    UserId = user.UserID,
                    Username = user.Username,
                    RoleName = roleName
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login failed for username {Username}", request.Username);
                return new AuthResultDTO
                {
                    Success = false,
                    Message = UserFacingErrorMessages.LoginFailed
                };
            }
        }

        public async Task<AuthResultDTO> ValidateTokenAsync(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_jwtSettings.SecretKey);

                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _jwtSettings.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                var jwtToken = (JwtSecurityToken)validatedToken;
                var userIdClaim = jwtToken.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return new AuthResultDTO { Success = false, Message = "Invalid token claims." };
                }

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null || !user.IsActive || user.IsDeleted)
                {
                    return new AuthResultDTO { Success = false, Message = "User not found or inactive." };
                }

                string roleName = user.Role != null ? user.Role.RoleName : "Admin";

                return new AuthResultDTO
                {
                    Success = true,
                    Username = user.Username,
                    RoleName = roleName,
                    Token = token
                };
            }
            catch
            {
                return new AuthResultDTO { Success = false, Message = "Token validation failed." };
            }
        }

        private static bool IsAccountLocked(User user)
        {
            return user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;
        }

        private string GenerateJwtToken(User user, string roleName)
        {
            var key = Encoding.ASCII.GetBytes(_jwtSettings.SecretKey);
            var tokenHandler = new JwtSecurityTokenHandler();

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, roleName)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.DurationInMinutes),
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
