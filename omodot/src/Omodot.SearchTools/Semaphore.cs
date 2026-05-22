using System.Collections.Generic;

namespace Omodot.SearchTools;

public sealed class Semaphore
{
    private readonly Queue<TaskCompletionSource> _queue = new();
    private readonly object _gate = new();
    private int _running;

    public Semaphore(int max)
    {
        if (max <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(max), "Semaphore max must be greater than zero.");
        }

        Max = max;
    }

    public int Max { get; }

    public Task AcquireAsync()
    {
        lock (_gate)
        {
            if (_running < Max)
            {
                _running++;
                return Task.CompletedTask;
            }

            var waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _queue.Enqueue(waiter);
            return waiter.Task;
        }
    }

    public void Release()
    {
        TaskCompletionSource? next = null;

        lock (_gate)
        {
            if (_running == 0)
            {
                throw new InvalidOperationException("Cannot release a semaphore that is not acquired.");
            }

            _running--;
            if (_queue.Count > 0)
            {
                next = _queue.Dequeue();
                _running++;
            }
        }

        next?.SetResult();
    }
}

public static class SearchSemaphore
{
    public static Semaphore Instance { get; } = new(2);
}
