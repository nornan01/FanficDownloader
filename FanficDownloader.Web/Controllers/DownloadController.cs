using FanficDownloader.Application.Services;
using FanficDownloader.Core.Models;
using FanficDownloader.Web.Dtos;
using Microsoft.AspNetCore.Mvc;
using FanficDownloader.Web.Services;
using Microsoft.Extensions.Logging;
using FanficDownloader.Web.Models;


namespace FanficDownloader.Web.Controllers;

[ApiController]
[Route("download")]
public class DownloadController : ControllerBase
{
    private readonly DownloadQueueService _queue;
    private readonly FanficDownloadService _downloadService;
    private readonly WebDownloadJobStore _jobStore;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DownloadController> _logger;

    public DownloadController(
        DownloadQueueService queue,
        FanficDownloadService downloadService,
        WebDownloadJobStore jobStore,
        IServiceScopeFactory scopeFactory,
        ILogger<DownloadController> logger)
    {
        _queue = queue;
        _downloadService = downloadService;
        _jobStore = jobStore;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [HttpPost("start/{format}")]
    public async Task<IActionResult> StartDownload(string format, [FromForm] DownloadRequest request, CancellationToken ct)
    {
        if (!string.Equals(format, "txt", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(format, "epub", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Unsupported format");
        }

        var jobId = Guid.NewGuid();
        var url = request.Url;
        var normalizedFormat = format.ToLowerInvariant();

        _jobStore.Create(jobId, url, normalizedFormat, HttpContext.TraceIdentifier, 0);

        int position;

        try
        {
            position = await _queue.EnqueueWithPosition(async queueCt =>
            {
                _jobStore.MarkRunning(jobId);

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var downloadService = scope.ServiceProvider.GetRequiredService<FanficDownloadService>();
                    var job = _jobStore.TryGet(jobId, out var createdJob) ? createdJob : null;
                    var progress = job?.Progress ?? new DownloadProgress();

                    var file = normalizedFormat == "epub"
                        ? await downloadService.BuildEpubAsync(url, progress, queueCt)
                        : await downloadService.BuildTxtAsync(url, progress, queueCt);

                    _jobStore.MarkCompleted(jobId, file);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "WEB job failed: {JobId} | {Url}", jobId, url);
                    _jobStore.MarkFailed(jobId, ex);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue WEB job: {JobId} | {Url}", jobId, url);
            _jobStore.MarkFailed(jobId, ex);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Download queue is unavailable");
        }

        _jobStore.UpdateQueuePosition(jobId, position);

        _logger.LogInformation("WEB JOB created: {JobId} | {Url} | {Format}", jobId, url, normalizedFormat);

        return Accepted(new
        {
            jobId,
            queuePosition = position
        });
    }


    [HttpPost("info")]
    public async Task<IActionResult> GetInfo([FromForm] DownloadRequest request,
                                         CancellationToken ct)
    {
        try
        {
            var fanfic = await _downloadService.GetInfoAsync(request.Url, ct);
            return Ok(fanfic);
        }
        catch (NotSupportedException)
        {
            return BadRequest("Source not supported");
        }
    }
    [HttpGet("progress/{jobId}")]
    public IActionResult GetProgress(Guid jobId)
    {
        if (!_jobStore.TryGet(jobId, out var job) || job == null)
            return NotFound();

        var total = job.Progress.TotalChapters;
        var completed = job.Progress.CompletedChapters;
        var percent = total > 0
            ? (int)Math.Floor((double)completed / total * 100)
            : job.Status == WebDownloadJobStatus.Completed
                ? 100
                : 0;

        return Ok(new
        {
            status = job.Status.ToString().ToLowerInvariant(),
            totalChapters = total,
            completedChapters = completed,
            percent = Math.Clamp(percent, 0, 100),
            queuePosition = job.QueuePosition,
            error = job.Error
        });
    }

    [HttpGet("file/{jobId}")]
    public IActionResult DownloadFile(Guid jobId)
    {
        if (!_jobStore.TryGet(jobId, out var job) || job == null)
            return NotFound();

        if (job.Status == WebDownloadJobStatus.Failed)
            return BadRequest(job.Error ?? "Download failed");

        if (job.Status != WebDownloadJobStatus.Completed || job.Result == null)
            return Conflict("File is not ready yet");

        return File(job.Result.Bytes, job.Result.ContentType, job.Result.FileName);
    }


}
