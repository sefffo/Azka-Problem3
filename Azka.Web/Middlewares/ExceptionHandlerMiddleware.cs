using Azka.Services.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Azka.Web.Middlewares;

public class ExceptionHandlerMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlerMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await next.Invoke(httpContext);
            await HandleNotFoundEndpointAsync(httpContext);
        }
        catch (ValidationException ex)
        {
            logger.LogWarning("Validation failed at {Method} {Path}: {Errors}",
                httpContext.Request.Method, httpContext.Request.Path, ex.Message);

            var errors = ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            await WriteProblemAsync(httpContext, 400, "Validation Failed",
                "One or more validation errors occurred.",
                new Dictionary<string, object?> { ["errors"] = errors, ["traceId"] = httpContext.TraceIdentifier });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception [{Type}] at {Method} {Path}",
                ex.GetType().Name, httpContext.Request.Method, httpContext.Request.Path);

            var (status, title, detail) = ex switch
            {
                NotFoundException e           => (404, "Not Found", e.Message),
                ConflictException e           => (409, "Conflict", e.Message),
                BadRequestException e         => (400, "Bad Request", e.Message),
                ForbiddenException e          => (403, "Forbidden", e.Message),
                ServiceUnavailableException e => (503, "Service Unavailable", e.Message),
                UnauthorizedAccessException e => (401, "Unauthorized", e.Message),
                _                             => (500, "Internal Server Error", "An unexpected error occurred.")
            };

            await WriteProblemAsync(httpContext, status, title, detail,
                new Dictionary<string, object?>
                {
                    ["traceId"] = httpContext.TraceIdentifier,
                    ["timestamp"] = DateTime.UtcNow,
                    ["stackTrace"] = IsDevEnvironment() ? ex.StackTrace : null
                });
        }
    }

    private static async Task HandleNotFoundEndpointAsync(HttpContext ctx)
    {
        if (ctx.Response.StatusCode == 404 && !ctx.Response.HasStarted)
            await WriteProblemAsync(ctx, 404, "Endpoint Not Found",
                $"The endpoint '{ctx.Request.Method} {ctx.Request.Path}' does not exist.",
                new Dictionary<string, object?> { ["traceId"] = ctx.TraceIdentifier });
    }

    private static async Task WriteProblemAsync(
        HttpContext ctx, int status, string title, string detail,
        Dictionary<string, object?> extensions)
    {
        var problem = new ProblemDetails
        {
            Title = title, Detail = detail, Status = status,
            Instance = $"{ctx.Request.Method} {ctx.Request.Path}"
        };
        foreach (var (k, v) in extensions) problem.Extensions[k] = v;
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json";
        await ctx.Response.WriteAsJsonAsync(problem);
    }

    private static bool IsDevEnvironment()
        => Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
}
