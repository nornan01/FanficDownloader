using System.Threading.Channels;
using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using FanficDownloader.Application.Configuration;
namespace FanficDownloader.Web.Services;

public class DownloadQueueService : BackgroundService
{
    private readonly Channel<Func<CancellationToken, Task>> _queue =
        Channel.CreateUnbounded<Func<CancellationToken, Task>>();

    private readonly SemaphoreSlim _semaphore;
    private readonly ILogger<DownloadQueueService> _logger;

    private readonly ConcurrentDictionary<Task, byte> _activeJobs = new();

    private volatile bool _isStopping;
    private int _queueLength = 0;

    public DownloadQueueService(
        ILogger<DownloadQueueService> logger,
        IOptions<DownloadSettings> options)
    {
        _logger = logger;
        _semaphore = new SemaphoreSlim(options.Value.MaxConcurrentDownloads);
    }

    public async Task<int> EnqueueWithPosition(Func<CancellationToken, Task> job)
    {
        if (_isStopping)
            throw new InvalidOperationException("Queue is stopping");

        var position = Interlocked.Increment(ref _queueLength);

        _logger.LogInformation(
            "Job enqueued. Position={Position}, QueueLength={QueueLength}",
            position,
            _queueLength);

        await _queue.Writer.WriteAsync(job);

        return position;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Download queue started");

        await foreach (var job in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            var task = ProcessJobAsync(job, stoppingToken);

            _activeJobs.TryAdd(task, 0);

            _ = task.ContinueWith(t =>
            {
                _activeJobs.TryRemove(t, out _);
            }, TaskScheduler.Default);
        }

        _logger.LogInformation("Download queue stopped");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping download queue");

        _isStopping = true;

        _queue.Writer.TryComplete();

        try
        {
            await Task.WhenAll(_activeJobs.Keys);
        }
        catch
        {
            // ошибки уже залогированы внутри задач
        }

        await base.StopAsync(cancellationToken);

        _logger.LogInformation("Download queue shutdown complete");
    }

    private async Task ProcessJobAsync(
        Func<CancellationToken, Task> job,
        CancellationToken stoppingToken)
    {
        await _semaphore.WaitAsync(stoppingToken);

        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation(
                "Job started. QueueLength={QueueLength}",
                _queueLength);

            await job(stoppingToken);

            sw.Stop();

            _logger.LogInformation(
                "Job finished successfully in {ElapsedMs} ms",
                sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Job cancelled");
        }
        catch (Exception ex)
        {
            sw.Stop();

            _logger.LogError(
                ex,
                "Job failed after {ElapsedMs} ms",
                sw.ElapsedMilliseconds);
        }
        finally
        {
            Interlocked.Decrement(ref _queueLength);

            _semaphore.Release();

            _logger.LogInformation(
                "Job released. QueueLength={QueueLength}",
                _queueLength);
        }
    }
    public int GetQueueLength()
    {
        return _queueLength;
    }
}