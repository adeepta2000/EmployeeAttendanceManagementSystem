using FluentValidation;
using FluentValidation.Results;

namespace Attendance.Application.Common.Messaging;

/// <summary>
/// Wraps every command handler: runs the command's validators first and throws
/// <see cref="ValidationException"/> (an HTTP 400 at the API) without calling the handler if any fail.
/// </summary>
internal sealed class ValidatingCommandHandler<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    IEnumerable<IValidator<TCommand>> validators)
    : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public async Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken)
    {
        var failures = new List<ValidationFailure>();
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(command, cancellationToken);
            failures.AddRange(result.Errors);
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await inner.HandleAsync(command, cancellationToken);
    }
}
