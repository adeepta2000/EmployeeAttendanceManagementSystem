namespace Attendance.Application.Common.Messaging;

/// <summary>
/// A request that changes state and produces a <typeparamref name="TResult"/>. Each command has one
/// handler, and a FluentValidation validator beside it that runs before the handler.
/// </summary>
public interface ICommand<TResult>;

/// <summary>
/// Handles one command type. Resolve it from DI to get the validating pipeline (spec §2.3: hand-rolled
/// instead of MediatR, which is commercially licensed).
/// </summary>
public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
