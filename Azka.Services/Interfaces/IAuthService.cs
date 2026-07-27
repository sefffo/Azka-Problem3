using Azka.Services.DTOs.Auth;
using Azka.Shared.Common;

namespace Azka.Services.Interfaces;

public interface IAuthService
{
    Task<ApiResponse<AuthResultDto>> RegisterAsync(RegisterDto dto);
    Task<ApiResponse<AuthResultDto>> LoginAsync(LoginDto dto);
}
