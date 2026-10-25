using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TodoApp.Shared.Abstractions.Results;
using TodoApp.Shared.Infrastructure.Auth;

namespace TodoApp.Shared.Infrastructure.Api;

/// <summary>
/// Base class for all module controllers. It translates a <see cref="Result"/> into HTTP,
/// so every module returns errors in the same RFC 9457 ProblemDetails shape.
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected Guid CurrentUserId => User.GetUserId();

    protected ActionResult OkOrProblem<T>(Result<T> result) =>
        result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error);

    protected ActionResult NoContentOrProblem(Result result) =>
        result.IsSuccess ? NoContent() : ToProblem(result.Error);

    protected ActionResult ToProblem(Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, statusCode, detail: error.Description);
        problem.Extensions["errorCode"] = error.Code;

        return new ObjectResult(problem)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" }
        };
    }
}
