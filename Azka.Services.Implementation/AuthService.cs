using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Azka.Domain.Entities;
using Azka.Services.DTOs.Auth;
using Azka.Services.Interfaces;
using Azka.Services.Implementation.Email;
using Azka.Shared.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Azka.Services.Implementation;

public class AuthService(
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    IEmailService emailService,
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

        // Fire-and-forget welcome email — runs on background thread, never blocks HTTP response
        var toEmail  = user.Email!;
        var fullName = user.FullName;
        var role     = user.Role;
        await emailQueue.EnqueueAsync(ct => emailService.SendAsync(
            to: toEmail,
            subject: "Welcome to Azka — Account Created",
            body: $"""
                <h2>Welcome, {fullName}!</h2>
                <p>Your <strong>{role}</strong> account has been created successfully.</p>
                <p>You can now log in using your email address.</p>
                <br/>
                <p style="color:#888;font-size:12px;">This is an automated message from the Azka system.</p>
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
            issuer:            jwtSettings["Issuer"],
            audience:          jwtSettings["Audience"],
            claims:            claims,
            expires:           expiry,
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
