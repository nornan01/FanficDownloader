using FanficDownloader.Application.Services;
using FanficDownloader.Web.Dtos;
using Microsoft.AspNetCore.Mvc;
using FanficDownloader.Core.Models;
using FanficDownloader.Application.Models;
using FanficDownloader.Web.Services;
using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;


namespace FanficDownloader.Web.Controllers;

[ApiController]
[Route("download")]
public class DownloadController : ControllerBase
{
    private readonly DownloadQueueService _queue;
    private readonly FanficDownloadService _downloadService;
    private readonly ILogger<DownloadController> _logger;

    public DownloadController(DownloadQueueService queue, FanficDownloadService downloadService, ILogger<DownloadController> logger)
    {
        _queue = queue;
        _downloadService = downloadService;
        _logger = logger;
    }

    [HttpPost("txt")]
    public async Task<IActionResult> DownloadTxt([FromForm] DownloadRequest request,
                                             CancellationToken ct)
    {
        var job = new DownloadJob
        {
            Url = request.Url,
            Format = "txt",
            RequesterId = HttpContext.TraceIdentifier
        };

        _logger.LogInformation("WEB JOB created: {JobId} | {Url}", job.Id, job.Url);
        var tcs = new TaskCompletionSource<DownloadFileResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var position = _queue.GetQueueLength() + 1;
        Response.Headers["X-Queue-Position"] = position.ToString();

        await _queue.EnqueueWithPosition(async (queueCt) =>
                {
                    try
                    {
                        using var scope = HttpContext.RequestServices.CreateScope();
                        var downloadService = scope.ServiceProvider
                                                    .GetRequiredService<FanficDownloadService>();

                        var file = await downloadService.BuildTxtAsync(job.Url, queueCt);
                        job.Result = file.Bytes;
                        tcs.SetResult(file);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                });
        var result = await tcs.Task;

        if (job.RequesterId != HttpContext.TraceIdentifier)
        {
            _logger.LogError("CRITICAL: WEB TXT job mismatch! JobId={JobId}", job.Id);
            return StatusCode(500, "Job mismatch");
        }

        if (job.Result == null)
        {
            _logger.LogError("CRITICAL: WEB TXT job result null! JobId={JobId}", job.Id);
            return StatusCode(500, "Job failed");
        }

        return File(job.Result, result.ContentType, result.FileName);
    }


    [HttpPost("epub")]
    public async Task<IActionResult> DownloadEpub([FromForm] DownloadRequest request,
                                              CancellationToken ct)
    {
        var job = new DownloadJob
        {
            Url = request.Url,
            Format = "epub",
            RequesterId = HttpContext.TraceIdentifier
        };
        _logger.LogInformation("WEB JOB created: {JobId} | {Url}", job.Id, job.Url);

        var tcs = new TaskCompletionSource<DownloadFileResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        var position = _queue.GetQueueLength() + 1;
        Response.Headers["X-Queue-Position"] = position.ToString();

        await _queue.EnqueueWithPosition(async (queueCt) =>
                    {
                        try
                        {
                            using var scope = HttpContext.RequestServices.CreateScope();
                            var downloadService = scope.ServiceProvider
                                                    .GetRequiredService<FanficDownloadService>();

                            var file = await downloadService.BuildEpubAsync(job.Url, queueCt);

                            job.Result = file.Bytes;

                            tcs.SetResult(file);
                        }
                        catch (Exception ex)
                        {
                            tcs.SetException(ex);
                        }
                    });
        var result = await tcs.Task;
        if (job.RequesterId != HttpContext.TraceIdentifier)
        {
            _logger.LogError("CRITICAL: WEB job mismatch! JobId={JobId}", job.Id);
            return StatusCode(500, "Job mismatch");
        }

        if (job.Result == null)
        {
            _logger.LogError("CRITICAL: WEB job result null! JobId={JobId}", job.Id);
            return StatusCode(500, "Job failed");
        }
        return File(job.Result, result.ContentType, result.FileName);
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


    

}
