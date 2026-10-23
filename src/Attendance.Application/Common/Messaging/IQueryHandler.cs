namespace Attendance.Application.Common.Messaging;

/// <summary>
/// A request that reads state without changing it. A validator is optional; when one exists it runs
/// before the handler, as it does for commands.
/// </summary>
public interface IQuery<TResult>;

/// <summary>Handles one query type. Resolve it from DI to get the validating pipeline.</summary>
public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
