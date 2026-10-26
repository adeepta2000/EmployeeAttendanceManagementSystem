using System.Threading.Channels;
using Attendance.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Attendance.Infrastructure.Background;

internal sealed record BackgroundWorkItem(string Name, Func<IServiceProvider, CancellationToken, Task> Work);

/// <summary>An in-memory, bounded queue of work for <see cref="BackgroundWorkService"/>.</summary>
internal sealed class BackgroundWorkQueue : IBackgroundWorkQueue
{
    /// <summary>
    /// Plenty for password-reset requests, which are rate-limited per IP. A full queue drops new work
    /// rather than holding requests up.
    /// </summary>
    public const int Capacity = 500;

    private readonly Channel<BackgroundWorkItem> _channel = Channel.CreateBounded<BackgroundWorkItem>(
        new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });

    public ChannelReader<BackgroundWorkItem> Reader => _channel.Reader;

    // With FullMode.Wait, TryWrite returns false instead of waiting when the queue is full.
    public bool TryEnqueue(string name, Func<IServiceProvider, CancellationToken, Task> work) =>
        _channel.Writer.TryWrite(new BackgroundWorkItem(name, work));
}

/// <summary>
/// Runs queued work one item at a time, each in a DI scope of its own, as a request would. A failing item
/// is logged and skipped; it never stops the items behind it.
/// </summary>
internal sealed class BackgroundWorkService(
    BackgroundWorkQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<BackgroundWorkService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await item.Work(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Background work {WorkName} failed", item.Name);
            }
        }
    }
}
