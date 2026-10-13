using Attendance.Shared.Results;
using Microsoft.AspNetCore.Mvc;

namespace Attendance.Api.Controllers;

// No [Produces] here: it is a result filter that would overwrite application/problem+json on errors.
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// Maps an expected failure to a ProblemDetails response. The error's code is added as the
    /// <c>code</c> extension so the client can branch on it without parsing the message.
    /// </summary>
    protected ObjectResult Problem(Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };

        var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, statusCode, detail: error.Message);
        problem.Extensions["code"] = error.Code;

        return new ObjectResult(problem)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" },
        };
    }
}
