using Microsoft.AspNetCore.Mvc;
using Szamla.Application.Common.Exceptions;

namespace Szamla.Api.Middleware;

/// <summary>
/// Last-resort safety net: converts known Application-layer exceptions into the matching HTTP
/// status, and logs+masks anything unexpected instead of leaking stack traces to the client.
/// Endpoint-level code should still handle the exceptions it can respond to more specifically.
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ConflictException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status409Conflict, ex.Message);
        }
        catch (ArgumentException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Kezeletlen hiba a kérés feldolgozása közben: {Path}", context.Request.Path);
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "Váratlan hiba történt.");
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, int statusCode, string title)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
        };

        await context.Response.WriteAsJsonAsync(problem);
    }
}
