using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace PageManager.API.Middlewares;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService pds
) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        var traceId = Activity.Current?.Id ?? ctx.TraceIdentifier;

        var problem = ex switch
        {
            NotFoundException nf => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Resource not found",
                Detail = nf.Message,
                Instance = ctx.Request.Path
            },

            ConflictException cf => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = cf.Message,
                Instance = ctx.Request.Path
            },

            ValidationException ve => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Detail = "One or more validation errors occurred.",
                Instance = ctx.Request.Path,
                Extensions =
                {
                    ["errors"] = ve.Errors
                        .GroupBy(e => e.PropertyName)
                        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
                }
            },

            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred. Please try again.",
                Instance = ctx.Request.Path
            }
        };

        problem.Extensions["traceId"] = traceId;
        ctx.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        ctx.Response.ContentType = "application/problem+json";
        
        await pds.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = ctx,
            ProblemDetails = problem,
            Exception = ex
        });

        if (problem.Status >= 500)
            logger.LogError(ex, "Unhandled exception. TraceId:{TraceId}", traceId);

        return true;
    }
}