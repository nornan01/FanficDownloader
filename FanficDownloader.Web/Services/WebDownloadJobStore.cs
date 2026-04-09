using System.Collections.Concurrent;
using FanficDownloader.Application.Models;
using FanficDownloader.Web.Models;

namespace FanficDownloader.Web.Services;

public class WebDownloadJobStore
{
    private static readonly TimeSpan Retention = TimeSpan.FromMinutes(30);
    private readonly ConcurrentDictionary<Guid, WebDownloadJob> _jobs = new();

    public WebDownloadJob Create(Guid jobId, string url, string format, string requesterId, int queuePosition)
    {
        CleanupExpired();

        var job = new WebDownloadJob
        {
            Id = jobId,
            Url = url,
            Format = format,
            RequesterId = requesterId,
            QueuePosition = queuePosition
        };

        _jobs[jobId] = job;
        return job;
    }

    public bool TryGet(Guid jobId, out WebDownloadJob? job)
    {
        CleanupExpired();
        return _jobs.TryGetValue(jobId, out job);
    }

    public void MarkRunning(Guid jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
            return;

        job.Status = WebDownloadJobStatus.Running;
        job.UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkCompleted(Guid jobId, DownloadFileResult result)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
            return;

        job.Result = result;
        job.Status = WebDownloadJobStatus.Completed;

        if (job.Progress.TotalChapters > 0)
        {
            job.Progress.CompletedChapters = Math.Max(
                job.Progress.CompletedChapters,
                job.Progress.TotalChapters);
        }

        job.UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkFailed(Guid jobId, Exception exception)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
            return;

        job.Status = WebDownloadJobStatus.Failed;
        job.Error = exception.Message;
        job.UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void UpdateQueuePosition(Guid jobId, int queuePosition)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
            return;

        job.QueuePosition = queuePosition;
        job.UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private void CleanupExpired()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var pair in _jobs)
        {
            var age = now - pair.Value.UpdatedAtUtc;
            var terminal = pair.Value.Status == WebDownloadJobStatus.Completed ||
                           pair.Value.Status == WebDownloadJobStatus.Failed;

            if (terminal && age > Retention)
            {
                _jobs.TryRemove(pair.Key, out _);
            }
        }
    }
}
