namespace Omodot.BackgroundAgent;

public sealed class ConcurrencyManager
{
    private sealed class QueueEntry
    {
        public required TaskCompletionSource<bool> Completion { get; init; }
        public bool Settled { get; set; }
    }

    private readonly BackgroundTaskCoreConfig? config;
    private readonly Dictionary<string, int> counts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Queue<QueueEntry>> queues = new(StringComparer.Ordinal);
    private readonly object sync = new();

    public ConcurrencyManager(BackgroundTaskCoreConfig? config = null)
    {
        this.config = config;
    }

    public int GetConcurrencyLimit(string model)
    {
        if (config?.ModelConcurrency is not null && config.ModelConcurrency.TryGetValue(model, out var modelLimit))
        {
            return modelLimit == 0 ? int.MaxValue : modelLimit;
        }

        var provider = model.Split('/', 2)[0];
        if (!string.IsNullOrEmpty(provider) && config?.ProviderConcurrency is not null && config.ProviderConcurrency.TryGetValue(provider, out var providerLimit))
        {
            return providerLimit == 0 ? int.MaxValue : providerLimit;
        }

        if (config?.DefaultConcurrency is int defaultLimit)
        {
            return defaultLimit == 0 ? int.MaxValue : defaultLimit;
        }

        return 5;
    }

    public Task AcquireAsync(string model)
    {
        lock (sync)
        {
            var limit = GetConcurrencyLimit(model);
            if (limit == int.MaxValue)
            {
                return Task.CompletedTask;
            }

            var current = counts.GetValueOrDefault(model);
            if (current < limit)
            {
                counts[model] = current + 1;
                return Task.CompletedTask;
            }

            var entry = new QueueEntry { Completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously) };
            if (!queues.TryGetValue(model, out var queue))
            {
                queue = new Queue<QueueEntry>();
                queues[model] = queue;
            }

            queue.Enqueue(entry);
            return entry.Completion.Task;
        }
    }

    public void Release(string model)
    {
        lock (sync)
        {
            var limit = GetConcurrencyLimit(model);
            if (limit == int.MaxValue)
            {
                return;
            }

            if (queues.TryGetValue(model, out var queue))
            {
                while (queue.Count > 0)
                {
                    var next = queue.Dequeue();
                    if (next.Settled)
                    {
                        continue;
                    }

                    next.Settled = true;
                    next.Completion.SetResult(true);
                    if (queue.Count == 0)
                    {
                        queues.Remove(model);
                    }

                    return;
                }

                queues.Remove(model);
            }

            var current = counts.GetValueOrDefault(model);
            if (current > 0)
            {
                counts[model] = current - 1;
            }
        }
    }

    public void CancelWaiters(string model)
    {
        lock (sync)
        {
            if (!queues.TryGetValue(model, out var queue))
            {
                return;
            }

            while (queue.Count > 0)
            {
                var entry = queue.Dequeue();
                if (entry.Settled)
                {
                    continue;
                }

                entry.Settled = true;
                entry.Completion.SetException(new InvalidOperationException($"Concurrency queue cancelled for model: {model}"));
            }

            queues.Remove(model);
        }
    }

    public void Clear()
    {
        foreach (var model in queues.Keys.ToArray())
        {
            CancelWaiters(model);
        }

        lock (sync)
        {
            counts.Clear();
            queues.Clear();
        }
    }

    public int GetCount(string model)
    {
        lock (sync)
        {
            return counts.GetValueOrDefault(model);
        }
    }

    public int GetQueueLength(string model)
    {
        lock (sync)
        {
            return queues.TryGetValue(model, out var queue) ? queue.Count : 0;
        }
    }
}
