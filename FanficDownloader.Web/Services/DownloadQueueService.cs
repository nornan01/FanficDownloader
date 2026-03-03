using System.Security.AccessControl;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FanficDownloader.Web.Services;

public class DownloadQueueService : BackgroundService
{
    private readonly Channel<Func<CancellationToken, Task>> _queue =
        Channel.CreateUnbounded<Func<CancellationToken, Task>>();

    private readonly SemaphoreSlim _semaphore = new(3); 
    private readonly ILogger<DownloadQueueService> _logger;
    private int _queueLength = 0;


    public DownloadQueueService(ILogger<DownloadQueueService> logger)
    {
        _logger = logger;
    }
    public async Task<int> EnqueueWithPosition(Func<CancellationToken, Task> job)
    {
        var position = Interlocked.Increment(ref _queueLength);
        _logger.LogInformation(
            "Job enqueued. Position={Position}, QueueLength={QueueLength}",
            position,
            _queueLength);
        await _queue.Writer.WriteAsync(job);
        return position;
    }

    public int GetQueueLength() => _queueLength;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Download queue started");

        await foreach (var job in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            _ = ProcessJobAsync(job, stoppingToken);
        }

        _logger.LogInformation("Download queue stopped");
    }

    private async Task ProcessJobAsync(
        Func<CancellationToken, Task> job,
        CancellationToken stoppingToken)
    {
        await _semaphore.WaitAsync(stoppingToken);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            _logger.LogInformation("Job started. ActiveJobs={Active}",
                3 - _semaphore.CurrentCount);
            await job(stoppingToken);
            sw.Stop();
            _logger.LogInformation(
                "Job finished successfully in {ElapsedMs} ms",
                sw.ElapsedMilliseconds);
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
}