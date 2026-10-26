using Attendance.Infrastructure.Background;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Attendance.Application.Tests.Infrastructure;

public sealed class BackgroundWorkServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Runs_each_item_in_a_scope_of_its_own_and_keeps_going_after_a_failure()
    {
        var services = new ServiceCollection().AddScoped<ScopeMarker>().BuildServiceProvider();
        var queue = new BackgroundWorkQueue();
        var worker = new BackgroundWorkService(queue, services.GetRequiredService<IServiceScopeFactory>(), NullLogger<BackgroundWorkService>.Instance);
        var markers = new List<ScopeMarker>();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        queue.TryEnqueue("first", (provider, _) => Record(provider)).Should().BeTrue();
        queue.TryEnqueue("failing", (_, _) => throw new InvalidOperationException("boom")).Should().BeTrue();
        queue.TryEnqueue("last", async (provider, _) =>
        {
            await Record(provider);
            done.SetResult();
        }).Should().BeTrue();

        await worker.StartAsync(Ct);
        await done.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await worker.StopAsync(Ct);

        markers.Should().HaveCount(2, "the failing item didn't stop the one behind it");
        markers[0].Should().NotBeSameAs(markers[1], "every item gets a scope of its own");

        Task Record(IServiceProvider provider)
        {
            markers.Add(provider.GetRequiredService<ScopeMarker>());
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void A_full_queue_refuses_more_work_instead_of_blocking()
    {
        var queue = new BackgroundWorkQueue();
        for (var i = 0; i < BackgroundWorkQueue.Capacity; i++)
        {
            queue.TryEnqueue("filler", (_, _) => Task.CompletedTask).Should().BeTrue();
        }

        queue.TryEnqueue("one too many", (_, _) => Task.CompletedTask).Should().BeFalse();
    }

    private sealed class ScopeMarker;
}
