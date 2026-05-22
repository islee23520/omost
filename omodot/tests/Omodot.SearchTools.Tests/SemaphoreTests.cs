using System.Collections.Concurrent;

using Omodot.SearchTools;

namespace Omodot.SearchTools.Tests;

public sealed class SemaphoreTests
{
    [Fact]
    public async Task AcquireQueuesUntilRelease()
    {
        var semaphore = new Semaphore(1);
        var events = new ConcurrentQueue<string>();

        await semaphore.AcquireAsync();
        events.Enqueue("first-acquired");

        var queued = Task.Run(async () =>
        {
            events.Enqueue("second-waiting");
            await semaphore.AcquireAsync();
            events.Enqueue("second-acquired");
            semaphore.Release();
        });

        await Task.Delay(50);
        Assert.DoesNotContain("second-acquired", events);

        semaphore.Release();
        await queued;

        Assert.Equal(new[] { "first-acquired", "second-waiting", "second-acquired" }, events.ToArray());
    }
}
