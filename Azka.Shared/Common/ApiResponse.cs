namespace Azka.Shared.Common;

public class ApiResponse<T>
{
    public bool Succeeded { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public List<string> Errors { get; set; } = new();

    public static ApiResponse<T> Success(T data, string message = "Operation completed successfully.")
        => new() { Succeeded = true, Message = message, Data = data };

    public static ApiResponse<T> Failure(string message, List<string>? errors = null)
        => new() { Succeeded = false, Message = message, Errors = errors ?? new() };
}
