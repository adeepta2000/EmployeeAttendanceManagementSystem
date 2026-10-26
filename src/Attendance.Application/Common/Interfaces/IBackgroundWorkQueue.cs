namespace Attendance.Application.Common.Interfaces;

/// <summary>
/// Runs short work items after the request that queued them has been answered, one at a time, each in
/// its own DI scope. In memory: a restart drops whatever is still queued, so queue only work that a user
/// can simply ask for again. Scheduled and retried jobs belong to Hangfire.
/// </summary>
public interface IBackgroundWorkQueue
{
    /// <summary>Queues <paramref name="work"/>; false when the queue is full and the work was dropped.</summary>
    /// <param name="name">Names the work in logs. Never put personal data or secrets in it.</param>
    bool TryEnqueue(string name, Func<IServiceProvider, CancellationToken, Task> work);
}
