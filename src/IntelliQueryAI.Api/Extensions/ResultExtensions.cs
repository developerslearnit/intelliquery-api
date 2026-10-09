using IntelliQueryAI.SharedKernel.Results;
using Microsoft.AspNetCore.Http;

namespace IntelliQueryAI.Api.Extensions;

/// <summary>
/// Extension methods for converting domain Result instances into standard ASP.NET Core IResult HTTP responses.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Converts a value-returning Result into an HTTP response.
    /// </summary>
    public static IResult ToHttpResult<TValue>(this Result<TValue> result, int successStatusCode = StatusCodes.Status200OK)
    {
        if (result.IsSuccess)
        {
            return successStatusCode switch
            {
                StatusCodes.Status201Created => Results.Created(string.Empty, result.Value),
                StatusCodes.Status204NoContent => Results.NoContent(),
                _ => Results.Ok(result.Value)
            };
        }

        return MapErrorToProblem(result.Error);
    }

    /// <summary>
    /// Converts a non-value Result into an HTTP response.
    /// </summary>
    public static IResult ToHttpResult(this Result result, int successStatusCode = StatusCodes.Status200OK)
    {
        if (result.IsSuccess)
        {
            return successStatusCode switch
            {
                StatusCodes.Status204NoContent => Results.NoContent(),
                _ => Results.Ok()
            };
        }

        return MapErrorToProblem(result.Error);
    }

    private static IResult MapErrorToProblem(Error error)
    {
        int statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };

        return Results.Problem(
            title: error.Code,
            detail: error.Description,
            statusCode: statusCode);
    }
}
