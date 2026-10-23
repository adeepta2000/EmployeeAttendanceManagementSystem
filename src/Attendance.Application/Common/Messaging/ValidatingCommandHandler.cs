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
        await RequestValidation.ValidateAsync(command, validators, cancellationToken);
        return await inner.HandleAsync(command, cancellationToken);
    }
}

/// <summary>The query counterpart of <see cref="ValidatingCommandHandler{TCommand,TResult}"/>.</summary>
internal sealed class ValidatingQueryHandler<TQuery, TResult>(
    IQueryHandler<TQuery, TResult> inner,
    IEnumerable<IValidator<TQuery>> validators)
    : IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    public async Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken)
    {
        await RequestValidation.ValidateAsync(query, validators, cancellationToken);
        return await inner.HandleAsync(query, cancellationToken);
    }
}

internal static class RequestValidation
{
    /// <exception cref="ValidationException">At least one validator failed.</exception>
    public static async Task ValidateAsync<T>(T request, IEnumerable<IValidator<T>> validators, CancellationToken cancellationToken)
    {
        var failures = new List<ValidationFailure>();
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(request, cancellationToken);
            failures.AddRange(result.Errors);
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }
    }
}
