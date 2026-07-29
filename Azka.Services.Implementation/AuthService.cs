using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Azka.Domain.Entities;
using Azka.Services.DTOs.Auth;
using Azka.Services.Implementation.Email;
using Azka.Services.Interfaces;
using Azka.Shared.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Azka.Services.Implementation;

/// <summary>
/// Handles user registration and login with JWT generation.
///
/// ════════════════════════════════════════════════════════════
///  REGISTER FLOW
/// ════════════════════════════════════════════════════════════
///
///  HTTP POST /api/Auth/register
///       │
///       ▼
///  [AuthController] ──► RegisterAsync(RegisterDto)
///       │
///       ▼
///  UserManager.FindByEmailAsync()          ← ASP.NET Identity (DB)
///       │
///       ├─ [email exists] ──► Failure("Email already registered")
///       │
///       ▼
///  UserManager.CreateAsync(user, password) → DB: AspNetUsers INSERT
///       │
///       ├─ [identity errors] ──► Failure(errors)
///       │
///       ▼
///  UserManager.AddToRoleAsync()            → DB: AspNetUserRoles INSERT
///       │
///       ▼
///  BackgroundEmailQueue.EnqueueAsync()     (fire-and-forget welcome email)
///       │
///       ▼
///  GenerateJwtToken()  ◄── reads JwtSettings from IConfiguration
///       │
///       ▼
///  ApiResponse&lt;AuthResultDto&gt;.Success(token)
///       │
///       ▼
///  HTTP 200 { token, email, fullName, role, expiresAt }
///
/// ════════════════════════════════════════════════════════════
///  LOGIN FLOW
/// ════════════════════════════════════════════════════════════
///
///  HTTP POST /api/Auth/login
///       │
///       ▼
///  [AuthController] ──► LoginAsync(LoginDto)
///       │
///       ▼
///  UserManager.FindByEmailAsync()          ← DB: AspNetUsers SELECT
///       │
///       ├─ [not found] ──► Failure("Invalid email or password")
///       │
///       ▼
///  UserManager.CheckPasswordAsync()        (PBKDF2 hash compare — no extra DB hit)
///       │
///       ├─ [invalid] ──► Failure("Invalid email or password")
///       │
///       ▼
///  GenerateJwtToken()
///   ├─ Claims: sub, email, name, role, jti
///   └─ Signs with HMAC-SHA256 SymmetricKey from config
///       │
///       ▼
///  ApiResponse&lt;AuthResultDto&gt;.Success(token)
///       │
///       ▼
///  HTTP 200 { token, email, fullName, role, expiresAt }
/// </summary>
public class AuthService(
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    BackgroundEmailQueue emailQueue) : IAuthService
{
    public async Task<ApiResponse<AuthResultDto>> RegisterAsync(RegisterDto dto)
    {
        var existingUser = await userManager.FindByEmailAsync(dto.Email);
        if (existingUser is not null)
            return ApiResponse<AuthResultDto>.Failure("Email is already registered.");

        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email    = dto.Email,
            FullName = dto.FullName,
            Role     = dto.Role
        };

        var result = await userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToList();
            return ApiResponse<AuthResultDto>.Failure("Registration failed.", errors);
        }

        await userManager.AddToRoleAsync(user, dto.Role);

        await emailQueue.EnqueueAsync(new EmailJobDescriptor(
            To:      user.Email!,
            Subject: "Welcome to Azka — Account Created",
            Body:    $"""
                     <h2>Welcome, {user.FullName}!</h2>
                     <p>Your <strong>{user.Role}</strong> account has been created successfully.</p>
                     <p>You can now log in using your email address.</p>
                     <br/>
                     <p style="color:#888;font-size:12px;">Automated message from the Azka system.</p>
                     """));

        var token = GenerateJwtToken(user);
        return ApiResponse<AuthResultDto>.Success(token, "Registration successful.");
    }

    public async Task<ApiResponse<AuthResultDto>> LoginAsync(LoginDto dto)
    {
        var user = await userManager.FindByEmailAsync(dto.Email);
        if (user is null)
            return ApiResponse<AuthResultDto>.Failure("Invalid email or password.");

        var isPasswordValid = await userManager.CheckPasswordAsync(user, dto.Password);
        if (!isPasswordValid)
            return ApiResponse<AuthResultDto>.Failure("Invalid email or password.");

        var token = GenerateJwtToken(user);
        return ApiResponse<AuthResultDto>.Success(token, "Login successful.");
    }

    private AuthResultDto GenerateJwtToken(ApplicationUser user)
    {
        var jwtSettings = configuration.GetSection("JwtSettings");
        var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry      = DateTime.UtcNow.AddMinutes(double.Parse(jwtSettings["ExpiryInMinutes"]!));

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim(ClaimTypes.Name,               user.FullName),
            new Claim(ClaimTypes.Role,               user.Role),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer:             jwtSettings["Issuer"],
            audience:           jwtSettings["Audience"],
            claims:             claims,
            expires:            expiry,
            signingCredentials: credentials);

        return new AuthResultDto
        {
            Token     = new JwtSecurityTokenHandler().WriteToken(token),
            Email     = user.Email!,
            FullName  = user.FullName,
            Role      = user.Role,
            ExpiresAt = expiry
        };
    }
}
