using System.Threading.Channels;
using System.Threading;

public class DownloadQueueService
{
    private readonly Channel<Func<Task>> _queue =
        Channel.CreateUnbounded<Func<Task>>();

    private int _queueLength = 0;

    public async Task StartWorkers(int workers)
    {
        for (int i = 0; i < workers; i++)
        {
            _ = Task.Run(ProcessQueue);
        }
    }

    private async Task ProcessQueue()
    {
        await foreach (var job in _queue.Reader.ReadAllAsync())
        {
            try
            {
                await job();
            }
            finally
            {
                Interlocked.Decrement(ref _queueLength);
            }
        }
    }

    public async Task Enqueue(Func<Task> job)
    {
        Interlocked.Increment(ref _queueLength);
        await _queue.Writer.WriteAsync(job);
    }

    public int GetQueueLength()
    {
        return _queueLength;
    }

    public async Task<int> EnqueueWithPosition(Func<Task> job)
    {
        var pos = Interlocked.Increment(ref _queueLength);
        await _queue.Writer.WriteAsync(job);
        return pos;
    }

}
